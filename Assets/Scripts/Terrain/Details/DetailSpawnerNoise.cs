using UnityEngine;
using System;

[Serializable]
public class DetailSpawnerNoise : BasicNoiseSettings {
  public class Generator {
    private DetailSpawnerNoise m_parent;
    private FastNoise m_noise;

    public Generator(DetailSpawnerNoise settings) {
      m_parent = settings;
      m_noise = new FastNoise(settings.fractalType.ToString());
      m_noise.Set("Source", new FastNoise(settings.noiseType.ToString()));
      m_noise.Set("Gain", settings.gain);
      m_noise.Set("Weighted Strength", settings.weightedStrength);
      m_noise.Set("Lacunarity", settings.lacunarity);
      m_noise.Set("Octaves", settings.octaves);

      if (settings.amplitude != 1f) {
        FastNoise multiply = new FastNoise("Multiply");
        multiply.Set("LHS", m_noise);
        multiply.Set("RHS", settings.amplitude);
        m_noise = multiply;
      }
    }

    public float Generate(float x, float y, int seed) {
      // Generate noise from a 2d point
      float value = m_noise.GenSingle2D(
        x / m_parent.scale,
        y / m_parent.scale,
        m_parent.seed + seed
      );

      // Use the curve
      if (m_parent.useCurve) {
        value = m_parent.curve.Evaluate(((value + 1f) / 2f));
      }

      // Normalize and return value
      return value;
    }
  }

  public Generator GetGenerator() {
    return new Generator(this);
  }
}
