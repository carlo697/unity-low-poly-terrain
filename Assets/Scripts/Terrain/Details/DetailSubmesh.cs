using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class DetailMeshSet {
  public DetailSubmesh[] submeshes;
}

[System.Serializable]
public class DetailSubmesh {
  public Mesh mesh;
  public Material material;
  public int submeshIndex;
  public ShadowCastingMode castShadows = ShadowCastingMode.On;
}