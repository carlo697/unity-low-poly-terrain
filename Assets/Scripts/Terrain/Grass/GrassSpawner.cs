
using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;

public abstract class GrassSpawner : ScriptableObject {
  public abstract Grass grass { get; }

  public abstract int Spawn(
    NativeList<GrassInstance> instances,
    Vector3 chunkPosition,
    ulong seed,
    int vertexIndex,
    Vector3 a,
    Vector3 b,
    Vector3 c,
    float area,
    Vector3 unnormalizedNormal,
    uint materialId
  );
}
