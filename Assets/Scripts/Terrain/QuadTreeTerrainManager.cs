using UnityEngine;
using System.Collections.Generic;

public class SpawnedChunk {
  public Bounds bounds;
  public TerrainChunk component;
  public GameObject gameObject;
  public bool needsUpdate = true;

  public SpawnedChunk(
    Bounds bounds,
    TerrainChunk component
  ) {
    this.bounds = bounds;
    this.component = component;
    this.gameObject = component.gameObject;
  }
}

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
  public float viewDistance = 100f;
  public Vector3 chunkSize = new Vector3(32f, 128f, 32f);
  public Vector3Int chunkResolution = new Vector3Int(32, 128, 32);
  public Material chunkMaterial;
  public bool debug;

  private List<QuadtreeChunk> m_quadtreeChunks = new List<QuadtreeChunk>();
  private List<SpawnedChunk> m_spawnedChunks = new List<SpawnedChunk>();
  private List<SpawnedChunk> m_spawnedChunksToDelete = new List<SpawnedChunk>();
  private List<Bounds> m_visibleChunkPositions = new List<Bounds>();
  private Dictionary<Bounds, SpawnedChunk> m_chunkDictionary =
    new Dictionary<Bounds, SpawnedChunk>();
  private HashSet<Bounds> m_visibleChunkPositionsHashSet =
    new HashSet<Bounds>();

  public float updatePeriod = 0.3f;
  private float m_updateTimer = 0.0f;
  public float generatePeriod = 0.02f;
  private float m_generateTimer = 0.0f;
  public int maxConsecutiveChunks = 2;
  public int maxConsecutiveChunksAtOneFrame = 2;

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
    float seaLevel = m_terrainShape.seaLevel * chunkSize.y;
    gameObject.transform.position = new Vector3(
      bounds.center.x - bounds.extents.x,
      -seaLevel,
      bounds.center.z - bounds.extents.z
    );
    gameObject.transform.SetParent(this.transform);

    // Create chunk component
    TerrainChunk chunk = gameObject.AddComponent<TerrainChunk>();

    // Hide the meshRenderer
    chunk.meshRenderer.enabled = false;

    // Add to the list
    SpawnedChunk data = new SpawnedChunk(bounds, chunk);
    m_spawnedChunks.Add(data);
    m_chunkDictionary.Add(bounds, data);

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

    // Create water object
    GameObject waterObj = new GameObject("Water");
    waterObj.transform.position = new Vector3(
      bounds.center.x - bounds.extents.x,
      0f,
      bounds.center.z - bounds.extents.z
    );
    waterObj.transform.SetParent(gameObject.transform);
  }

  private Vector3 FlatY(Vector3 worldPosition) {
    return new Vector3(
      worldPosition.x,
      0f,
      worldPosition.z
    );
  }

  private void UpdateVisibleChunkPositions(Camera camera, bool drawGizmos = false) {
    Vector3 cameraPosition = FlatY(camera.transform.position);

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

    m_visibleChunkPositions.Clear();
    m_visibleChunkPositionsHashSet.Clear();
    for (int i = 0; i < visibleQuadtreeChunks.Count; i++) {
      QuadtreeChunk chunk = visibleQuadtreeChunks[i];

      // Save the chunk
      Bounds bounds = new Bounds(
        chunk.bounds.center,
        new Vector3(chunk.bounds.size.x, chunkSize.y, chunk.bounds.size.z)
      );
      m_visibleChunkPositions.Add(bounds);
      m_visibleChunkPositionsHashSet.Add(bounds);
    }

    // Sort the array by measuring the distance from the chunk to the camera
    m_lastCameraPosition = cameraPosition;
    m_visibleChunkPositions.Sort(new DistanceToCameraComparer(camera));
    m_debugChunkCount = m_visibleChunkPositions.Count;

    // Set camera fog
    RenderSettings.fogStartDistance = 100f;
    RenderSettings.fogEndDistance = viewDistance;
  }

  private void UpdateFollowingVisibleChunks() {
    // Check if the chunks are already there
    foreach (Bounds bounds in m_visibleChunkPositions) {
      bool foundChunk = m_chunkDictionary.ContainsKey(bounds);

      if (!foundChunk) {
        CreateChunk(bounds);
      }
    }

    // Delete chunks that are out of view
    for (int i = m_spawnedChunks.Count - 1; i >= 0; i--) {
      SpawnedChunk chunk = m_spawnedChunks[i];
      Bounds chunkBounds = chunk.bounds;
      // Find a chunk with the same position
      bool foundPosition = m_visibleChunkPositionsHashSet.Contains(
        chunkBounds
      );

      if (!foundPosition) {
        m_spawnedChunks.Remove(chunk);
        m_chunkDictionary.Remove(chunk.bounds);
        chunk.gameObject.name = string.Format("(To Delete) {0}", chunk.gameObject.name);

        if (chunk.component.hasEverBeenGenerated) {
          m_spawnedChunksToDelete.Add(chunk);
        } else {
          chunk.component.DestroyOnNextFrame();
        }
      }
    }
  }

  private void RequestChunksGeneration() {
    int totalInProgress = 0;
    for (int index = 0; index < m_spawnedChunks.Count; index++) {
      SpawnedChunk chunk = m_spawnedChunks[index];

      if (chunk.component.isGenerating) {
        totalInProgress++;
      }
    }

    if (totalInProgress >= maxConsecutiveChunks) {
      return;
    }

    int requestsOnThisFrame = 0;

    // Tell chunks to generate their meshes
    // Check if the chunks are already there
    for (int index = 0; index < m_visibleChunkPositions.Count; index++) {
      Bounds bounds = m_visibleChunkPositions[index];

      if (m_chunkDictionary.ContainsKey(bounds)) {
        SpawnedChunk chunk = m_chunkDictionary[bounds];

        // Tell the chunk to start generating if the budget is available
        if (chunk.needsUpdate) {
          chunk.component.GenerateOnNextFrame();
          chunk.needsUpdate = false;
          requestsOnThisFrame++;
          totalInProgress++;
        }
      }

      if (
        requestsOnThisFrame >= maxConsecutiveChunksAtOneFrame
        || totalInProgress >= maxConsecutiveChunks
      ) {
        return;
      }
    }
  }

  private void DeleteChunks() {
    // Delete chunks that are out of view
    for (int i = m_spawnedChunksToDelete.Count - 1; i >= 0; i--) {
      SpawnedChunk chunkToDelete = m_spawnedChunksToDelete[i];

      if (chunkToDelete.component.isJobInProgress) {
        continue;
      }

      // Find the chunks intersecting this chunk
      bool areAllReady = true;
      for (int j = 0; j < m_spawnedChunks.Count; j++) {
        SpawnedChunk chunkB = m_spawnedChunks[j];

        if (
          chunkB.bounds.Intersects(chunkToDelete.bounds)
          && !chunkB.component.hasEverBeenGenerated
        ) {
          areAllReady = false;
          break;
        }
      }

      if (areAllReady) {
        Destroy(chunkToDelete.gameObject);
        m_spawnedChunksToDelete.RemoveAt(i);
      }
    }

    // Show chunks that are completed
    for (int j = 0; j < m_spawnedChunks.Count; j++) {
      SpawnedChunk chunk = m_spawnedChunks[j];
      if (chunk.component.hasEverBeenGenerated)
        chunk.component.meshRenderer.enabled = true;
    }
  }

  private void Update() {
    m_generateTimer += Time.deltaTime;
    if (m_generateTimer > generatePeriod) {
      m_generateTimer = 0f;
      DeleteChunks();
      RequestChunksGeneration();
    }

    Camera camera = Camera.main;
    if (camera) {
      m_updateTimer += Time.deltaTime;
      if (m_updateTimer > updatePeriod) {
        m_updateTimer = 0f;

        UpdateVisibleChunkPositions(camera);
        UpdateFollowingVisibleChunks();
      }
    }
  }

  private void OnDrawGizmos() {
    if (drawGizmos) {
      Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
      Gizmos.DrawSphere(m_lastCameraPosition, viewDistance);

      UpdateVisibleChunkPositions(Camera.main, true);

      Gizmos.color = Color.white;
      for (int i = 0; i < m_visibleChunkPositions.Count; i++) {
        Bounds bounds = m_visibleChunkPositions[i];
        Gizmos.DrawWireCube(bounds.center, bounds.size);
      }
    }
  }
}
