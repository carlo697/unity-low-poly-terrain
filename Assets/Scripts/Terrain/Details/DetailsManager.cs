using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;

public enum DetailsRenderMode {
  Disable,
  InstancingFromManager,
  InstancingFromChunk,
  GameObjects
}

public class DetailsManager : MonoBehaviour {
  public TerrainChunkManager manager { get { return m_terrainManager; } }
  [SerializeField] private TerrainChunkManager m_terrainManager;

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
  public bool logGenerationInfo;
  public bool logGpuInstancingInfo;
  public bool skipInstancingPreparation;
  public KeyCode keyToToggleInstancingPreparation = KeyCode.None;
  public bool skipInstancingRendering;

  private void Start() {
    if (terrainShape.useDetails) {
      InitializeDetailsDatabase();

      // Start coroutine to schedule the updates of the details
      StartCoroutine(UpdateCoroutine());

      // Register events in the terrain manager
      m_terrainManager.ChunkGenerated += ChunkGeneratedEventHandler;
      m_terrainManager.ChunkReplaced += ChunkReplacedEventHandler;
    }
  }

  private void InitializeDetailsDatabase() {
    for (int i = 0; i < terrainShape.detailSpawners.Length; i++) {
      // Fill up the dictionary of details
      DetailSpawner spawner = terrainShape.detailSpawners[i];
      m_detailsById[spawner.detail.id] = spawner.detail;

      // Instantiate the batch classes
      DetailMeshSet[] meshSets = spawner.detail.meshes;
      foreach (DetailMeshSet meshSet in meshSets) {
        foreach (DetailMeshWithLOD levelOfDetail in meshSet.levelOfDetails) {
          DetailSubmesh[] submeshes = levelOfDetail.submeshes;

          m_instancingBatches[submeshes] = new DetailsInstancingBatch(
            submeshes,
            new Bounds(),
            DetailsBatchRenderMode.Solid,
            true
          );

          m_instancingShadowBatches[submeshes] = new DetailsInstancingBatch(
            submeshes,
            new Bounds(),
            DetailsBatchRenderMode.Shadows,
            true
          );
        }
      }
    }
  }

