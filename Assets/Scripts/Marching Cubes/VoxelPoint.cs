using UnityEngine;

public struct VoxelPoint {
  public Vector3 position;
  public float value;
  public Color color;
  public float roughness;
  public uint material;

  public override string ToString() {
    return $"pos: {position}, value: {value}";
  }
}