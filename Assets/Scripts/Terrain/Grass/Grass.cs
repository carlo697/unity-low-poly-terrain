using UnityEngine;

public abstract class Grass : ScriptableObject {
  public abstract int id { get; }
  public abstract DetailMeshSet[] meshes { get; }
  public abstract float maxDistance { get; }
}