  private void OnDestroy() {
    // Unregister events
    m_terrainManager.ChunkGenerated -= ChunkGeneratedEventHandler;
    m_terrainManager.ChunkReplaced -= ChunkReplacedEventHandler;

    // Release the buffers used by GPU instancing
    foreach (var item in m_instancingBatches) {
      item.Value.Destroy();
    }
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

  private void Update() {
    if (!terrainShape.useDetails) {
      return;
    }

    if (keyToToggleInstancingPreparation != KeyCode.None
      && Input.GetKeyDown(keyToToggleInstancingPreparation)
    ) {
      skipInstancingPreparation = !skipInstancingPreparation;
    }

    // Update the lists needed to use GPU instancing
    if (!skipInstancingPreparation
      && m_renderMode == DetailsRenderMode.InstancingFromManager
    ) {
      PrepareMeshInstancing();
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
    }
  }

  private void UpdateVisibleChunks(bool drawGizmos = false) {
    if (m_terrainManager.usedCamera == null) return;
    Vector3 camera3dPosition = m_terrainManager.usedCamera.transform.position;
    Vector2 camera2dPosition = new Vector2(camera3dPosition.x, camera3dPosition.z);

    Vector3 chunk3dScale = m_terrainManager.chunkScale;
    Vector3 chunk3dExtents = chunk3dScale / 2f;

    Vector2 chunk2dScale = new Vector2(
      m_terrainManager.chunkScale.x,
      m_terrainManager.chunkScale.z
    );
    Vector2 chunk2dExtents = chunk2dScale / 2f;

    // Get the area the player is standing right now
    Vector2Int currentChunk2dCoords = new Vector2Int(
      Mathf.FloorToInt(camera2dPosition.x / chunk2dScale.x),
      Mathf.FloorToInt(camera2dPosition.y / chunk2dScale.y)
    );

    int visibleX = Mathf.CeilToInt(viewDistance / chunk2dScale.x);
    int visibleY = Mathf.CeilToInt(viewDistance / chunk2dScale.y);

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
        Vector2 position2d = Vector2.Scale(coords2d, chunk2dScale) + chunk2dExtents;
        Vector3 position3d = new Vector3(position2d.x, m_terrainManager.seaWorldLevel, position2d.y);
        Bounds bounds = new Bounds(position3d, chunk3dScale);

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
    GameObject gameObject = new GameObject($"{bounds.center.x}, {bounds.center.z}");

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
    chunk.logGenerationInfo = logGenerationInfo;

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
        terrainChunk.scale.x / m_terrainManager.chunkScale.x,
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

  private Dictionary<DetailSubmesh[], DetailsInstancingBatch> m_instancingBatches = new();
  private Dictionary<DetailSubmesh[], DetailsInstancingBatch> m_instancingShadowBatches = new();

  private void PrepareMeshInstancing() {
    // Measure the total time
    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    // Clear the groups and batches on the grid
    foreach (var item in m_instancingBatches) {
      item.Value.Clear();
    }
    foreach (var item in m_instancingShadowBatches) {
      item.Value.Clear();
    }

    Camera camera = Camera.main;
    Vector3 cameraPosition = camera.transform.position;
    Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(camera);

    // Variables for the jobs
    NativeArray<Plane> nativeCameraPlanes = new NativeArray<Plane>(cameraPlanes, Allocator.TempJob);
    GCHandle details = GCHandle.Alloc(m_detailsById);
    GCHandle chunks = GCHandle.Alloc(m_spawnedChunks);
    GCHandle instancingBatches = GCHandle.Alloc(m_instancingBatches);
    GCHandle instancingShadowBatches = GCHandle.Alloc(m_instancingShadowBatches);

    DetailsInstancingJob job = new DetailsInstancingJob {
      shadowDistance = QualitySettings.shadowDistance,
      maxDistance = viewDistance,
      cameraPosition = cameraPosition,
      cameraPlanes = nativeCameraPlanes,
      details = details,
      chunks = chunks,
      instancingBatches = instancingBatches,
      instancingShadowBatches = instancingShadowBatches
    };
    JobHandle handle = job.ScheduleBatch(m_spawnedChunks.Count, 300);

    // Complete the job
    handle.Complete();

    // Deallocate
    nativeCameraPlanes.Dispose();
    details.Free();
    chunks.Free();
    instancingBatches.Free();
    instancingShadowBatches.Free();

    // foreach (var item in m_instancingBatches) {
    //   item.Value.isConcurrent = false;
    // }
    // foreach (var item in m_instancingShadowBatches) {
    //   item.Value.isConcurrent = false;
    // }

    // float shadowDistance = QualitySettings.shadowDistance;

    // // Iterate the spawned chunks
    // for (int i = 0; i < m_spawnedChunks.Count; i++) {
    //   DetailsChunk chunk = m_spawnedChunks[i];

    //   // Skip if the chunk don't have instances
    //   if (chunk.instances.Count == 0) {
    //     continue;
    //   }

    //   // Skip if the chunk is not visible
    //   bool isChunkVisible = chunk.bounds.Intersects(cameraPlanes);
    //   Vector3 nearestPoint = chunk.bounds.ClosestPoint(cameraPosition);
    //   bool isChunkShadowVisible = Vector3.Distance(nearestPoint, cameraPosition) < shadowDistance;
    //   if (!isChunkVisible && !isChunkShadowVisible) {
    //     continue;
    //   }

    //   for (int j = 0; j < chunk.instances.Count; j++) {
    //     DetailInstance instance = chunk.instances[j];
    //     Detail detail = m_detailsById[instance.detailId];

    //     // Add the matrix for the solid pass
    //     if (isChunkVisible) {
    //       bool isVisible = instance.sphereBounds.IntersectsExcludingFarPlane(cameraPlanes);
    //       if (isVisible) {
    //         DetailsInstancingBatch batch = m_instancingBatches[detail.submeshes];
    //         batch.matrixList.Add(instance.matrix);
    //       }
    //     }

    //     // Add the matrix for the shadow pass
    //     if (isChunkShadowVisible) {
    //       bool isShadowVisible =
    //         Vector3.Distance(instance.sphereBounds.center, cameraPosition) + instance.sphereBounds.radius < shadowDistance;
    //       if (isShadowVisible) {
    //         DetailsInstancingBatch batch = m_instancingShadowBatches[detail.submeshes];
    //         if (batch.hasShadows) {
    //           batch.matrixList.Add(instance.matrix);
    //         }
    //       }
    //     }
    //   }
    // }

    // Count the instances
    int totalInstanceCount = 0;
    int totalShadowInstanceCount = 0;
    foreach (var item in m_instancingBatches) {
      totalInstanceCount += item.Value.matrices.Count * item.Value.submeshCount;
    }
    foreach (var item in m_instancingShadowBatches) {
      totalShadowInstanceCount += item.Value.matrices.Count * item.Value.submeshCount;
    }

    // Set bounds
    Bounds bounds = new Bounds(cameraPosition, Vector3.one * 1000f);
    foreach (var item in m_instancingBatches) {
      item.Value.bounds = bounds;
    }
    foreach (var item in m_instancingShadowBatches) {
      item.Value.bounds = bounds;
    }

    // Upload the buffers to the GPU
    System.Diagnostics.Stopwatch buffersTimer = new System.Diagnostics.Stopwatch();
    buffersTimer.Start();
    foreach (var item in m_instancingBatches) {
      item.Value.UploadBuffers();
    }
    foreach (var item in m_instancingShadowBatches) {
      item.Value.UploadBuffers();
    }
    buffersTimer.Stop();

    // Stop measuring the total time
    timer.Stop();

    // Log the times to the console
    if (logGpuInstancingInfo) {
      Debug.Log(
        $"{buffersTimer.ElapsedMilliseconds} ms to create buffers in GPU by coping instances from CPU"
      );

      Debug.Log(
        $"{timer.ElapsedMilliseconds} ms ({timer.ElapsedTicks} ticks) to prepare {totalInstanceCount} instances and {totalShadowInstanceCount} shadow instances instances (GPU instancing)"
      );
    }
  }

  private void LateUpdate() {
    if (
      terrainShape.useDetails
      && m_renderMode == DetailsRenderMode.InstancingFromManager
    ) {
      foreach (var item in m_instancingBatches) {
        if (!skipInstancingRendering) {
          item.Value.Render();
        }
      }

      foreach (var item in m_instancingShadowBatches) {
        if (!skipInstancingRendering) {
          item.Value.Render();
        }
      }
    }
  }

  #endregion
}
