using UnityEngine;
using System.Collections.Generic;

public class SpawnedWaterChunk {
  public Bounds bounds;
  public GameObject gameObject;

  public SpawnedWaterChunk(Bounds bounds, GameObject gameObject) {
    this.bounds = bounds;
    this.gameObject = gameObject;
  }
}

public class WaterChunkManager : MonoBehaviour {
  public float viewDistance = 100f;
  public DistanceShape distanceShape;
  public Vector3 chunkSize = new Vector3(32f, 2f, 32f);
  public int chunkResolution = 32;
  public Material waterMaterial;
  public Transform waterParent;

  private List<QuadtreeChunk> m_quadtreeChunks = new List<QuadtreeChunk>();
  private List<SpawnedWaterChunk> m_SpawnedWaterChunks = new List<SpawnedWaterChunk>();
  private List<SpawnedWaterChunk> m_SpawnedWaterChunksToDelete = new List<SpawnedWaterChunk>();
  private List<Bounds> m_visibleChunkPositions = new List<Bounds>();
  private Dictionary<Bounds, SpawnedWaterChunk> m_chunkDictionary =
    new Dictionary<Bounds, SpawnedWaterChunk>();
  private HashSet<Bounds> m_visibleChunkPositionsHashSet =
    new HashSet<Bounds>();

  public float generatePeriod = 0.3f;
  private float m_generateTimer = 0.0f;

  public bool drawGizmos = true;

  [SerializeField] private TerrainShape m_terrainShape;

  public int levelsOfDetail = 8;
  private List<float> m_levelDistances;

  private void Awake() {
    if (!m_terrainShape) {
      m_terrainShape = GetComponent<TerrainShape>();
    }
  }

  private void CreateChunk(Bounds bounds) {
    // Create water object
    GameObject waterObj = new GameObject(string.Format(
      "{0}, {1}", bounds.center.x, bounds.center.z
    ));

    // Set position and parent
    waterObj.transform.position = new Vector3(
      bounds.center.x - bounds.extents.x,
      0f,
      bounds.center.z - bounds.extents.z
    );
    waterObj.transform.SetParent(waterParent);

    // Apply water component
    TerrainChunkWater water = waterObj.AddComponent<TerrainChunkWater>();
    water.seaLevel = m_terrainShape.seaLevel;
    water.resolution = new Vector2Int(chunkResolution, chunkResolution);
    water.size = new Vector2(bounds.size.x, bounds.size.z);
    water.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;

    // Add to the list
    SpawnedWaterChunk data = new SpawnedWaterChunk(bounds, waterObj);
    m_SpawnedWaterChunks.Add(data);
    m_chunkDictionary.Add(bounds, data);
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
     2f,
     2.5f
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
    for (int i = m_SpawnedWaterChunks.Count - 1; i >= 0; i--) {
      SpawnedWaterChunk chunk = m_SpawnedWaterChunks[i];
      Bounds chunkBounds = chunk.bounds;
      // Find a chunk with the same position
      bool foundPosition = m_visibleChunkPositionsHashSet.Contains(
        chunkBounds
      );

      if (!foundPosition) {
        m_SpawnedWaterChunks.Remove(chunk);
        m_chunkDictionary.Remove(chunk.bounds);
        Destroy(chunk.gameObject);
      }
    }
  }

  private void DeleteChunks() {
    // Delete chunks that are out of view
    for (int i = m_SpawnedWaterChunksToDelete.Count - 1; i >= 0; i--) {
      SpawnedWaterChunk chunkToDelete = m_SpawnedWaterChunksToDelete[i];

      Destroy(chunkToDelete.gameObject);
      m_SpawnedWaterChunksToDelete.RemoveAt(i);
    }
  }

  private void Update() {
    Camera camera = Camera.main;
    if (camera) {
      m_generateTimer += Time.deltaTime;
      if (m_generateTimer > generatePeriod) {
        m_generateTimer = 0f;

        UpdateVisibleChunkPositions(camera);
        UpdateFollowingVisibleChunks();
        DeleteChunks();
      }
    }
  }

  private void OnDrawGizmos() {
    if (drawGizmos) {
      Gizmos.color = new Color(1f, 1f, 1f, 0.1f);

      UpdateVisibleChunkPositions(Camera.main, true);

      Gizmos.color = Color.white;
      for (int i = 0; i < m_visibleChunkPositions.Count; i++) {
        Bounds bounds = m_visibleChunkPositions[i];
        Gizmos.DrawWireCube(bounds.center, bounds.size);
      }
    }
  }
}
