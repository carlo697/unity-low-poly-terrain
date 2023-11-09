using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class DetailMeshSet {
  public DetailMeshWithLOD[] levelOfDetails;
}

[System.Serializable]
public class DetailMeshWithLOD {
  [Range(0f, 1f)]
  public float distance;
  public DetailSubmesh[] submeshes;
}

[System.Serializable]
public class DetailSubmesh {
  public Mesh mesh;
  public Material material;
  public int submeshIndex;
  public ShadowCastingMode castShadows = ShadowCastingMode.On;
}