
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
  private List<DetailInstance> m_instances;

  private bool m_updateFlag;
  private bool m_destroyFlag;

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
    if (integerLevelOfDetail != m_integerLevelOfDetail || m_instances == null || m_instances.Count == 0) {
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

    // Delete the old game objects
    DestroyInstances(false);

    // Generate a seed for this chunk
    ulong seed = (ulong)(terrainShape.terrainSeed + bounds.center.GetHashCode());

    if (m_instances == null) {
      m_instances = new(128);
    }

    // Iterate spawners to add the instances
    for (int i = 0; i < terrainShape.detailSpawners.Length; i++) {
      DetailSpawner spawner = terrainShape.detailSpawners[i];
      spawner.Spawn(m_instances, seed, bounds, integerLevelOfDetail, normalizedLevelOfDetail);
    }

    // if (bounds.center.x == -304f && bounds.center.z == -112f) {
    //   Debug.LogFormat("instances: {0}", m_instances.Count);
    //   Debug.Break();
    // }

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

    if (logGenerationInfo && m_instances.Count > 50) {
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
    if (m_instances != null) {
      m_instances.Clear();
    }
  }

  private void OnDrawGizmosSelected() {
    Gizmos.color = Color.white;
    Gizmos.DrawWireCube(bounds.center, bounds.size);
  }

  public void ScheduleDestroy() {
    m_destroyFlag = true;
  }

  private void OnDestroy() {
    DestroyInstances(true);
  }

  private void Render() {
    // Iterate the batches
    foreach (var item in m_instancingBatches) {
      item.Value.Render();
    }
  }
}
