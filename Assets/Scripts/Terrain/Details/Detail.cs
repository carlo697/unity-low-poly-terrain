using UnityEngine;

public abstract class Detail : ScriptableObject {
  public abstract string id { get; }
  public abstract GameObject[] prefabs { get; }
  public abstract int preAllocateCount { get; }
}
