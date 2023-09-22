
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System.Collections;
using System.Collections.Generic;

public enum DetailsChunkStatus {
  Spawned,
  Generating,
  Generated
}

public class DetailsChunk : MonoBehaviour {
  private static int updatesThisFrame;
  private static int lastFrameCount;

  public DetailsManager manager;
  public Bounds bounds;
  public TerrainShape terrainShape;

  public DetailsChunkStatus status = DetailsChunkStatus.Spawned;

  private int m_integerLevelOfDetail;
  private float m_normalizedLevelOfDetail;

  public List<DetailInstance> instances { get { return m_instances; } }
  private List<DetailInstance> m_instances = new List<DetailInstance>();

  private bool m_updateFlag;
  private bool m_destroyFlag;
  private JobHandle? m_handle;
  private NativeArray<RaycastHit> m_results;
  private NativeArray<RaycastCommand> m_commands;

  private Dictionary<DetailSubmesh, List<Matrix4x4>> m_instancingBatches = new();
  private List<GameObject> m_instancedGameObjects;

  private void Start() {
    if (manager.renderMode == DetailsRenderMode.GameObjects) {
      m_instancedGameObjects = new();
    }
  }

  private void Update() {
    if (lastFrameCount != Time.frameCount) {
      updatesThisFrame = 0;
      lastFrameCount = Time.frameCount;
    }

    if (m_destroyFlag) {
      if (status != DetailsChunkStatus.Generating) {
        Destroy(gameObject);
      }
    } else {
      if (m_updateFlag && status != DetailsChunkStatus.Generating && updatesThisFrame < 2) {
        updatesThisFrame++;
        m_updateFlag = false;
        status = DetailsChunkStatus.Generating;
        StartCoroutine(PlaceDetails());
      }

      if (manager.renderMode == DetailsRenderMode.InstancingFromChunk && !manager.debugSkipGpuInstancing) {
        Render();
      }
    }
  }

  public void RequestUpdate(int integerLevelOfDetail, float normalizedLevelOfDetail) {
    // Only update if the level of detail changed or if the chunk
    // has 0 detail instances
    if (integerLevelOfDetail != m_integerLevelOfDetail || m_instances.Count == 0) {
      m_updateFlag = true;
      m_integerLevelOfDetail = integerLevelOfDetail;
      m_normalizedLevelOfDetail = normalizedLevelOfDetail;
    }
  }

