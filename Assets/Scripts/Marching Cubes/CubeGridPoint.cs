using UnityEngine;

public struct CubeGridPoint {
  public int index;
  public Vector3 position;
  public float value;
  // public byte material;
  public Color color;

  public CubeGridPoint(
    int index,
    Vector3 position,
    float value
  ) {
    this.index = index;
    this.position = position;
    this.value = value;
    // this.material = 0;
    this.color = new Color();
  }

  public override string ToString() {
    return string.Format(
      "pos: {0}, value: {1}",
      position.ToString(),
      value
    );
  }
}