using UnityEngine;
using System.Collections.Generic;

struct DistanceToCameraComparer : IComparer<Bounds> {
  public Vector3 cameraPosition;
  public Plane[] cameraPlanes;

  public DistanceToCameraComparer(Camera camera) {
    this.cameraPosition = camera.transform.position;
    this.cameraPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
  }

  public int Compare(Bounds a, Bounds b) {
    bool isAInside = GeometryUtility.TestPlanesAABB(cameraPlanes, a);
    bool isBInside = GeometryUtility.TestPlanesAABB(cameraPlanes, b);

    if (isAInside != isBInside) {
      return isBInside.CompareTo(isAInside);
    }

    float distanceA =
      (a.center.x - cameraPosition.x) * (a.center.x - cameraPosition.x)
      + (a.center.z - cameraPosition.z) * (a.center.z - cameraPosition.z);

    float distanceB =
      (b.center.x - cameraPosition.x) * (b.center.x - cameraPosition.x)
      + (b.center.z - cameraPosition.z) * (b.center.z - cameraPosition.z);

    return distanceA.CompareTo(distanceB);
  }
}

public class QuadTreeTerrainManager : MonoBehaviour {
  public Camera usedCamera { get { return Camera.main; } }

  public float viewDistance = 100f;
  public Vector3 chunkSize = new Vector3(32f, 128f, 32f);
  public Vector3Int chunkResolution = new Vector3Int(32, 128, 32);
  public Material chunkMaterial;
  public bool debug;
  public bool debugConsecutiveChunksGenerations;

  public float seaWorldLevel { get { return m_terrainShape.seaLevel * chunkSize.y; } }

  private List<QuadtreeChunk> m_quadtreeChunks = new List<QuadtreeChunk>();
  private List<TerrainChunk> m_spawnedChunks = new List<TerrainChunk>();
  private List<TerrainChunk> m_spawnedChunksToDelete = new List<TerrainChunk>();
  private Dictionary<Bounds, TerrainChunk> m_spawnedChunksDictionary =
    new Dictionary<Bounds, TerrainChunk>();

  private List<Bounds> m_visibleChunkBounds = new List<Bounds>();
  private HashSet<Bounds> m_visibleChunkBoundsHashSet =
    new HashSet<Bounds>();

  public event System.Action<TerrainChunk> ChunkGenerated;
  public event System.Action<TerrainChunk, List<TerrainChunk>> ChunkReplaced;
  public event System.Action<TerrainChunk> ChunkDeleted;

  public float updatePeriod = 0.3f;
  private float m_updateTimer = 0.0f;
  public float generatePeriod = 0.02f;
  private float m_generateTimer = 0.0f;
  public int maxConsecutiveChunks = 8;

  public bool drawGizmos = true;

  private Vector3 m_lastCameraPosition;

  [SerializeField] private TerrainShape m_terrainShape;

  public DistanceShape distanceShape;
  public int levelsOfDetail = 8;
  public float detailDistanceBase = 2f;
  public float detailDistanceMultiplier = 1f;
  public int detailDistanceDecreaseAtLevel = 1;
  public float detailDistanceConstantDecrease = 0f;
  private List<float> m_levelDistances;
  [SerializeField] private int m_debugChunkCount;

  private void Awake() {
    if (!m_terrainShape) {
      m_terrainShape = GetComponent<TerrainShape>();
    }
  }

  private void CreateChunk(Bounds bounds) {
    // Create empty GameObject
    GameObject gameObject = new GameObject(string.Format(
      "{0}, {1}", bounds.center.x, bounds.center.z
    ));

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
    float resolutionLevel = chunkSize.x / bounds.size.x;

    // Set variables
    chunk.drawGizmos = false;
    chunk.debug = debug;
    chunk.terrainShape = m_terrainShape;
    chunk.size = bounds.size;
    chunk.resolution = new Vector3Int(
      chunkResolution.x,
      Mathf.CeilToInt(chunkResolution.y * resolutionLevel),
      chunkResolution.z
    );
    chunk.GetComponent<MeshRenderer>().sharedMaterial = chunkMaterial;
  }

  private Vector3 FlatY(Vector3 worldPosition) {
    return new Vector3(
      worldPosition.x,
      0f,
      worldPosition.z
    );
  }

  private void UpdateVisibleChunks(Camera camera, bool drawGizmos = false) {
    Vector3 cameraPosition = FlatY(camera.transform.position);
    Vector3 quadChunkOffset = new Vector3(0f, -seaWorldLevel + chunkSize.y / 2f, 0f);

    m_levelDistances = QuadtreeChunk.CalculateLevelDistances(
      chunkSize.x,
      levelsOfDetail,
      detailDistanceBase,
      detailDistanceMultiplier,
      detailDistanceDecreaseAtLevel,
      detailDistanceConstantDecrease
    );

    m_quadtreeChunks = QuadtreeChunk.CreateQuadtree(
      cameraPosition,
      chunkSize,
      quadChunkOffset,
      m_levelDistances,
      viewDistance,
      distanceShape,
      m_quadtreeChunks,
      drawGizmos
    );

    List<QuadtreeChunk> visibleQuadtreeChunks = QuadtreeChunk.RetrieveVisibleChunks(
      m_quadtreeChunks,
      cameraPosition,
      viewDistance
    );

    m_visibleChunkBounds.Clear();
    m_visibleChunkBoundsHashSet.Clear();
    for (int i = 0; i < visibleQuadtreeChunks.Count; i++) {
      QuadtreeChunk chunk = visibleQuadtreeChunks[i];

      // Save the chunk
      m_visibleChunkBounds.Add(chunk.bounds);
      m_visibleChunkBoundsHashSet.Add(chunk.bounds);
    }

    // Sort the array by measuring the distance from the chunk to the camera
    m_lastCameraPosition = cameraPosition;
    m_visibleChunkBounds.Sort(new DistanceToCameraComparer(camera));
    m_debugChunkCount = m_visibleChunkBounds.Count;

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
        chunk.gameObject.name = string.Format("(To Delete) {0}", chunk.gameObject.name);

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

    if (debugConsecutiveChunksGenerations) {
      Debug.LogFormat("totalInProgress: {0}, totalSpawned: {1}", totalInProgress, totalSpawned);
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

  private void Update() {
    m_generateTimer += Time.deltaTime;
    if (m_generateTimer > generatePeriod) {
      m_generateTimer = 0f;
      DeleteReplacedChunks();
      RequestChunksGeneration();
    }

    if (usedCamera) {
      m_updateTimer += Time.deltaTime;
      if (m_updateTimer > updatePeriod) {
        m_updateTimer = 0f;

        UpdateVisibleChunks(usedCamera);
        UpdateSpawnedChunks();
      }
    }
  }

  private void OnDrawGizmos() {
    if (drawGizmos) {
      Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
      Gizmos.DrawSphere(m_lastCameraPosition, viewDistance);

      UpdateVisibleChunks(Camera.main, true);

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
