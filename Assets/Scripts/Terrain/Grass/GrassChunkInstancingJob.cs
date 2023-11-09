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

  public void Execute() {
    var timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    var grasses = (Dictionary<int, Grass>)this.grasses.Target;
    var groups = (Dictionary<DetailMeshSet, GrassInstancingBatch>)this.groups.Target;

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
      if (distance > grass.maxDistance * maxDistance) {
        continue;
      }

      DetailMeshSet meshSet = grass.meshes[instance.meshIndex];

      // Get the list of matrices or create it if necessary
      GrassInstancingBatch batch;
      if (!groups.TryGetValue(meshSet, out batch)) {
        batch = groups[meshSet] = new GrassInstancingBatch(grass);
      }

      // Add the matrix
      batch.matrices.Add(instance.matrix);
    }

    // Calculate number of batches
    int totalBatches = 0;
    foreach (var batch in groups) {
      totalBatches += batch.Value.matrices.Count;
    }

    // Debug.LogFormat(
    //   "Grass mesh instancing prepared in {0} ms, resulting in {2} batches from {1} instances",
    //   timer.ElapsedMilliseconds,
    //   instances.Length,
    //   totalBatches
    // );
  }
}