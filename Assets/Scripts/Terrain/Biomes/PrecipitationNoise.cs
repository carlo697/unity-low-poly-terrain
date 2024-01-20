using UnityEngine;
using System;
using System.Runtime.CompilerServices;

[Serializable]
public class PrecipitationNoise {
  public Range range = new Range(0f, 450f);
  public FractalNoise noise = new FractalNoise {
    seed = 6,
    scale = 8f,
    noiseType = NoiseType.OpenSimplex2S,
    octaves = 3,
    fractalType = FractalType.FractalFBm,
    useCurve = true,
    curve = AnimationCurve.Linear(-1f, 0f, 1f, 450f)
  };

  public class Generator : INoiseGenerator {
    private PrecipitationNoise m_settings;
    private INoiseGenerator m_generator;

    public Generator(PrecipitationNoise settings) {
      m_settings = settings;
      m_generator = settings.noise.GetGenerator();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float GetFinalValue(float value) {
      return Mathf.Clamp(value, m_settings.range.min, m_settings.range.max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Normalize(float value) {
      return MathUtils.LinearInterpolation(value, m_settings.range.min, m_settings.range.max, 0f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Denormalize(float value) {
      return MathUtils.LinearInterpolation(value, 0f, 1f, m_settings.range.min, m_settings.range.max);
    }

    public float Generate3d(float x, float y, float z, float scale, int seed) {
      throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Generate2d(float x, float y, float scale, int seed) {
      float value = m_generator.Generate2d(x, y, scale, seed);
      value = GetFinalValue(value);
      return value;
    }

    public float[] GenerateGrid3d(FastNoiseChunk chunk, float scale, int terrainSeed) {
      throw new NotImplementedException();
    }

    public float[] GenerateGrid2d(FastNoiseChunk chunk, float scale, int terrainSeed) {
      float[] values = m_generator.GenerateGrid2d(chunk, scale, terrainSeed);

      int pointCount = chunk.pointCount2d;
      for (int index = 0; index < pointCount; index++) {
        values[index] = GetFinalValue(values[index]);
      }

      return values;
    }
  }

  public Generator GetGenerator() {
    return new Generator(this);
  }
}
