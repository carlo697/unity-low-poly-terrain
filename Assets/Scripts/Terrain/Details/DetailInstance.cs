using UnityEngine;

public struct DetailInstance {
  public int detailId;
  public int meshIndex;

  public Vector3 position;
  public Quaternion rotation;
  public Vector3 scale;
  public Matrix4x4 matrix;
  public SphereBounds sphereBounds;
}
