using UnityEngine;
using System.Collections.Generic;

public class OptimizedTerrainManager : MonoBehaviour {
  public struct ChunkTransform {
    public Vector3 coords;
    public Vector3 position;
    public Vector3 size;
    public Bounds bounds;

    public ChunkTransform(Vector3 coords, Vector3 position, Vector3 size) {
      this.coords = position;
      this.position = coords;
      this.size = size;
      this.bounds = new Bounds(position + size / 2f, size);
    }

    public override bool Equals(System.Object objB) {
      if (objB == null || GetType() != objB.GetType()) return false;
      ChunkTransform b = (ChunkTransform)objB;
      return (this.position == b.position) && (this.size == b.size);
    }

    public static bool operator !=(ChunkTransform a, ChunkTransform b) {
      return (a.position != b.position) && (a.size != b.size);
    }

    public static bool operator ==(ChunkTransform a, ChunkTransform b) {
      return (a.position == b.position) && (a.size == b.size);
    }

    public override int GetHashCode() {
      return position.GetHashCode() ^ size.GetHashCode();
    }
  }

  public class SpawnedChunk {
    public ChunkTransform transform;
    public TerrainChunk component;
    public GameObject gameObject;
    public bool needsUpdate = true;
    public Bounds bounds;

    public SpawnedChunk(
      ChunkTransform transform,
      TerrainChunk component
    ) {
      this.transform = transform;
      this.component = component;
      this.gameObject = component.gameObject;
      this.bounds = new Bounds(
        transform.position + transform.size / 2f,
        transform.size
      );
    }
  }

  struct DistanceToCameraComparer : IComparer<ChunkTransform> {
    public Vector3 cameraPosition;
    public Plane[] cameraPlanes;

    public DistanceToCameraComparer(Camera camera) {
      this.cameraPosition = camera.transform.position;
      this.cameraPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
    }

    public int Compare(ChunkTransform a, ChunkTransform b) {
      bool isAInside = GeometryUtility.TestPlanesAABB(cameraPlanes, a.bounds);
      bool isBInside = GeometryUtility.TestPlanesAABB(cameraPlanes, b.bounds);

      if (isAInside != isBInside) {
        return isAInside.CompareTo(isBInside);
      }

      float distanceA =
      (a.position.x - cameraPosition.x) * (a.position.x - cameraPosition.x)
      + (a.position.z - cameraPosition.z) * (a.position.z - cameraPosition.z);

      float distanceB =
        (b.position.x - cameraPosition.x) * (b.position.x - cameraPosition.x)
        + (b.position.z - cameraPosition.z) * (b.position.z - cameraPosition.z);

      return distanceA.CompareTo(distanceB);
    }
  }

  public float viewDistance = 100f;
  public Vector3 chunkSize = new Vector3(32f, 64f, 32f);
  public Vector3Int chunkResolution = new Vector3Int(32, 64, 32);
  public float waterLevel = 40f;
  public Material chunkMaterial;
  public bool debug;

  public int noiseOctaves = 3;
  public float lodDistance = 75f;

  private List<SpawnedChunk> m_chunks = new List<SpawnedChunk>();
  private List<SpawnedChunk> m_chunksToDelete = new List<SpawnedChunk>();
  private List<ChunkTransform> m_visibleChunkPositions = new List<ChunkTransform>();
  private Dictionary<ChunkTransform, SpawnedChunk> m_chunkDictionary =
    new Dictionary<ChunkTransform, SpawnedChunk>();
  private HashSet<ChunkTransform> m_visibleChunkPositionsHashSet =
    new HashSet<ChunkTransform>();

  public float updatePeriod = 0.3f;
  private float m_updateTimer = 0.0f;
  public float generatePeriod = 0.02f;
  private float m_generateTimer = 0.0f;
  public int maxNumberOfChunksToGenerate = 2;

  public bool drawGizmos = true;

  private Vector3 m_lastCameraPosition;

  [SerializeField] private TerrainNoise m_terrainNoise;

  private void Awake() {
    if (!m_terrainNoise) {
      m_terrainNoise = GetComponent<TerrainNoise>();
    }
  }

