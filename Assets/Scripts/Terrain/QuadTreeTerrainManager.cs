using UnityEngine;
using System.Collections.Generic;

class ChunkTree {
  public List<float> levelDistances;
  public int level;
  public Vector2 position;
  public Vector2 extents;
  public ChunkTree[] children;
  public Bounds bounds;

  public ChunkTree(List<float> levelDistances, int level, Vector2 position, Vector2 extents) {
    this.levelDistances = levelDistances;
    this.level = level;
    this.position = position;
    this.extents = extents;
    this.children = null;
    this.bounds = new Bounds(
      new Vector3(position.x, 0f, position.y),
      new Vector3(extents.x * 2f, 1f, extents.y * 2f)
    );
  }

  public void Build(Vector3 cameraPosition, bool drawGizmos = false) {
    if (level > levelDistances.Count - 1)
      return;

    Vector3 closestPoint = bounds.ClosestPoint(cameraPosition);
    float levelDistance = levelDistances[level];

    if (drawGizmos) {
      Gizmos.color = Color.red;
      Gizmos.DrawWireSphere(cameraPosition, levelDistance);
    }

    if (Vector3.Distance(cameraPosition, closestPoint) <= levelDistance) {
      Vector2 halfExtents = extents / 2f;
      children = new ChunkTree[4] {
        // North east
        new ChunkTree(
          levelDistances,
          level + 1,
          position + halfExtents,
          halfExtents
        ),
        // South east
        new ChunkTree(
          levelDistances,
          level + 1,
          position + new Vector2(halfExtents.x, -halfExtents.y),
          halfExtents
        ),
        // South west
        new ChunkTree(
          levelDistances,
          level + 1,
          position - halfExtents,
          halfExtents
        ),
        // North west
        new ChunkTree(
          levelDistances,
          level + 1,
          position + new Vector2(-halfExtents.x, halfExtents.y),
          halfExtents
        )
      };

      foreach (ChunkTree child in children) {
        child.Build(cameraPosition, drawGizmos);
      }
    }
  }

  public List<ChunkTree> GetChunksRecursively(List<ChunkTree> list = null) {
    list = list ?? new List<ChunkTree>();
    list.Add(this);

    if (children != null) {
      foreach (ChunkTree child in children) {
        child.GetChunksRecursively(list);
      }
    }

    return list;
  }
}

public struct ChunkTransform {
  public Vector3 position;
  public Vector3 size;
  public Bounds bounds;

