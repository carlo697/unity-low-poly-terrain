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
    var groups = (Dictionary<DetailSubmesh, List<Matrix4x4>>)this.groups.Target;

    // Clear the matrices
    foreach (var batch in groups) {
      batch.Value.Clear();
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

      DetailSubmesh[] submeshes = grass.meshes[instance.meshIndex].submeshes;
      for (int meshIdx = 0; meshIdx < submeshes.Length; meshIdx++) {
        DetailSubmesh submesh = submeshes[meshIdx];

        // Get the list of matrices or create it if necessary
        List<Matrix4x4> lists;
        if (!groups.TryGetValue(submesh, out lists)) {
          lists = groups[submesh] = new List<Matrix4x4>();
        }

        // Add the matrix
        lists.Add(instance.matrix);
      }
    }

    // Calculate number of batches
    int totalBatches = 0;
    foreach (var batch in groups) {
      totalBatches += batch.Value.Count;
    }

    // Debug.LogFormat(
    //   "Grass mesh instancing prepared in {0} ms, resulting in {2} batches from {1} instances",
    //   timer.ElapsedMilliseconds,
    //   instances.Length,
    //   totalBatches
    // );
  }
}