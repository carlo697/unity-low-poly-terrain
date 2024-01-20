using UnityEngine;
using System;
using System.Runtime.CompilerServices;

[Serializable]
public class FractalNoise : BasicNoiseSettings {
  public class Generator : INoiseGenerator {
    private FractalNoise m_settings;
    private FastNoise m_noise;
    private AnimationCurve m_curve;

    public Generator(FractalNoise settings) {
      m_settings = settings;

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

      if (settings.useDomainWarp) {
        FastNoise domainWarp = new FastNoise("Domain Warp Gradient");
        domainWarp.Set("Source", m_noise);
        domainWarp.Set("Warp Amplitude", settings.domainWarpAmplitude);
        domainWarp.Set("Warp Frequency", settings.domainWarpFrequency);
        m_noise = domainWarp;
      }

      // Create a new curve because AnimationCurve is not thread-safe
      if (settings.useCurve) {
        m_curve = new AnimationCurve(settings.curve.keys);
      }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Generate3d(float x, float y, float z, float scale, int seed) {
      // Generate noise from a 2d point
      float value = m_noise.GenSingle3D(
        x / m_settings.scale,
        y / m_settings.scale,
        z / m_settings.scale,
        m_settings.seed + seed
      );

      // Use the curve
      if (m_settings.useCurve) {
        value = m_curve.Evaluate(value);
      }

      return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Generate2d(float x, float y, float scale, int seed) {
      float frequency = 1f / (scale * m_settings.scale);

      // Generate noise from a 2d point
      float value = m_noise.GenSingle2D(x * frequency, y * frequency, m_settings.seed + seed);

      // Use the curve
      if (m_settings.useCurve) {
        value = m_curve.Evaluate(value);
      }

      return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float[] GenerateGrid3d(float[] output, FastNoiseChunk chunk, float scale, int terrainSeed) {
      return GenerateGrid(output, true, chunk, scale, terrainSeed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float[] GenerateGrid2d(float[] output, FastNoiseChunk chunk, float scale, int terrainSeed) {
      return GenerateGrid(output, false, chunk, scale, terrainSeed);
    }

    private float[] GenerateGrid(float[] output, bool is3d, FastNoiseChunk chunk, float scale, int terrainSeed) {
      float[] pixels = chunk.GenerateGrid(
        output,
        is3d,
        m_noise,
        terrainSeed + m_settings.seed,
        m_settings.scale * scale
      );

      if (m_settings.useCurve) {
        for (int index = 0; index < pixels.Length; index++) {
          pixels[index] = m_curve.Evaluate(pixels[index]);
        }
      }

      return pixels;
    }
  }

  public Generator GetGenerator() {
    return new Generator(this);
  }
}
