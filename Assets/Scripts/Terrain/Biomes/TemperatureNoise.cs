using UnityEngine;
using System;
using System.Runtime.CompilerServices;

[Serializable]
public class TemperatureNoise {
  public Vector2 mapSize = Vector3.one * 16000f;
  public Range range = new Range(-15f, 30f);
  public float polarTemperatureDifference = 45f;
  public FractalNoise noise = new FractalNoise {
    seed = 5,
    scale = 8f,
    noiseType = NoiseType.OpenSimplex2S,
    octaves = 3,
    fractalType = FractalType.FractalFBm,
    useCurve = true,
    curve = AnimationCurve.Linear(-1f, 20f, 1f, 30f)
  };

  public class Generator : INoiseGenerator {
    private TemperatureNoise m_settings;
    private INoiseGenerator m_generator;

    public Generator(TemperatureNoise settings) {
      m_settings = settings;
      m_generator = settings.noise.GetGenerator();
    }

    private float GetFinalValue(float value, float z) {
      float bottomEdge = -0.5f * m_settings.mapSize.y;
      float distanceToBottom = z - bottomEdge;
      if (distanceToBottom < 0f) {
        distanceToBottom = 0f;
      }

      float falloff = Mathf.Abs(distanceToBottom) / (m_settings.mapSize.y);
      // falloff = Mathf.SmoothStep(0f, 1f, falloff);

      return Mathf.Clamp(
        value - falloff * m_settings.polarTemperatureDifference,
        m_settings.range.min,
        m_settings.range.max
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Normalize(float value) {
      return MathUtils.LinearInterpolation(value, m_settings.range.min, m_settings.range.max, 0f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Denormalize(float value) {
      return MathUtils.LinearInterpolation(value, 0f, 1f, m_settings.range.min, m_settings.range.max);
    }

    public float Generate3d(float x, float y, float z, int seed) {
      throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Generate2d(float x, float y, int seed) {
      float value = m_generator.Generate2d(x, y, seed);
      value = GetFinalValue(value, y);
      return value;
    }

    public float[] GenerateGrid3d(FastNoiseChunk chunk, float scale, int terrainSeed) {
      throw new NotImplementedException();
    }

    public float[] GenerateGrid2d(FastNoiseChunk chunk, float scale, int terrainSeed) {
      float[] values = m_generator.GenerateGrid2d(chunk, scale, terrainSeed);

      int pointCount = chunk.pointCount2d;
      for (int index = 0; index < pointCount; index++) {
        float value = values[index];

        Vector2Int coords = TextureUtils.Get2dFromIndex(index, chunk.resolution.x);
        float worldZ = chunk.position.z + ((float)coords.y / (chunk.resolution.z - 1)) * chunk.scale.z;

        values[index] = GetFinalValue(value, worldZ);
      }

      return values;
    }
  }

  public Generator GetGenerator() {
    return new Generator(this);
  }
}
