using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public struct GrassChunkInstancingJob : IJob {
  public float maxDistance;
  public Vector3 cameraPosition;
  [ReadOnly] public NativeArray<GrassInstance> instances;
  [ReadOnly] public GCHandle grasses;
  [ReadOnly] public GCHandle groups;
  public bool logTime;

  public void Execute() {
    var timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    var grasses = (Dictionary<int, Grass>)this.grasses.Target;
    var groups = (Dictionary<DetailSubmesh[], GrassInstancingBatch>)this.groups.Target;

    // Clear the matrices
    foreach (var batch in groups) {
      batch.Value.matrices.Clear();
    }

    // Iterate the instances
    for (int i = 0; i < instances.Length; i++) {
      GrassInstance instance = instances[i];

      // Calculate distance to camera
      float distance = Vector3.Distance(instance.matrix.GetPosition(), cameraPosition);
      if (distance > maxDistance) {
        continue;
      }

      Grass grass = grasses[instance.grassId];
      float maxGrassAbsoluteDistance = grass.maxDistance * maxDistance;
      if (distance > maxGrassAbsoluteDistance) {
        continue;
      }

      DetailMeshSet meshSet = grass.meshes[instance.meshIndex];

      // Get LOD
      float normalizedDistance = distance / maxGrassAbsoluteDistance;
      DetailSubmesh[] submeshes = null;
      for (int j = meshSet.levelOfDetails.Length - 1; j >= 0; j--) {
        DetailMeshWithLOD meshWithLod = meshSet.levelOfDetails[j];
        if (normalizedDistance > meshWithLod.distance) {
          submeshes = meshWithLod.submeshes;
          break;
        }
      }

      // Get the list of matrices or create it if necessary
      GrassInstancingBatch batch;
      if (!groups.TryGetValue(submeshes, out batch)) {
        batch = groups[submeshes] = new GrassInstancingBatch(grass);
      }

      // Add the matrix
      batch.matrices.Add(instance.matrix);
    }

    // Calculate number of batches
    int totalBatches = 0;
    foreach (var batch in groups) {
      totalBatches += batch.Value.matrices.Count;
    }

    if (logTime) {
      Debug.Log(
        $"Grass mesh instancing prepared in {timer.Elapsed.TotalMilliseconds} ms, resulting in {instances.Length} batches from {totalBatches} instances"
      );
    }
  }
}