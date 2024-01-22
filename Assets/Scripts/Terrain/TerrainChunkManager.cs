using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;
using System.Collections.Generic;

public class TerrainChunkManager : MonoBehaviour {
  public Camera usedCamera { get { return Camera.main; } }

  [Header("Chunks")]
  [FormerlySerializedAs("chunkSize")]
  public Vector3 chunkScale = new Vector3(32f, 128f, 32f);
  public Vector3Int chunkResolution = new Vector3Int(32, 128, 32);
  public Material chunkMaterial;
  public TerrainShape terrainShape { get { return m_terrainShape; } }
  [SerializeField] private TerrainShape m_terrainShape;

  [Header("Chunk Distribution")]
  public float viewDistance = 5000f;
  public DistanceShape distanceShape = DistanceShape.Circle;
  public int levelsOfDetail = 8;
  public float detailDistanceBase = 2f;
  public float detailDistanceMultiplier = 2.5f;
  public int detailDistanceDecreaseAtLevel = 1;
  public float detailDistanceConstantDecrease = 0f;

  [Header("Generation Periods")]
  public float updatePeriod = 0.1f;
  public float generatePeriod = 0.02f;
  public int maxConsecutiveChunks = 8;

  [Header("Debug")]
  public bool logGenerationInfo;
  public bool logGenerationsInProgress;
  public bool drawGizmos;

  public float seaWorldLevel { get { return m_terrainShape.seaLevel * chunkScale.y; } }

  private List<QuadtreeChunk> m_quadtreeChunks = new();
  private List<TerrainChunk> m_spawnedChunks = new();
  private List<TerrainChunk> m_spawnedChunksToDelete = new();
  private Dictionary<Bounds, TerrainChunk> m_spawnedChunksDictionary = new();

  private List<Bounds> m_visibleChunkBounds = new();
  private HashSet<Bounds> m_visibleChunkBoundsHashSet = new();

  private Vector3 m_lastCameraPosition;

  private List<float> m_levelDistances = new();
  private List<QuadtreeChunk> m_visibleQuadtreeChunks = new();

  public event System.Action<TerrainChunk> ChunkGenerated;
  public event System.Action<TerrainChunk> ChunkSpawned;
  public event System.Action<TerrainChunk, List<TerrainChunk>> ChunkReplaced;
  public event System.Action<TerrainChunk> ChunkDeleted;

  private void Start() {
    // If we don't access FastNoise from the main thread FIRST, for some reason it
    // crashes sometimes when accessed first on other threads (like in Jobs).
    // So here we instantiate the FastNoise class as a workaround.
    FastNoise fastNoise = new FastNoise("OpenSimplex2S");

    StartCoroutine(UpdateVisibleChunksCoroutine());
    StartCoroutine(GenerationCoroutine());
  }

  private IEnumerator UpdateVisibleChunksCoroutine() {
    while (true) {
      yield return new WaitForSeconds(updatePeriod);

      UpdateVisibleChunks();
      UpdateSpawnedChunks();
    }
  }

  private IEnumerator GenerationCoroutine() {
    while (true) {
      yield return new WaitForSeconds(generatePeriod);

      DeleteReplacedChunks();
      RequestChunksGeneration();
    }
  }

  private void CreateChunk(Bounds bounds) {
    // Create empty GameObject
    GameObject gameObject = new GameObject($"{bounds.center.x}, {bounds.center.z}");

    // Set position and parent
    gameObject.transform.position = bounds.center - bounds.extents;
    gameObject.transform.SetParent(this.transform);

    // Create chunk component
    TerrainChunk chunk = gameObject.AddComponent<TerrainChunk>();

    // Hide the meshRenderer
    chunk.meshRenderer.enabled = false;

    // Add to the list
    m_spawnedChunks.Add(chunk);
    m_spawnedChunksDictionary.Add(bounds, chunk);

    // Add mesh collider
    gameObject.AddComponent<MeshCollider>();

    // Calculate the resolution level
    float resolutionLevel = chunkScale.x / bounds.size.x;

    // Set variables
    chunk.terrainManager = this;
    chunk.drawGizmos = false;
    chunk.debug = logGenerationInfo;
    chunk.terrainShape = m_terrainShape;
    chunk.scale = bounds.size;
    chunk.resolution = new Vector3Int(
      chunkResolution.x,
      Mathf.CeilToInt(chunkResolution.y * resolutionLevel),
      chunkResolution.z
    );
    chunk.GetComponent<MeshRenderer>().sharedMaterial = chunkMaterial;

    // Events
    ChunkSpawned?.Invoke(chunk);
  }

