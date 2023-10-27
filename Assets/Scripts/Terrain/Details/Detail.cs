using UnityEngine;

public abstract class Detail : ScriptableObject {
  public abstract int id { get; }
  public abstract GameObject[] prefabs { get; }
  public abstract DetailSubmesh[] submeshes { get; }
}
