using UnityEngine;
using System.Collections.Generic;

public class WaterChunkManager : MonoBehaviour {
  public float viewDistance = 100f;
  public DistanceShape distanceShape;
  public Vector3 chunkSize = new Vector3(32f, 2f, 32f);
  public int chunkResolution = 32;
  public Material waterMaterial;
  public Transform waterParent;

  private List<QuadtreeChunk> m_quadtreeChunks = new();
  private List<WaterChunk> m_spawnedChunks = new();
  private List<WaterChunk> m_spawnedChunksToDelete = new();
  private Dictionary<Bounds, WaterChunk> m_spawnedChunksDictionary = new();

  private List<Bounds> m_visibleChunkBounds = new();
  private HashSet<Bounds> m_visibleChunkBoundsHashSet = new();

  public float generatePeriod = 0.3f;
  private float m_generateTimer = 0.0f;

  public bool drawGizmos = true;

  [SerializeField] private TerrainShape m_terrainShape;

  public int levelsOfDetail = 8;
  private List<float> m_levelDistances = new();
  private List<QuadtreeChunkNode> m_visibleQuadtreeChunks = new();

  private void CreateChunk(Bounds bounds) {
    // Create water object
    GameObject waterObj = new GameObject($"{bounds.center.x}, {bounds.center.z}");

    // Set position and parent
    waterObj.transform.position = new Vector3(
      bounds.center.x - bounds.extents.x,
      0f,
      bounds.center.z - bounds.extents.z
    );
    waterObj.transform.SetParent(waterParent);

    // Apply water component
    WaterChunk chunk = waterObj.AddComponent<WaterChunk>();
    chunk.bounds = bounds;
    chunk.seaLevel = m_terrainShape.seaLevel;
    chunk.resolution = new Vector2Int(chunkResolution, chunkResolution);
    chunk.size = new Vector2(bounds.size.x, bounds.size.z);
    chunk.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;

    // Add to the list
    m_spawnedChunks.Add(chunk);
    m_spawnedChunksDictionary.Add(bounds, chunk);
  }

  private void UpdateVisibleChunkPositions(Camera camera, bool drawGizmos = false) {
    Vector3 cameraPosition = camera.transform.position;

    QuadtreeChunk.CalculateLevelDistances(
      m_levelDistances,
      levelsOfDetail,
      chunkSize.x,
      2f,
      2.5f
    );

    QuadtreeChunk.CreateQuadtrees(
      m_quadtreeChunks,
      cameraPosition,
      chunkSize,
      Vector3.zero,
      m_levelDistances,
      viewDistance,
      distanceShape,
      drawGizmos
    );

    QuadtreeChunk.RetrieveVisibleChunks(
      m_visibleChunkBounds,
      m_quadtreeChunks,
      cameraPosition,
      viewDistance
    );

    m_visibleChunkBoundsHashSet.Clear();
    for (int i = 0; i < m_visibleChunkBounds.Count; i++) {
      Bounds bounds = m_visibleChunkBounds[i];
      m_visibleChunkBoundsHashSet.Add(bounds);
    }
  }

  private void UpdateFollowingVisibleChunks() {
    // Check if the chunks are already there
    for (int i = 0; i < m_visibleChunkBounds.Count; i++) {
      Bounds bounds = m_visibleChunkBounds[i];

      if (!m_spawnedChunksDictionary.ContainsKey(bounds)) {
        CreateChunk(bounds);
      }
    }

    // Delete chunks that are out of view
    for (int i = m_spawnedChunks.Count - 1; i >= 0; i--) {
      WaterChunk chunk = m_spawnedChunks[i];
      // Find a chunk with the same position
      bool foundPosition = m_visibleChunkBoundsHashSet.Contains(chunk.bounds);

      if (!foundPosition) {
        m_spawnedChunks.Remove(chunk);
        m_spawnedChunksDictionary.Remove(chunk.bounds);
        Destroy(chunk.gameObject);
      }
    }
  }

  private void DeleteChunks() {
    // Delete chunks that are out of view
    for (int i = m_spawnedChunksToDelete.Count - 1; i >= 0; i--) {
      WaterChunk chunkToDelete = m_spawnedChunksToDelete[i];

      Destroy(chunkToDelete.gameObject);
      m_spawnedChunksToDelete.RemoveAt(i);
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
      for (int i = 0; i < m_visibleChunkBounds.Count; i++) {
        Bounds bounds = m_visibleChunkBounds[i];
        Gizmos.DrawWireCube(bounds.center, bounds.size);
      }
    }
  }
}
