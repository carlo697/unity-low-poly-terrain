
using UnityEngine;

public class FractalNoise : INoise {
  private float frequency;
  private float amplitude;
  private float persistence;
  private int seed;
  private int octaves;
  private float maxHeight;

  private Vector3[] m_offsets;

  public FractalNoise(
    float frequency,
    float amplitude = 1f,
    float persistence = 0.5f,
    int octaves = 3,
    int seed = 0
  ) {
    this.frequency = frequency;
    this.amplitude = amplitude;
    this.persistence = persistence;
    this.octaves = octaves;

    this.maxHeight = 1f / getMaxValue();
    // this.maxHeight = 1f / Mathf.Pow(getMaxValue(), 1f / 2.5f) * 1.02f;

    SetSeed(seed);
  }

  public float getMaxValue() {
    return (Mathf.Pow(this.persistence, octaves) - 1f) / (persistence - 1f);
  }

  public void SetSeed(int seed) {
    this.seed = seed;

    System.Random prng = new System.Random(seed);
    m_offsets = new Vector3[octaves];

    for (int i = 0; i < octaves; i++) {
      m_offsets[i] = new Vector3(
        prng.Next(-1000, 1000),
        prng.Next(-1000, 1000),
        prng.Next(-1000, 1000)
      );
    }
  }

  public float Sample(float x, float y, float z) {
    float finalValue = 0.0f;

    float frequency = this.frequency;
    float amplitude = this.amplitude;

    for (int i = 0; i < octaves; ++i) {
      finalValue += PerlinNoise.Sample(
        x * frequency + m_offsets[i].x,
        y * frequency + m_offsets[i].y,
        z * frequency + m_offsets[i].z
      ) * amplitude;

      amplitude *= persistence;
      frequency *= 2.0f;
    }

    return finalValue * maxHeight;
  }
}