  private void UpdateVisibleChunks(bool drawGizmos = false) {
    Vector3 cameraPosition = usedCamera.transform.position;
    Vector3 quadChunkOffset = new Vector3(0f, -seaWorldLevel + chunkScale.y / 2f, 0f);

    m_levelDistances = QuadtreeChunk.CalculateLevelDistances(
      m_levelDistances,
      levelsOfDetail,
      chunkScale.x,
      detailDistanceBase,
      detailDistanceMultiplier,
      detailDistanceDecreaseAtLevel,
      detailDistanceConstantDecrease
    );

    m_quadtreeChunks = QuadtreeChunk.CreateQuadtree(
      cameraPosition,
      chunkScale,
      quadChunkOffset,
      m_levelDistances,
      viewDistance,
      distanceShape,
      m_quadtreeChunks,
      drawGizmos
    );

    m_visibleQuadtreeChunks = QuadtreeChunk.RetrieveVisibleChunks(
      m_visibleQuadtreeChunks,
      m_quadtreeChunks,
      cameraPosition,
      viewDistance
    );

    m_visibleChunkBounds.Clear();
    m_visibleChunkBoundsHashSet.Clear();
    for (int i = 0; i < m_visibleQuadtreeChunks.Count; i++) {
      QuadtreeChunk chunk = m_visibleQuadtreeChunks[i];

      // Save the chunk
      m_visibleChunkBounds.Add(chunk.bounds);
      m_visibleChunkBoundsHashSet.Add(chunk.bounds);
    }

    // Sort the array by measuring the distance from the chunk to the camera
    m_lastCameraPosition = cameraPosition;
    VisibilitySortMode mode = m_spawnedChunksToDelete.Count > 50
      ? VisibilitySortMode.Farthest
      : VisibilitySortMode.NearestAndInsideFrustum;
    BoundsVisibilitySorter.Sort(m_visibleChunkBounds, usedCamera, mode);

    // Set camera fog
    RenderSettings.fogStartDistance = 100f;
    RenderSettings.fogEndDistance = viewDistance;
  }

  private void UpdateSpawnedChunks() {
    // Check if the chunks are already there
    for (int i = 0; i < m_visibleChunkBounds.Count; i++) {
      Bounds bounds = m_visibleChunkBounds[i];
      bool foundChunk = m_spawnedChunksDictionary.ContainsKey(bounds);

      if (!foundChunk) {
        CreateChunk(bounds);
      }
    }

    // Delete chunks that are out of view
    for (int i = m_spawnedChunks.Count - 1; i >= 0; i--) {
      TerrainChunk chunk = m_spawnedChunks[i];

      // Is this chunk still part of the visible chunks?
      bool foundPosition = m_visibleChunkBoundsHashSet.Contains(chunk.bounds);

      if (!foundPosition) {
        m_spawnedChunks.Remove(chunk);
        m_spawnedChunksDictionary.Remove(chunk.bounds);
        chunk.gameObject.name = $"(To Delete) {chunk.gameObject.name}";

        if (chunk.hasEverBeenGenerated) {
          m_spawnedChunksToDelete.Add(chunk);
        } else {
          chunk.ScheduleDestroy();

          // Events
          ChunkDeleted?.Invoke(chunk);
        }
      }
    }
  }

