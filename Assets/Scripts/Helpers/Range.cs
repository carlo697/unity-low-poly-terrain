using System;

[Serializable]
public struct Range {
  public float min;
  public float max;

  public Range(float min, float max) {
    this.min = min;
    this.max = max;
  }

  public bool IsInside(float value) {
    return value >= min && value <= max;
  }

  public override string ToString() {
    return $"Min: {min}, Max: {max}";
  }
}