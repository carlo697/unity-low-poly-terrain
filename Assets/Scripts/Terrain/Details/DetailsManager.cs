using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DetailsRenderMode {
  Disable,
  InstancingFromManager,
  InstancingFromChunk,
  GameObjects
}

public class DetailsManager : MonoBehaviour {
  public QuadTreeTerrainManager manager { get { return m_terrainManager; } }
  [SerializeField] private QuadTreeTerrainManager m_terrainManager;

  public TerrainShape terrainShape { get { return m_terrainManager.terrainShape; } }

  [Header("Chunk Distribution")]
  public float updateVisibleChunksPeriod = 0.5f;
  public float updateSpawnedChunksPeriod = 0.2f;
  public float viewDistance = 500f;
  public AnimationCurve levelOfDetailCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

  public DetailsRenderMode renderMode { get { return m_renderMode; } }
  [Header("Instancing")]
  [SerializeField] private DetailsRenderMode m_renderMode = DetailsRenderMode.InstancingFromManager;

  public Dictionary<int, Detail> detailsById { get { return m_detailsById; } }
  private Dictionary<int, Detail> m_detailsById = new();

  private List<DetailsChunk> m_spawnedChunks = new();
  private Dictionary<Bounds, DetailsChunk> m_spawnedChunksDictionary = new();

  private List<Bounds> m_visibleChunkBounds = new();
  private HashSet<Bounds> m_visibleChunkBoundsHashSet = new();

  public Transform chunksParent { get { return m_chunksParent; } }
  [SerializeField] private Transform m_chunksParent;

  [Header("Debug")]
  public bool drawGizmos;
  public bool debugGpuInstancing;
  public bool debugSkipGpuInstancing;

  private void Start() {
    if (terrainShape.useDetails) {
      InitializeDetailsDatabase();

      // Start coroutine to schedule the updates of the details
      StartCoroutine(UpdateCoroutine());

      // Register events in the terrain manager
      m_terrainManager.ChunkGenerated += ChunkGeneratedEventHandler;
      m_terrainManager.ChunkReplaced += ChunkReplacedEventHandler;

      // Allocate prefabs in the pool
      if (m_renderMode == DetailsRenderMode.InstancingFromManager) {
        // Initialize the grid used by instancing
        InitializeInstancingGrid();
      }
    }
  }

  private void InitializeDetailsDatabase() {
    for (int i = 0; i < terrainShape.detailSpawners.Length; i++) {
      DetailSpawner spawner = terrainShape.detailSpawners[i];
      m_detailsById[spawner.detail.id] = spawner.detail;
    }
  }

