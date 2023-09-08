
using UnityEngine;
using System.Collections.Generic;

public abstract class DetailSpawner : ScriptableObject {
  public abstract Detail detail { get; }
  public abstract void Spawn(
    List<TempDetailInstance> instances,
    ulong seed,
    Bounds bounds,
    float levelOfDetail
  );
}
