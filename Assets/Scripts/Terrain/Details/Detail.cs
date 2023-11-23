using UnityEngine;

public abstract class Detail : ScriptableObject {
  public abstract int id { get; }
  public abstract GameObject[] prefabs { get; }
  public abstract DetailMeshSet[] meshes { get; }
  public abstract float maxDistance { get; }
}