  private void OnDestroy() {
    // Unregister events
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
        (int integer, float normalized) = GetLevelOfDetail(cameraPosition, chunk.bounds.center);
        chunk.RequestUpdate(integer, normalized);
      }
    }
  }

  public IEnumerator UpdateCoroutine() {
    while (true) {
      // Update the list visible chunks
      UpdateVisibleChunks();
      yield return new WaitForSeconds(updateVisibleChunksPeriod);

      // Spawn/despawn chunks
      UpdateSpawnedChunks();
      yield return new WaitForSeconds(updateSpawnedChunksPeriod);

      // Update the lists needed to use GPU instancing
      yield return StartCoroutine(PrepareMeshInstancing());
      yield return new WaitForSeconds(updateVisibleChunksPeriod / 2f);
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
          Gizmos.color = Color.white;
          Gizmos.DrawCube(bounds.center, bounds.size);
        }

        m_visibleChunkBounds.Add(bounds);
        m_visibleChunkBoundsHashSet.Add(bounds);
      }
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
        chunk.ScheduleDestroy();
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
    gameObject.transform.SetParent(m_chunksParent);

    // Create chunk component
    DetailsChunk chunk = gameObject.AddComponent<DetailsChunk>();

    // Add to the list
    m_spawnedChunks.Add(chunk);
    m_spawnedChunksDictionary.Add(bounds, chunk);

    // Set variables
    chunk.manager = this;
    chunk.bounds = bounds;
    chunk.terrainShape = terrainShape;

    // Request update
    (int integer, float normalized) = GetLevelOfDetail(
      m_terrainManager.usedCamera.transform.position,
      chunk.bounds.center
    );
    chunk.RequestUpdate(integer, normalized);
  }

  public (int, float) GetLevelOfDetail(Vector3 cameraPosition, Vector3 chunkCenter) {
    TerrainChunk terrainChunk = m_terrainManager.GetChunkAt(chunkCenter);

    if (terrainChunk) {
      // Calculate a level of detail integer between 1 and the maximun level
      int integer = 1 + (int)Mathf.Log(
        terrainChunk.size.x / m_terrainManager.chunkSize.x,
        2
      );
      // Calculate the a normalized level of detail (between 0 and 1)
      float normalized = levelOfDetailCurve.Evaluate(1f / integer);
      return (integer, normalized);
    }

    return (0, 0f);
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

  #region GPU INSTANCING

  private class InstancingCell {
    public Dictionary<DetailSubmesh, List<List<Matrix4x4>>> groups = new();
    public Dictionary<DetailSubmesh, int> groupsBatchIndex = new();
  }

  // We use two SimpleGrid2 because we update the instancing batches during
  // several frames and while doing that we need to have an unaltered backup
  // of the grid to keep calling DrawMeshInstanced with it
  private SimpleGrid2<InstancingCell> m_instancingGridCopy;
  private SimpleGrid2<InstancingCell> m_instancingGrid;

  private Vector2 GetCenterOfInstancingGrid() {
    return new Vector2(
      m_terrainManager.usedCamera.transform.position.x,
      m_terrainManager.usedCamera.transform.position.z
    );
  }

  private void InitializeInstancingGrid() {
    // Get the center of the grid
    Vector2 gridCenter = GetCenterOfInstancingGrid();

    // Build grid
    float gridWidth = (viewDistance + m_terrainManager.chunkSize.x) * 2.5f;
    Vector2 gridSize = new Vector2(gridWidth, gridWidth);
    Vector2Int gridResolution = new Vector2Int(32, 32);

    // Create the two grids
    m_instancingGridCopy = new SimpleGrid2<InstancingCell>(
      gridCenter,
      gridSize,
      gridResolution
    );
    m_instancingGrid = new SimpleGrid2<InstancingCell>(
      gridCenter,
      gridSize,
      gridResolution
    );
  }

  private IEnumerator PrepareMeshInstancing() {
    yield return null;

    if (m_renderMode != DetailsRenderMode.InstancingFromManager) {
      yield break;
    }

    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    // Swap the two grids because we are about to edit the copy
    SimpleGrid2<InstancingCell> gridA = m_instancingGridCopy;
    m_instancingGridCopy = m_instancingGrid;
    m_instancingGrid = gridA;

    // Update the center of the grid
    m_instancingGridCopy.center = GetCenterOfInstancingGrid();

    // Clear the groups and batches on the grid
    for (int i = 0; i < m_instancingGridCopy.cells.Length; i++) {
      InstancingCell cell = m_instancingGridCopy.cells[i];

      cell.groupsBatchIndex.Clear();
      foreach (var batch in cell.groups) {
        foreach (var lists in batch.Value) {
          lists.Clear();
        }
      }
    }

    // Iterate the spawned chunks
    int instancePauseCount = 0;
    int totalFrames = 1;
    int totalInstanceCount = 0;
    for (int i = 0; i < m_spawnedChunks.Count; i++) {
      DetailsChunk chunk = m_spawnedChunks[i];

      if (chunk.instances.Count == 0) {
        continue;
      }

      // Iterate the instances in the chunk
      for (int j = 0; j < chunk.instances.Count; j++) {
        DetailInstance instance = chunk.instances[j];
        Detail detail = m_detailsById[instance.detailId];

        // Get grid cell
        InstancingCell cell = m_instancingGridCopy.GetCellAt(
          instance.position.x, instance.position.z
        );

        // Ignore the instance if it doesn't have submeshes
        if (detail.submeshes.Length > 0) {
          // Keep track of the number of instances so we can pause and
          // keep updating the next frame
          instancePauseCount += detail.submeshes.Length;
          if (instancePauseCount > 4000) {
            timer.Stop();
            yield return new WaitForSeconds(0.01f);
            timer.Start();
            instancePauseCount = 0;
            totalFrames++;
          }

          // Iterate the submeshes
          for (int k = 0; k < detail.submeshes.Length; k++) {
            DetailSubmesh submesh = detail.submeshes[k];

            // Get the list of lists or create it if necessary
            List<List<Matrix4x4>> lists;
            if (!cell.groups.TryGetValue(submesh, out lists)) {
              lists = cell.groups[submesh] = new List<List<Matrix4x4>>(5);
            }

            // Get the index of the current list or create it if necessary
            int currentIndex;
            if (!cell.groupsBatchIndex.TryGetValue(submesh, out currentIndex)) {
              currentIndex = cell.groupsBatchIndex[submesh] = 0;
            }

            // Create the list given by the index if it doesn't exist
            if (lists.Count - 1 < currentIndex) {
              lists.Add(new List<Matrix4x4>(1023));
            }

            // Get the current list
            List<Matrix4x4> currentList = lists[currentIndex];

            // Add the matrix
            currentList.Add(instance.matrix);
            totalInstanceCount++;

            // Increase the index when the limit of 1023 is passed
            if (currentList.Count >= 1023) {
              cell.groupsBatchIndex[submesh]++;
            }
          }
        }
      }
    }

    if (debugGpuInstancing) {
      Debug.Log(
        string.Format(
          "Time: {0} ms to prepare {1} instances for GPU instancing in {2} frames",
          timer.ElapsedMilliseconds,
          totalInstanceCount,
          totalFrames
        )
      );
    }
  }

  private void Update() {
    // We'll call DrawMeshInstanced using the grid build by the
    // PrepareMeshInstancing coroutine
    if (
      terrainShape.useDetails
      && m_renderMode == DetailsRenderMode.InstancingFromManager
      && !debugSkipGpuInstancing
    ) {
      // Iterate the cells of the grid
      for (int i = 0; i < m_instancingGrid.cells.Length; i++) {
        InstancingCell cell = m_instancingGrid.cells[i];

        // Iterate the groups of details
        foreach (var batch in cell.groups) {
          // Iterate the batches
          foreach (List<Matrix4x4> list in batch.Value) {
            if (list.Count > 0) {
              Graphics.DrawMeshInstanced(
                batch.Key.mesh,
                batch.Key.submeshIndex,
                batch.Key.material,
                list
              );
            }
          }
        }
      }
    }
  }

  #endregion
}