  public IEnumerator PlaceDetails() {
    // Let's copy the level of details in case they are updated while the chunk
    // is still generating
    int integerLevelOfDetail = m_integerLevelOfDetail;
    float normalizedLevelOfDetail = m_normalizedLevelOfDetail;

    yield return null;

    // Record time taken to generate and spawn the details
    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    // Generate a seed for this chunk
    ulong seed = (ulong)(terrainShape.terrainSeed + bounds.center.GetHashCode());

    // Create a list of temporal instances using the spawners
    List<TempDetailInstance> tempInstances = new List<TempDetailInstance>(1000);
    for (int i = 0; i < terrainShape.detailSpawners.Length; i++) {
      DetailSpawner spawner = terrainShape.detailSpawners[i];

      // Call the "Spawn" method to add the temporal instances to the list
      spawner.Spawn(tempInstances, seed, bounds, integerLevelOfDetail, normalizedLevelOfDetail);
    }

    // if (bounds.center.x == -304f && bounds.center.z == -112f) {
    //   Debug.LogFormat("{0}, {1}, {2}", m_levelOfDetail, seed, tempInstances.Count);
    // }

    // Create the arrays needed to schedule the job for the raycasts
    m_results = new NativeArray<RaycastHit>(tempInstances.Count, Allocator.Persistent);
    m_commands = new NativeArray<RaycastCommand>(tempInstances.Count, Allocator.Persistent);

    QueryParameters parameters = QueryParameters.Default;
    parameters.hitBackfaces = true;

    // Transfer the raycast commands from the temporal instances to the "commands" array
    for (int i = 0; i < tempInstances.Count; i++) {
      m_commands[i] = tempInstances[i].raycastCommand;
    }

    // if (bounds.center.x == -304f && bounds.center.z == -112f) {
    //   Debug.LogFormat("instances: {0}", m_instances.Count);
    //   Debug.Break();
    // }

    // Schedule the raycast commands
    m_handle = RaycastCommand.ScheduleBatch(
      m_commands,
      m_results,
      1000,
      1
    );

    timer.Stop();

    // Wait for the job to be completed
    while (!m_handle.Value.IsCompleted) {
      yield return new WaitForSeconds(.1f);
    }

    timer.Start();

    // Complete the job
    m_handle.Value.Complete();

    // Delete the old game objects
    DestroyInstances();

    // Get the final instances and register them
    for (int i = 0; i < m_results.Length; i++) {
      if (m_results[i].collider != null) {
        if (tempInstances[i].GetFinalInstance(m_results[i], out DetailInstance instance))
          m_instances.Add(instance);
      }
    }

    // if (bounds.center.x == -304f && bounds.center.z == -112f) {
    //   Debug.LogFormat("instances: {0}", m_instances.Count);
    //   Debug.Break();
    // }

    // Dispose the job
    DisposeJob();

    if (manager.renderMode == DetailsRenderMode.InstancingFromChunk) {
      // Add GPU instancing batches
      for (int i = 0; i < m_instances.Count; i++) {
        DetailInstance instance = m_instances[i];
        Detail detail = manager.detailsById[instance.detailId];

        if (detail.submeshes.Length > 0) {
          for (int j = 0; j < detail.submeshes.Length; j++) {
            DetailSubmesh batch = detail.submeshes[j];

            // Get the list or create it if it doesn't exist
            List<Matrix4x4> matrices;
            if (!m_instancingBatches.TryGetValue(batch, out matrices)) {
              matrices = m_instancingBatches[batch] = new();
            }

            // Add the matrix
            matrices.Add(instance.matrix);
          }
        }
      }
    } else if (manager.renderMode == DetailsRenderMode.GameObjects) {
      // Instantiate new game objects
      for (int i = 0; i < m_instances.Count; i++) {
        DetailInstance instance = m_instances[i];

        Detail detail = manager.detailsById[instance.detailId];
        GameObject obj = PrefabPool.Get(detail.prefabs[instance.prefabIndex]);
        obj.transform.SetPositionAndRotation(instance.position, instance.rotation);
        obj.transform.localScale = instance.scale;
        // obj.transform.SetParent(transform, false);
        obj.SetActive(true);
        m_instancedGameObjects.Add(obj);
      }
    }

    timer.Stop();

    // Debug.Log(
    //   string.Format(
    //     "Time: {0} ms, instances: {1}",
    //     timer.ElapsedMilliseconds,
    //     m_instances.Count
    //   )
    // );

    status = DetailsChunkStatus.Generated;
  }

  private void DestroyInstances() {
    if (manager.renderMode == DetailsRenderMode.GameObjects) {
      // Delete the spawned game objects
      for (int i = 0; i < m_instancedGameObjects.Count; i++) {
        PrefabPool.Release(m_instancedGameObjects[i]);
      }

      m_instancedGameObjects.Clear();
    } else if (manager.renderMode == DetailsRenderMode.InstancingFromChunk) {
      // Clear batches for GPU instancing
      foreach (var batch in m_instancingBatches) {
        batch.Value.Clear();
      }
    }

    // Clear array
    m_instances.Clear();
  }

  public static Vector3 RandomPointInBounds(Bounds bounds) {
    return new Vector3(
      UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
      bounds.max.y,
      UnityEngine.Random.Range(bounds.min.z, bounds.max.z)
    );
  }

