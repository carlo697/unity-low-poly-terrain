using UnityEngine;

public struct GridPoint {
  public Vector3 position;
  public float value;

  public GridPoint(Vector3 position, float value) {
    this.position = position;
    this.value = value;
  }
}