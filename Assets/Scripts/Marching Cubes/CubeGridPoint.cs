using UnityEngine;

public struct CubeGridPoint {
  public int index;
  public Vector3Int coordinates;
  public Vector3 position;
  public float value;

  public CubeGridPoint(int index, Vector3Int coordinates, Vector3 position, float value) {
    this.index = index;
    this.coordinates = coordinates;
    this.position = position;
    this.value = value;
  }

  public override string ToString() {
    return string.Format(
      "pos: {0}, value: {1}",
      position.ToString(),
      value
    );
  }
}