  [ContextMenu("Test Raycasts")]
  public void TestRaycast() {
    // Check if it has a mesh collider
    MeshCollider collider = GetComponent<MeshCollider>();

    if (collider) {
      // Start recording time
      System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
      timer.Start();

      Bounds bounds = collider.bounds;
      RaycastHit[] hits = new RaycastHit[10];
      float distance = collider.bounds.size.y;
      int totalHits = 0;

      if (collider) {
        for (int i = 0; i < 20000; i++) {
          Ray ray = new Ray(RandomPointInBounds(bounds), Vector3.down);
          int hitCount = Physics.RaycastNonAlloc(ray, hits, distance);

          for (int j = 0; j < hitCount; j++) {
            RaycastHit hit = hits[j];
            totalHits++;
          }
        }
      }

      // Stop recording time
      timer.Stop();
      Debug.LogFormat("{0} ms", timer.ElapsedMilliseconds);
      Debug.LogFormat("Total hits: {0}", totalHits);
    }
  }

  [ContextMenu("Test Raycasts With Job")]
  public void TestRaycastWithJob() {
    // Check if it has a mesh collider
    MeshCollider collider = GetComponent<MeshCollider>();

    if (collider) {
      // Start recording time
      System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
      timer.Start();

      // Create the arrays needed to schedule the job
      int maxHits = 5;
      int rayCount = 20000;
      var results = new NativeArray<RaycastHit>(rayCount * maxHits, Allocator.TempJob);
      var commands = new NativeArray<RaycastCommand>(rayCount, Allocator.TempJob);

      QueryParameters parameters = QueryParameters.Default;
      parameters.hitBackfaces = true;

      // Create the raycast commands
      Bounds bounds = collider.bounds;
      for (int i = 0; i < rayCount; i++) {
        commands[i] = new RaycastCommand(
          RandomPointInBounds(bounds),
          Vector3.down,
          parameters
        );
      }

      // Schedule the raycasts and complete the job
      JobHandle handle = RaycastCommand.ScheduleBatch(
        commands,
        results,
        1000,
        maxHits
      );

      handle.Complete();

      // Read the results
      int totalHits = 0;
      for (int i = 0; i < results.Length; i++) {
        if (results[i].collider != null) {
          totalHits++;
        }
      }

      // Dispose the buffers
      results.Dispose();
      commands.Dispose();

      // Stop recording time
      timer.Stop();
      Debug.LogFormat("{0} ms", timer.ElapsedMilliseconds);
      Debug.LogFormat("Total hits: {0}", totalHits);
    }
  }

  // [ContextMenu("Test Looping Points")]
  // public void TestLoopingPoints() {
  //   // Start recording time
  //   System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
  //   timer.Start();

  //   float total = 0;
  //   for (int i = 0; i < 20; i++) {
  //     for (int j = 0; j < m_chunk.points.Length; j++) {
  //       CubeGridPoint point = m_chunk.points[j];
  //       total += point.value;
  //     }
  //   }

  //   // Stop recording time
  //   timer.Stop();
  //   Debug.LogFormat("{0} ms", timer.ElapsedMilliseconds);
  //   Debug.LogFormat("Total value: {0}", total);
  // }

  private void OnDrawGizmosSelected() {
    Gizmos.color = Color.white;
    Gizmos.DrawWireCube(bounds.center, bounds.size);
  }

  public void ScheduleDestroy() {
    m_destroyFlag = true;
  }

  private void DisposeJob() {
    m_results.Dispose();
    m_commands.Dispose();
    m_handle = null;
  }

  private void CancelJob() {
    m_handle.Value.Complete();
    DisposeJob();
  }

  private void OnDestroy() {
    if (m_handle.HasValue) {
      Debug.Log("Details Chunk destroyed and there was a job running");
      CancelJob();
    }

    DestroyInstances();
  }

  private void Render() {
    // Iterate the batches to draw them
    foreach (var batch in m_instancingBatches) {
      if (batch.Value.Count > 0) {
        Graphics.DrawMeshInstanced(
          batch.Key.mesh,
          batch.Key.submeshIndex,
          batch.Key.material,
          batch.Value
        );
      }
    }
  }
}
