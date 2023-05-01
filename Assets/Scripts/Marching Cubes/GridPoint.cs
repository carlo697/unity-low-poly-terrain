using UnityEngine;

public struct GridPoint {
  public Vector3 position;
  public float value;

  public GridPoint(Vector3 position, float value) {
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