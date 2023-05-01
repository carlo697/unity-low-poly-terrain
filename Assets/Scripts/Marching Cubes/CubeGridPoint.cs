using UnityEngine;

public struct CubeGridPoint {
  public Vector3 position;
  public float value;

  public CubeGridPoint(Vector3 position, float value) {
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