  private void CreateChunk(ChunkTransform chunkPosition) {
    // Create empty GameObject
    GameObject gameObject = new GameObject(string.Format(
      "{0}, {1}", chunkPosition.position.x, chunkPosition.position.z
    ));

    // Set position and parent
    gameObject.transform.position = new Vector3(
      chunkPosition.position.x,
      -waterLevel,
      chunkPosition.position.z
    );
    gameObject.transform.localScale = chunkPosition.size;
    gameObject.transform.SetParent(transform);

    // Create chunk component
    TerrainChunk chunk = gameObject.AddComponent<TerrainChunk>();

    // Hide the meshRenderer
    chunk.meshRenderer.enabled = false;

    // Add to the list
    SpawnedChunk data = new SpawnedChunk(chunkPosition, chunk);
    m_chunks.Add(data);
    m_chunkDictionary.Add(chunkPosition, data);

    // Add mesh collider
    gameObject.AddComponent<MeshCollider>();

    // Calculate the resolution level
    float resolutionLevel = chunkPosition.size.x / chunkSize.x;

    // Set variables
    chunk.debug = debug;
    chunk.samplerFactory = m_terrainNoise;
    chunk.resolution = new Vector3Int(
      chunkResolution.x,
      Mathf.CeilToInt(chunkSize.y / resolutionLevel),
      chunkResolution.z
    );
    chunk.GetComponent<MeshRenderer>().sharedMaterial = chunkMaterial;
  }

  private Vector3 GetNearestChunkCoordsTo(Vector3 worldPosition) {
    return new Vector3(
      Mathf.Round(worldPosition.x / chunkSize.x),
      0f,
      Mathf.Round(worldPosition.z / chunkSize.z)
    );
  }

  private Vector3 GetChunkPositionFromCoords(Vector3 chunkCoords) {
    return new Vector3(
     chunkCoords.x * chunkSize.x,
     0f,
     chunkCoords.z * chunkSize.z
   );
  }

  private Vector3 FlatY(Vector3 worldPosition) {
    return new Vector3(
      worldPosition.x,
      0f,
      worldPosition.z
    );
  }

  private void UpdateVisibleChunkPositions(Camera camera) {
    m_visibleChunkPositions.Clear();
    m_visibleChunkPositionsHashSet.Clear();

    // Get the camera position and bounds
    Vector3 cameraPosition = FlatY(camera.transform.position);
    Bounds cameraBounds = new Bounds(
      cameraPosition,
      Vector3.one * viewDistance * 2f
    );

    // Get the chunk the player is standing right now
    Vector3 mainChunkCoords = GetNearestChunkCoordsTo(cameraPosition);

    // The first square is 2x2
    int level = 0;
    int currentEdgeSize = 2;
    // There are four edges
    int currentEdgeIndex = 0;
    int currentEdgePointIndex = 0;

    float currentSize = 1;
    Vector3 currentCoords = mainChunkCoords + Vector3.left;
    Vector3 currentDirection = Vector3.right;
    while (true) {
      // Get world position
      Vector3 position = GetChunkPositionFromCoords(currentCoords);
      // Get world size
      Vector3 size = chunkSize * currentSize;
      size.y = chunkSize.y;

      // Construct a bounds to measure the distance
      Bounds bounds = new Bounds(position + size / 2f, size);

      if (!bounds.Intersects(cameraBounds)) {
        break;
      }

      // Save the chunk
      ChunkTransform chunk = new ChunkTransform(position, currentCoords, size);
      m_visibleChunkPositions.Add(chunk);
      m_visibleChunkPositionsHashSet.Add(chunk);

      // Move along the current edge
      currentEdgePointIndex++;

      // Change direction
      if (currentEdgePointIndex >= currentEdgeSize) {
        currentEdgeIndex++;
        currentEdgePointIndex = 1;

        if (currentDirection == Vector3.right) {
          currentDirection = Vector3.back;
        } else if (currentDirection == Vector3.back) {
          currentDirection = Vector3.left;
        } else if (currentDirection == Vector3.left) {
          currentDirection = Vector3.forward;
        }
      }

      currentCoords += currentDirection * currentSize;

      // Go to the next level
      if (currentEdgeIndex == 3 && currentEdgePointIndex >= currentEdgeSize - 1) {
        level++;
        if (level > 1) {
          currentSize *= 2;
        }

        currentEdgeSize = 4;

        currentEdgeIndex = 0;
        currentEdgePointIndex = 0;
        currentDirection = Vector3.right;

        if (level > 1) {
          currentCoords = currentCoords + Vector3.forward * currentSize * 0.5f + Vector3.left * currentSize;
        } else {
          currentCoords = currentCoords + Vector3.forward * currentSize + Vector3.left * currentSize;
        }
      }
    }

    // Sort the array by measuring the distance from the chunk to the camera
    m_lastCameraPosition = cameraPosition;
    m_visibleChunkPositions.Sort(new DistanceToCameraComparer(camera));
  }

