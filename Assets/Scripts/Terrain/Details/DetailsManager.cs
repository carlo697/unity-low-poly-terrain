using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetailsManager : MonoBehaviour {
  public QuadTreeTerrainManager manager { get { return m_terrainManager; } }
  [SerializeField] private QuadTreeTerrainManager m_terrainManager;

  public TerrainShape terrainShape { get { return m_terrainShape; } }
  [SerializeField] private TerrainShape m_terrainShape;

  public float updateVisibleChunksPeriod = 0.5f;
  public float updateSpawnedChunksPeriod = 0.2f;
  public float viewDistance = 500f;
  public AnimationCurve levelOfDetailCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

  private List<DetailsChunk> m_spawnedChunks = new();
  private Dictionary<Bounds, DetailsChunk> m_spawnedChunksDictionary = new();

  private List<Bounds> m_visibleChunkBounds = new();
  private HashSet<Bounds> m_visibleChunkBoundsHashSet =
    new HashSet<Bounds>();
  public Transform detailsParent;

  [Header("Debug")]
  public bool drawGizmos;

  private void Start() {
    if (m_terrainShape.useDetails) {
      StartCoroutine(UpdateVisibleChunksCoroutine());
      StartCoroutine(UpdateSpawnedChunksCoroutine());
      m_terrainManager.ChunkGenerated += ChunkGeneratedEventHandler;
      m_terrainManager.ChunkReplaced += ChunkReplacedEventHandler;

      // Allocate prefabs in the pool
      for (int i = 0; i < terrainShape.detailSpawners.Length; i++) {
        DetailSpawner spawner = terrainShape.detailSpawners[i];

        for (int j = 0; j < spawner.detail.prefabs.Length; j++) {
          PrefabPool.Allocate(spawner.detail.prefabs[j], spawner.detail.preAllocateCount);
        }
      }
    }
  }

  private void OnDestroy() {
    m_terrainManager.ChunkGenerated -= ChunkGeneratedEventHandler;
    m_terrainManager.ChunkReplaced -= ChunkReplacedEventHandler;
  }

  private void ChunkGeneratedEventHandler(TerrainChunk generatedChunk) {
    UpdateChunksInside(generatedChunk.bounds);
  }

  private void ChunkReplacedEventHandler(TerrainChunk generatedChunk, List<TerrainChunk> replacedBy) {
    UpdateChunksInside(generatedChunk.bounds);
  }

  private void UpdateChunksInside(Bounds bounds) {
    Vector3 cameraPosition = m_terrainManager.usedCamera.transform.position;

    // Find the details chunks inside the generated terrain chunk
    for (int i = 0; i < m_spawnedChunks.Count; i++) {
      DetailsChunk chunk = m_spawnedChunks[i];
      if (chunk.bounds.IsInside(bounds)) {
        chunk.RequestUpdate(GetLevelOfDetail(cameraPosition, chunk.bounds.center));
      }
    }
  }

  public IEnumerator UpdateVisibleChunksCoroutine() {
    while (true) {
      UpdateVisibleChunks();
      yield return new WaitForSeconds(updateVisibleChunksPeriod);
    }
  }

  private void UpdateVisibleChunks(bool drawGizmos = false) {
    if (m_terrainManager.usedCamera == null) return;
    Vector3 camera3dPosition = m_terrainManager.usedCamera.transform.position;
    Vector2 camera2dPosition = new Vector2(camera3dPosition.x, camera3dPosition.z);

    Vector3 chunk3dSize = m_terrainManager.chunkSize;
    Vector3 chunk3dExtents = chunk3dSize / 2f;

    Vector2 chunk2dSize = new Vector2(
      m_terrainManager.chunkSize.x,
      m_terrainManager.chunkSize.z
    );
    Vector2 chunk2dExtents = chunk2dSize / 2f;

    // Get the area the player is standing right now
    Vector2Int currentChunk2dCoords = new Vector2Int(
      Mathf.FloorToInt(camera2dPosition.x / chunk2dSize.x),
      Mathf.FloorToInt(camera2dPosition.y / chunk2dSize.y)
    );

    int visibleX = Mathf.CeilToInt(viewDistance / chunk2dSize.x);
    int visibleY = Mathf.CeilToInt(viewDistance / chunk2dSize.y);

    // Delete last chunks
    m_visibleChunkBounds.Clear();
    m_visibleChunkBoundsHashSet.Clear();

    // Build a list of the coords of the visible chunks
    for (
      int y = currentChunk2dCoords.y - visibleY;
      y <= currentChunk2dCoords.y + visibleY;
      y++
    ) {
      for (
        int x = currentChunk2dCoords.x - visibleX;
        x <= currentChunk2dCoords.x + visibleX;
        x++
      ) {
        Vector2Int coords2d = new Vector2Int(x, y);
        Vector2 position2d = Vector2.Scale(coords2d, chunk2dSize) + chunk2dExtents;
        Vector3 position3d = new Vector3(position2d.x, m_terrainManager.seaWorldLevel, position2d.y);
        Bounds bounds = new Bounds(position3d, chunk3dSize);

        float distanceToChunk = Mathf.Sqrt(bounds.SqrDistance(camera3dPosition));
        if (distanceToChunk > viewDistance) {
          continue;
        }

        if (drawGizmos) {
          Gizmos.color = Color.Lerp(
            Color.black,
            Color.white,
            GetLevelOfDetail(camera3dPosition, bounds.center)
          );
          Gizmos.DrawCube(bounds.center, bounds.size);
        }

        m_visibleChunkBounds.Add(bounds);
        m_visibleChunkBoundsHashSet.Add(bounds);
      }
    }
  }

  public IEnumerator UpdateSpawnedChunksCoroutine() {
    while (true) {
      UpdateSpawnedChunks();
      yield return new WaitForSeconds(updateSpawnedChunksPeriod);
    }
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
      DetailsChunk chunk = m_spawnedChunks[i];
      // Find a chunk with the same position
      bool foundBounds = m_visibleChunkBoundsHashSet.Contains(chunk.bounds);

      if (!foundBounds) {
        m_spawnedChunks.Remove(chunk);
        m_spawnedChunksDictionary.Remove(chunk.bounds);
        Destroy(chunk.gameObject);
      }
    }

    // Debug.Log(m_spawnedChunks.Count);
  }

  private void CreateChunk(Bounds bounds) {
    // Create empty GameObject
    GameObject gameObject = new GameObject(string.Format(
      "{0}, {1}", bounds.center.x, bounds.center.z
    ));

    // Set position and parent
    gameObject.transform.position = bounds.center - bounds.extents;
    gameObject.transform.SetParent(detailsParent);

    // Create chunk component
    DetailsChunk chunk = gameObject.AddComponent<DetailsChunk>();

    // Add to the list
    m_spawnedChunks.Add(chunk);
    m_spawnedChunksDictionary.Add(bounds, chunk);

    // Set variables
    chunk.bounds = bounds;
    chunk.terrainShape = terrainShape;
  }

  public float GetLevelOfDetail(Vector3 cameraPosition, Vector3 chunkCenter) {
    cameraPosition.y = 0;
    float distanceToCamera = Vector3.Distance(cameraPosition, chunkCenter);
    float normalizedDistanceToCamera = Mathf.Clamp01(distanceToCamera / viewDistance);

    // float levelOfDetail = 1 - Mathf.Exp(-3f * (1f - normalizedDistanceToCamera));
    // if (levelOfDetail > 0.9f)
    //   levelOfDetail = 1f;

    return levelOfDetailCurve.Evaluate(normalizedDistanceToCamera);
  }

  private void OnDrawGizmos() {
    if (drawGizmos) {
      Camera camera = m_terrainManager.usedCamera;
      if (camera) {
        Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
        Gizmos.DrawSphere(camera.transform.position, viewDistance);
      }

      UpdateVisibleChunks(true);
    }
  }
}