  public ChunkTransform(Vector3 position, Vector3 size) {
    this.position = position;
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
      return isBInside.CompareTo(isAInside);
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

public class QuadTreeTerrainManager : MonoBehaviour {
  public float viewDistance = 100f;
  public Vector3 chunkSize = new Vector3(32f, 128f, 32f);
  public Vector3Int chunkResolution = new Vector3Int(32, 128, 32);
  public Material chunkMaterial;
  public bool debug;

  private List<ChunkTree> m_chunkTrees = new List<ChunkTree>();
  private List<SpawnedChunk> m_spawnedChunks = new List<SpawnedChunk>();
  private List<SpawnedChunk> m_spawnedChunksToDelete = new List<SpawnedChunk>();
  private List<ChunkTransform> m_visibleChunkPositions = new List<ChunkTransform>();
  private Dictionary<ChunkTransform, SpawnedChunk> m_chunkDictionary =
    new Dictionary<ChunkTransform, SpawnedChunk>();
  private HashSet<ChunkTransform> m_visibleChunkPositionsHashSet =
    new HashSet<ChunkTransform>();

  public float updatePeriod = 0.3f;
  private float m_updateTimer = 0.0f;
  public float generatePeriod = 0.02f;
  private float m_generateTimer = 0.0f;
  public int maxConsecutiveChunks = 2;
  public int maxConsecutiveChunksAtOneFrame = 2;

  public bool drawGizmos = true;

  private Vector3 m_lastCameraPosition;

  [SerializeField] private TerrainNoise m_terrainNoise;

  public float levelsOfDetail = 8f;
  public float detailDistanceBase = 2f;
  public float detailDistanceMultiplier = 1f;
  public int detailDistanceDecreaseAtLevel = 1;
  public float detailDistanceConstantDecrease = 0f;
  private List<float> m_levelDistances = new List<float>();
  [SerializeField] private int m_debugChunkCount;

  private void Awake() {
    if (!m_terrainNoise) {
      m_terrainNoise = GetComponent<TerrainNoise>();
    }
  }
  private void CreateChunk(ChunkTransform transform) {
    // Create empty GameObject
    GameObject gameObject = new GameObject(string.Format(
      "{0}, {1}", transform.position.x, transform.position.z
    ));

    // Set position and parent
    float seaLevel = m_terrainNoise.seaLevel * chunkSize.y;
    gameObject.transform.position = new Vector3(
      transform.position.x,
      -seaLevel,
      transform.position.z
    );
    gameObject.transform.SetParent(base.transform);

    // Create chunk component
    TerrainChunk chunk = gameObject.AddComponent<TerrainChunk>();

    // Hide the meshRenderer
    chunk.meshRenderer.enabled = false;

    // Add to the list
    SpawnedChunk data = new SpawnedChunk(transform, chunk);
    m_spawnedChunks.Add(data);
    m_chunkDictionary.Add(transform, data);

    // Add mesh collider
    gameObject.AddComponent<MeshCollider>();

    // Calculate the resolution level
    float resolutionLevel = chunkSize.x / transform.size.x;

    // Set variables
    chunk.drawGizmos = false;
    chunk.debug = debug;
    chunk.samplerFactory = m_terrainNoise;
    chunk.size = transform.size;
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

  private void UpdateChunkTrees(Vector3 cameraPosition, bool drawGizmos = false) {
    m_levelDistances.Clear();

    // Calculate the whole size of the tree so that the minimun size of a chunk
    // is equal to chunkSize.x
    float minimunChunkSize = chunkSize.x;
    float areaExtents = Mathf.Pow(2f, levelsOfDetail - 1f) * minimunChunkSize;
    float areaSize = areaExtents * 2f;
    Vector2 extents = new Vector2(areaExtents, areaExtents);

    // Calculate the distances for the levels of detail
    for (int i = 0; i < levelsOfDetail; i++) {
      int decreaseLevel = Mathf.Max(0, i - detailDistanceDecreaseAtLevel);
      m_levelDistances.Add(
        (
          (Mathf.Pow(detailDistanceBase, i + 1f) * minimunChunkSize)
          / (1f + (float)decreaseLevel * detailDistanceConstantDecrease)
        ) * detailDistanceMultiplier
      );
    }
    m_levelDistances.Reverse();

    // Harcoded distances for the levels of detail
    // m_levelDistances = new List<float> {
    //   8192f,
    //   4096f,
    //   2048f,
    //   1024f,
    //   512f,
    //   256f,
    //   128f,
    //   64f
    // };

    // Create the tree
    m_chunkTrees.Clear();

    // Get the area the player is standing right now
    Vector2 mainAreaCoords = new Vector2(
      Mathf.Floor(cameraPosition.x / areaSize),
      Mathf.Floor(cameraPosition.z / areaSize)
    );
    Vector2 mainAreaPosition = new Vector2(
      mainAreaCoords.x * areaSize,
      mainAreaCoords.y * areaSize
    );

    // ChunkTree newTree = new ChunkTree(
    //   m_levelDistances,
    //   0,
    //   mainAreaPosition + extents,
    //   extents
    // );
    // newTree.Build(cameraPosition, drawGizmos);
    // m_chunkTrees.Add(newTree);

    int visibleX = Mathf.CeilToInt(viewDistance / areaSize);
    int visibleY = Mathf.CeilToInt(viewDistance / areaSize);

    // Build a list of the coords of the visible chunks
    for (
      int y = (int)mainAreaCoords.y - visibleY;
      y <= mainAreaCoords.y + visibleY;
      y++) {
      for (
        int x = (int)mainAreaCoords.x - visibleX;
        x <= mainAreaCoords.x + visibleX;
        x++
      ) {
        Vector2 coords = new Vector3(x, y);
        Vector2 position = new Vector2(
          coords.x * areaSize + areaExtents,
          coords.y * areaSize + areaExtents
        );
        Bounds bounds = new Bounds(
          new Vector3(position.x, 0, position.y),
          new Vector3(extents.x * 2f, chunkSize.y, extents.y * 2f)
        );

        // Check if a sphere of radius 'distance' is touching the chunk
        float distanceToChunk = Mathf.Sqrt(bounds.SqrDistance(cameraPosition));
        if (distanceToChunk > viewDistance) {
          continue;
        }

        if (drawGizmos) {
          Gizmos.color = Color.blue;
          Gizmos.DrawWireCube(
            new Vector3(position.x, 0, position.y),
            new Vector3(extents.x * 2f, chunkSize.y, extents.y * 2f)
          );
        }

        ChunkTree area = new ChunkTree(
          m_levelDistances,
          0,
          position,
          new Vector2(areaExtents, areaExtents)
        );
        area.Build(cameraPosition, drawGizmos);
        m_chunkTrees.Add(area);
      }
    }
  }

  private List<ChunkTree> GetChunksFromTrees() {
    List<ChunkTree> list = new List<ChunkTree>();

    foreach (ChunkTree child in m_chunkTrees) {
      child.GetChunksRecursively(list);
    }

    return list;
  }

  private void UpdateVisibleChunkPositions(Camera camera, bool drawGizmos = false) {
    Vector3 cameraPosition = FlatY(camera.transform.position);
    UpdateChunkTrees(cameraPosition, drawGizmos);

    float sqrViewDistance = viewDistance * viewDistance;

    m_visibleChunkPositions.Clear();
    m_visibleChunkPositionsHashSet.Clear();
    foreach (ChunkTree chunk in GetChunksFromTrees()) {
      if (chunk.children == null) {
        Vector3 closestPoint = chunk.bounds.ClosestPoint(cameraPosition);

        if (Vector3.Distance(closestPoint, cameraPosition) <= viewDistance) {
          // Save the chunk
          ChunkTransform transform = new ChunkTransform(
            new Vector3(chunk.position.x - chunk.extents.x, 0f, chunk.position.y - chunk.extents.y),
            new Vector3(chunk.extents.x * 2f, chunkSize.y, chunk.extents.y * 2f)
          );
          m_visibleChunkPositions.Add(transform);
          m_visibleChunkPositionsHashSet.Add(transform);
        }
      }
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
    foreach (ChunkTransform position in m_visibleChunkPositions) {
      bool foundChunk = m_chunkDictionary.ContainsKey(position);

      if (!foundChunk) {
        CreateChunk(position);
      }
    }

    // Delete chunks that are out of view
    for (int i = m_spawnedChunks.Count - 1; i >= 0; i--) {
      SpawnedChunk chunk = m_spawnedChunks[i];
      ChunkTransform chunkPosition = chunk.transform;
      // Find a chunk with the same position
      bool foundPosition = m_visibleChunkPositionsHashSet.Contains(
        chunkPosition
      );

      if (!foundPosition) {
        m_spawnedChunks.Remove(chunk);
        m_chunkDictionary.Remove(chunk.transform);
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
      ChunkTransform position = m_visibleChunkPositions[index];

      if (m_chunkDictionary.ContainsKey(position)) {
        SpawnedChunk chunk = m_chunkDictionary[position];

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
      foreach (ChunkTransform chunk in m_visibleChunkPositions) {
        Gizmos.DrawWireCube(chunk.position + chunk.size / 2f, chunk.size);
      }
    }
  }
}
