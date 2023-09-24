using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public struct BakeSingleMeshJob : IJob {
  private NativeReference<int> meshId;

  public BakeSingleMeshJob(NativeReference<int> meshIds) {
    this.meshId = meshIds;
  }

  public void Execute() {
    Physics.BakeMesh(meshId.Value, false);
  }
}