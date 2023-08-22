
using UnityEngine;
using System.Collections.Generic;

public abstract class DetailSpawner : ScriptableObject {
  public abstract Detail detail { get; }
  public abstract List<TempDetailInstance> Spawn(ulong seed, Bounds bounds, float levelOfDetail);
}