  private void RequestChunksGeneration() {
    // Count the number of chunks that are being generated
    int totalInProgress = 0;
    int totalSpawned = 0;
    for (int index = 0; index < m_spawnedChunks.Count; index++) {
      TerrainChunk chunk = m_spawnedChunks[index];
      if (chunk.status == TerrainChunkStatus.Generating) {
        totalInProgress++;
      }

      if (chunk.status == TerrainChunkStatus.Spawned) {
        totalSpawned++;
      }
    }

    if (logGenerationsInProgress) {
      Debug.Log(
        $"Total generations in progress: {totalInProgress}, total spawned: {totalSpawned}, to delete: {m_spawnedChunksToDelete.Count}"
      );
    }

    // Skip if we reach the limit of consecutive updates
    if (totalInProgress >= maxConsecutiveChunks) {
      return;
    }

    // Iterate the chunks to tell them to generate their meshes.
    // We use the "m_visibleChunkBounds" list because it's sorted.
    for (int index = 0; index < m_visibleChunkBounds.Count; index++) {
      Bounds bounds = m_visibleChunkBounds[index];

      // Check if the bounds is a spawned chunked
      if (m_spawnedChunksDictionary.ContainsKey(bounds)) {
        TerrainChunk chunk = m_spawnedChunksDictionary[bounds];

        // Tell the chunk to start generation
        if (chunk.status == TerrainChunkStatus.Spawned) {
          chunk.RequestUpdate();

          // Event to call when the chunk is ready
          void GenerationCompleted() {
            chunk.GenerationCompleted -= GenerationCompleted;
            chunk.meshRenderer.enabled = true;

            // Check if there are chunks inside this one (that means that this chunk is gonna
            // replace other chunks)
            bool isInsideAnotherChunk = false;
            for (int j = 0; j < m_spawnedChunksToDelete.Count; j++) {
              TerrainChunk chunkB = m_spawnedChunksToDelete[j];

              if (chunkB.bounds.IsInside(bounds) && chunkB.hasEverBeenGenerated) {
                isInsideAnotherChunk = true;
                break;
              }
            }

            // Events
            if (!isInsideAnotherChunk) {
              ChunkGenerated?.Invoke(chunk);
            }
          };

          // Attach event handler
          chunk.GenerationCompleted += GenerationCompleted;
          return;
        }
      }
    }
  }

  private void DeleteReplacedChunks() {
    // Find the chunks that were replaced by other chunks and delete them
    for (int i = m_spawnedChunksToDelete.Count - 1; i >= 0; i--) {
      TerrainChunk chunkToDelete = m_spawnedChunksToDelete[i];

      // Find out if all the new chunks inside this chunk are already generated
      bool areAllReady = true;
      List<TerrainChunk> insideChunks = new();
      for (int j = 0; j < m_spawnedChunks.Count; j++) {
        TerrainChunk chunkB = m_spawnedChunks[j];

        if (chunkB.bounds.IsInside(chunkToDelete.bounds)) {
          insideChunks.Add(chunkB);

          if (!chunkB.hasEverBeenGenerated) {
            areAllReady = false;
            break;
          }
        }
      }

      // If all the chunks inside are generated, we can safely delete this chunk
      if (areAllReady) {
        Destroy(chunkToDelete.gameObject);
        m_spawnedChunksToDelete.RemoveAt(i);

        // Events
        if (insideChunks.Count > 0) {
          // The chunk was replaced by other chunks
          ChunkReplaced?.Invoke(chunkToDelete, insideChunks);
        } else {
          // The chunk was completely deleted and it's empty space now
          ChunkDeleted?.Invoke(chunkToDelete);
        }
      }
    }
  }

  private void OnDrawGizmos() {
    if (drawGizmos) {
      Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
      Gizmos.DrawSphere(m_lastCameraPosition, viewDistance);

      UpdateVisibleChunks(true);

      Gizmos.color = Color.white;
      for (int i = 0; i < m_visibleChunkBounds.Count; i++) {
        Bounds bounds = m_visibleChunkBounds[i];
        Gizmos.DrawWireCube(bounds.center, bounds.size);
      }
    }
  }

  public TerrainChunk GetChunkAt(Vector3 position) {
    QuadtreeChunk chunk = QuadtreeChunk.GetChunkAt(m_quadtreeChunks, position);

    if (chunk != null) {
      return m_spawnedChunksDictionary.GetValueOrDefault(chunk.bounds);
    }

    return null;
  }
}
