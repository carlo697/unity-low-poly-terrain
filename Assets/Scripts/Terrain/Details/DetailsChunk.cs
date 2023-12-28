
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

  public bool logGenerationInfo;

  public DetailsChunkStatus status { get { return m_status; } }
  private DetailsChunkStatus m_status = DetailsChunkStatus.Spawned;

  private int m_integerLevelOfDetail;
  private float m_normalizedLevelOfDetail;

  public List<DetailInstance> instances { get { return m_instances; } }
  private List<DetailInstance> m_instances = new();

  private bool m_updateFlag;
  private bool m_destroyFlag;
  private JobHandle? m_handle;
  private NativeArray<RaycastHit> m_results;
  private NativeArray<RaycastCommand> m_commands;

  private Dictionary<DetailSubmesh[], DetailsInstancingBatch> m_instancingBatches = new();
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
      if (m_status != DetailsChunkStatus.Generating) {
        Destroy(gameObject);
      }
    } else {
      if (m_updateFlag && m_status != DetailsChunkStatus.Generating && updatesThisFrame < 2) {
        updatesThisFrame++;
        m_updateFlag = false;
        m_status = DetailsChunkStatus.Generating;
        StartCoroutine(PlaceDetails());
      }
    }
  }

  private void LateUpdate() {
    if (manager.renderMode == DetailsRenderMode.InstancingFromChunk && !manager.skipInstancingRendering) {
      Render();
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
    DestroyInstances(false);

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

        DetailMeshSet meshSet = detail.meshes[instance.meshIndex];
        DetailSubmesh[] submeshes = meshSet.levelOfDetails[0].submeshes;
        if (submeshes.Length > 0) {
          // Get the batch or create it if it doesn't exist
          DetailsInstancingBatch batch;
          if (!m_instancingBatches.TryGetValue(submeshes, out batch)) {
            batch = m_instancingBatches[submeshes] = new DetailsInstancingBatch(submeshes, bounds);
          }

          // Add the matrix
          batch.matrices.Add(instance.matrix);
        }
      }
    } else if (manager.renderMode == DetailsRenderMode.GameObjects) {
      // Instantiate new game objects
      for (int i = 0; i < m_instances.Count; i++) {
        DetailInstance instance = m_instances[i];

        Detail detail = manager.detailsById[instance.detailId];
        GameObject obj = PrefabPool.Get(detail.prefabs[instance.meshIndex]);
        obj.transform.SetPositionAndRotation(instance.position, instance.rotation);
        obj.transform.localScale = instance.scale;
        // obj.transform.SetParent(transform, false);
        obj.SetActive(true);
        m_instancedGameObjects.Add(obj);
      }
    }

    if (manager.renderMode == DetailsRenderMode.InstancingFromChunk) {
      // Create buffers for GPU instancing
      foreach (var item in m_instancingBatches) {
        DetailsInstancingBatch batch = item.Value;
        batch.UploadBuffers();
      }
    }

    m_status = DetailsChunkStatus.Generated;

    timer.Stop();

    if (logGenerationInfo) {
      Debug.Log(
        $"{timer.ElapsedMilliseconds} ms ({timer.ElapsedTicks} ticks) to generate {m_instances.Count} details"
      );
    }
  }

  private void DestroyInstances(bool destroy) {
    if (manager.renderMode == DetailsRenderMode.GameObjects) {
      // Delete the spawned game objects
      for (int i = 0; i < m_instancedGameObjects.Count; i++) {
        PrefabPool.Release(m_instancedGameObjects[i]);
      }

      m_instancedGameObjects.Clear();
    } else if (manager.renderMode == DetailsRenderMode.InstancingFromChunk) {
      // Clear batches for GPU instancing
      foreach (var item in m_instancingBatches) {
        DetailsInstancingBatch batch = item.Value;
        if (destroy) {
          batch.Destroy();
        } else {
          batch.Clear();
        }
      }
    }

    // Clear array
    m_instances.Clear();
  }

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

    DestroyInstances(true);
  }

  private void Render() {
    // Iterate the batches
    foreach (var item in m_instancingBatches) {
      item.Value.Render();
    }
  }
}
