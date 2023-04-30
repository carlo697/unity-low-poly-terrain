using UnityEngine;

public class BasicNoise : INoise {
  private float frequency;
  private float amplitude;
  private int seed;

  private Vector3 m_offset;

  public BasicNoise(
    float frequency,
    float amplitude = 1f,
    int seed = 0
  ) {
    this.frequency = frequency;
    this.amplitude = amplitude;
    SetSeed(seed);
  }

  public void SetSeed(int seed) {
    this.seed = seed;

    System.Random prng = new System.Random(seed);
    m_offset = new Vector3(
      prng.Next(-1000, 1000),
      prng.Next(-1000, 1000),
      prng.Next(-1000, 1000)
    );
  }

  public float Sample(float x, float y, float z) {
    return PerlinNoise.Sample(
      x * frequency + m_offset.x,
      y * frequency + m_offset.y,
      z * frequency + m_offset.z
    ) * amplitude;
  }
}
