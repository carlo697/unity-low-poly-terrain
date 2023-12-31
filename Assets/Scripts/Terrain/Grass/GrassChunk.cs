using Unity.Jobs;
using Unity.Collections;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public enum GrassChunkStatus {
  Spawned,
  Generating,
  Generated
}

[RequireComponent(typeof(TerrainChunk))]
public class GrassChunk : MonoBehaviour {
  public GrassManager manager;
  public float maxDistance = 100f;

  [Header("Debug")]
  public bool logGenerationInfo;
  public bool logInstancingInfo;
  public bool skipRendering;

  public TerrainShape terrainShape { get { return m_terrainChunk.terrainShape; } }

  public TerrainChunk terrainChunk { get { return m_terrainChunk; } }
  private TerrainChunk m_terrainChunk;

  public GrassChunkStatus status { get { return m_status; } }
  private GrassChunkStatus m_status = GrassChunkStatus.Spawned;

  private Camera m_camera;
  private Vector3 m_cameraPosition;

  private Dictionary<DetailSubmesh[], GrassInstancingBatch> m_groups = new();
  private Dictionary<DetailSubmesh[], GrassInstancingBatch> m_groupsCopy = new();

  private JobHandle? m_generationJobHandle;
  private NativeList<GrassInstance> m_nativeInstances;
  private GCHandle m_spawnersHandle;
  private Mesh.MeshDataArray m_meshDataArray;

  private JobHandle? m_instancingJobHandle;
  private GCHandle m_grassesHandle;
  private GCHandle m_groupsHandle;

  private MaterialPropertyBlock m_materialBlock;

  public float distanceToCamera {
    get {
      Vector3 closestPoint = m_terrainChunk.bounds.ClosestPoint(m_cameraPosition);
      return Vector3.Distance(closestPoint, m_cameraPosition);
    }
  }

  public bool isWithinMaxDistance {
    get {
      return distanceToCamera < maxDistance;
    }
  }

  public bool isUsingGrass {
    get {
      return m_terrainChunk.levelOfDetail == 1 && terrainShape.useGrass;
    }
  }

  private void Awake() {
    m_terrainChunk = GetComponent<TerrainChunk>();
    if (!isUsingGrass) {
      return;
    }

    m_nativeInstances = new NativeList<GrassInstance>(Allocator.Persistent);
    StartCoroutine(UpdateCoroutine());
  }

  private void OnDestroy() {
    if (!isUsingGrass) {
      return;
    }

    if (m_generationJobHandle.HasValue) {
      Debug.Log("Grass Chunk destroyed and there was a generation job running");
      CancelGenerationJob();
    }

    if (m_instancingJobHandle.HasValue) {
      Debug.Log("Grass Chunk destroyed and there was a instancing job running");
      CancelInstancingJob();
    }

    m_nativeInstances.Dispose();
  }

  private IEnumerator UpdateCoroutine() {
    // Wait for the chunk to be generated
    while (m_terrainChunk.status != TerrainChunkStatus.Generated) {
      yield return 0f;
    }

    // Wait for the chunk to be near the camera
    do {
      UpdateCameraPosition();
      yield return 0f;
    } while (distanceToCamera > maxDistance);

    // Generate for the first time
    ScheduleGenerationJob();

    // Wait for the job to be completed
    while (m_generationJobHandle.HasValue) {
      if (m_generationJobHandle.Value.IsCompleted) {
        ProcessGenerationJobResult();
      } else {
        yield return 0f;
      }
    }

    // Start a coroutine that prepares the groups for mesh instancing
    StartCoroutine(PrepareInstancing());

    while (true) {
      // Get the position to the camera
      UpdateCameraPosition();

      // Don't update if the chunk is far away
      if (!isWithinMaxDistance) {
        yield return 0f;
        continue;
      }

      // Render the grass batches
      Render();

      yield return 0f;
    }
  }

  private void UpdateCameraPosition() {
    m_camera = Camera.main;
    m_cameraPosition = m_camera.transform.position;
  }

