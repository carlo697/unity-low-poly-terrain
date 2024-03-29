
using UnityEngine;
using System.Collections.Generic;

public abstract class DetailSpawner : ScriptableObject {
  public abstract Detail detail { get; }
  public abstract void Spawn(
    List<DetailInstance> instances,
    ulong seed,
    TerrainChunk terrainChunk,
    Bounds bounds,
    int integerLevelOfDetail,
    float normalizedLevelOfDetail
  );
}
