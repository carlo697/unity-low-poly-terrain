using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public struct BakeSingleMeshJob : IJob {
  private int meshId;

  public BakeSingleMeshJob(int meshIds) {
    this.meshId = meshIds;
  }

  public void Execute() {
    Physics.BakeMesh(meshId, false);
  }
}