  private void Render() {
    foreach (var item in m_groups) {
      DetailSubmesh[] submeshes = item.Key;
      GrassInstancingBatch batch = item.Value;

      // Set values in the material block
      float absoluteMaxDistance = maxDistance * batch.grass.maxDistance;
      m_materialBlock.SetFloat("_FadeStart", absoluteMaxDistance * 0.5f);
      m_materialBlock.SetFloat("_FadeEnd", absoluteMaxDistance);

      if (batch.matrices.Count > 0) {
        for (int i = 0; i < submeshes.Length; i++) {
          DetailSubmesh submesh = submeshes[i];

          if (!skipRendering) {
            Graphics.DrawMeshInstanced(
              submesh.mesh,
              submesh.submeshIndex,
              submesh.material,
              batch.matrices,
              m_materialBlock,
              submesh.castShadows
            );
          }
        }
      }
    }
  }

  private void ScheduleGenerationJob() {
    m_status = GrassChunkStatus.Generating;

    m_spawnersHandle = GCHandle.Alloc(terrainShape.grassSpawners);
    m_meshDataArray = Mesh.AcquireReadOnlyMeshData(m_terrainChunk.mesh);

    GrassChunkGenerateJob job = new GrassChunkGenerateJob {
      chunkPosition = m_terrainChunk.position,
      chunkSeed = terrainShape.terrainSeed,
      cameraPosition = m_cameraPosition,
      instances = m_nativeInstances,
      spawners = m_spawnersHandle,
      meshDataArray = m_meshDataArray,
      logTime = logGenerationInfo
    };

    m_generationJobHandle = job.Schedule();
  }

  private void DisposeGenerationJob() {
    m_spawnersHandle.Free();
    m_meshDataArray.Dispose();
    m_generationJobHandle = null;
  }

  private void CancelGenerationJob() {
    m_generationJobHandle.Value.Complete();
    DisposeGenerationJob();
  }

  private void ProcessGenerationJobResult() {
    m_generationJobHandle.Value.Complete();

    // Free memory
    DisposeGenerationJob();

    m_status = GrassChunkStatus.Generated;
  }

  private IEnumerator PrepareInstancing() {
    while (true) {
      // Don't update if the chunk is far away
      if (!isWithinMaxDistance) {
        yield return 0f;
        continue;
      }

      // Schedule the job to prepare the mesh instancing
      ScheduleInstancingJob();

      // Wait for the job to be completed
      while (m_instancingJobHandle.HasValue) {
        if (m_instancingJobHandle.Value.IsCompleted) {
          ProcessInstancingJobResult();
        } else {
          yield return 0f;
        }
      }

      yield return new WaitForSeconds(0.1f);
    }
  }

  private void ScheduleInstancingJob() {
    // Swap the two dictionary of groups because we are about to edit one of then
    SwapGroups();

    m_grassesHandle = GCHandle.Alloc(manager.grassesById);
    m_groupsHandle = GCHandle.Alloc(m_groupsCopy);

    GrassChunkInstancingJob job = new GrassChunkInstancingJob {
      maxDistance = maxDistance,
      cameraPosition = m_cameraPosition,
      instances = m_nativeInstances,
      grasses = m_grassesHandle,
      groups = m_groupsHandle,
      logTime = logInstancingInfo
    };

    m_instancingJobHandle = job.Schedule();
  }

  private void SwapGroups() {
    var copy = m_groupsCopy;
    m_groupsCopy = m_groups;
    m_groups = copy;
  }

  private void DisposeInstancingJob() {
    m_grassesHandle.Free();
    m_groupsHandle.Free();
    m_instancingJobHandle = null;
  }

  private void CancelInstancingJob() {
    m_instancingJobHandle.Value.Complete();
    DisposeInstancingJob();
  }

  private void ProcessInstancingJobResult() {
    m_instancingJobHandle.Value.Complete();

    // Create a material block
    if (m_materialBlock == null) {
      m_materialBlock = new MaterialPropertyBlock();
    }

    // Free memory
    DisposeInstancingJob();
  }
}