  private void UpdateFollowingVisibleChunks() {
    // Check if the chunks are already there
    foreach (ChunkTransform position in m_visibleChunkPositions) {
      bool foundChunk = m_chunkDictionary.ContainsKey(position);

      if (!foundChunk) {
        CreateChunk(position);
      }
    }

    // Delete chunks that are out of view
    for (int i = m_chunks.Count - 1; i >= 0; i--) {
      SpawnedChunk chunk = m_chunks[i];
      ChunkTransform chunkPosition = chunk.transform;
      // Find a chunk with the same position
      bool foundPosition = m_visibleChunkPositionsHashSet.Contains(
        chunkPosition
      );

      if (!foundPosition) {
        m_chunks.Remove(chunk);
        m_chunkDictionary.Remove(chunk.transform);
        chunk.gameObject.name = string.Format("(To Delete) {0}", chunk.gameObject.name);

        if (chunk.component.hasEverBeenGenerated) {
          m_chunksToDelete.Add(chunk);
        } else {
          chunk.component.DestroyOnNextFrame();
        }
      }
    }
  }

  private void RequestChunksGeneration() {
    int totalInProgress = 0;
    for (int index = 0; index < m_chunks.Count; index++) {
      SpawnedChunk chunk = m_chunks[index];

      if (chunk.component.isGenerating) {
        totalInProgress++;
      }
    }

    if (totalInProgress >= maxNumberOfChunksToGenerate) {
      return;
    }

    // Tell chunks to generate their meshes
    // Check if the chunks are already there
    for (int index = 0; index < m_visibleChunkPositions.Count; index++) {
      ChunkTransform position = m_visibleChunkPositions[index];

      if (m_chunkDictionary.ContainsKey(position)) {
        SpawnedChunk chunk = m_chunkDictionary[position];

        // Tell the chunk to start generating if the budget is available
        if (chunk.needsUpdate) {
          chunk.component.GenerateOnNextFrame();
          chunk.needsUpdate = false;
          return;
        }
      }
    }
  }

  private void DeleteChunks() {
    // Delete chunks that are out of view
    for (int i = m_chunksToDelete.Count - 1; i >= 0; i--) {
      SpawnedChunk chunkToDelete = m_chunksToDelete[i];

      if (chunkToDelete.component.isJobInProgress) {
        continue;
      }

      // Find the chunks intersecting this chunk
      bool areAllReady = true;
      for (int j = 0; j < m_chunks.Count; j++) {
        SpawnedChunk chunkB = m_chunks[j];

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
        m_chunksToDelete.RemoveAt(i);
      }
    }

    // Show chunks that are completed
    for (int j = 0; j < m_chunks.Count; j++) {
      SpawnedChunk chunk = m_chunks[j];
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
      UpdateVisibleChunkPositions(Camera.main);

      Gizmos.color = Color.black;
      Gizmos.DrawWireCube(m_lastCameraPosition, Vector3.one * viewDistance * 2f);

      Gizmos.color = Color.white;
      foreach (ChunkTransform chunk in m_visibleChunkPositions) {
        Gizmos.DrawWireCube(chunk.position + chunk.size / 2f, chunk.size);
      }
    }
  }
}
