using UnityEngine;
using System;
using System.Runtime.CompilerServices;

[Serializable]
public class FalloffNoise {
  public Vector2 mapSize = Vector3.one * 16000f;
  public bool useNoise = true;
  public AnimationCurve falloffGradientCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

  public NoiseTreeGenerator noise = new NoiseTreeGenerator {
    seed = 2,
    scale = 5.5f,
  };

  public class Generator : INoiseGenerator {
    private FalloffNoise m_settings;
    private INoiseGenerator m_noiseGenerator;
    private AnimationCurve m_gradientCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);

    public Generator(FalloffNoise settings) {
      m_settings = settings;
      m_noiseGenerator = settings.noise.GetGenerator();

      // Create copies of the curves (for thread safety)
      m_gradientCurve = new AnimationCurve(m_settings.falloffGradientCurve.keys);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float GetFinalValue(float value, float worldX, float worldZ) {
      // Clamped coordinates for creating the falloff map
      float posX = (worldX / (m_settings.mapSize.x)) * 2f;
      posX = Mathf.Clamp01(Math.Abs(posX));
      float posY = (worldZ / (m_settings.mapSize.y)) * 2f;
      posY = Mathf.Clamp01(Math.Abs(posY));

      // Create the falloff map
      float falloff = 1f - (1f - posX * posX) * (1f - posY * posY);
      falloff = 1f - m_gradientCurve.Evaluate(falloff);

      // Combine the falloff map and the noise
      if (m_settings.useNoise) {
        return value * falloff;
      }

      return falloff;
    }

    public float Generate3d(float x, float y, float z, float scale, int seed) {
      throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Generate2d(float x, float y, float scale, int seed) {
      float value = m_noiseGenerator.Generate2d(x, y, scale, seed);
      value = GetFinalValue(value, x, y);
      return value;
    }

    public float[] GenerateGrid3d(float[] output, FastNoiseChunk chunk, float scale, int terrainSeed) {
      throw new NotImplementedException();
    }

    public float[] GenerateGrid2d(float[] output, FastNoiseChunk chunk, float scale, int terrainSeed) {
      float[] values = m_noiseGenerator.GenerateGrid2d(output, chunk, scale, terrainSeed);

      for (int y = 0; y < chunk.resolution.z; y++) {
        for (int x = 0; x < chunk.resolution.x; x++) {
          // Transform the coordinates
          int index2D = TextureUtils.GetIndexFrom2d(x, y, chunk.resolution.x);

          // World position
          float worldX = chunk.position.x + ((float)x / (chunk.resolution.x - 1)) * chunk.scale.x;
          float worldZ = chunk.position.z + ((float)y / (chunk.resolution.z - 1)) * chunk.scale.z;

          // Save the final value
          values[index2D] = GetFinalValue(values[index2D], worldX, worldZ);
        }
      }

      return values;
    }
  }

  public Generator GetGenerator() {
    return new Generator(this);
  }
}
