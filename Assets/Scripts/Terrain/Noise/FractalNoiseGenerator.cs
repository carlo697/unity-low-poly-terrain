using UnityEngine;
using System;
using System.Runtime.CompilerServices;

[Serializable]
public class FractalNoiseGenerator : BasicNoiseSettings {
  [Header("Domain Warp")]
  public bool useDomainWarp = false;
  public float domainWarpAmplitude = 1f;
  public float domainWarpFrequency = 0.5f;

  public class Generator : TerrainNoiseGenerator {
    private FractalNoiseGenerator m_settings;
    private FastNoise m_noise;
    private AnimationCurve m_curve;

    public Generator(FractalNoiseGenerator settings) {
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
    public float Generate3d(float x, float y, float z, int seed) {
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
    public float Generate2d(float x, float y, int seed) {
      // Generate noise from a 2d point
      float value = m_noise.GenSingle2D(
        x / m_settings.scale,
        y / m_settings.scale,
        m_settings.seed + seed
      );

      // Use the curve
      if (m_settings.useCurve) {
        value = m_curve.Evaluate(value);
      }

      return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float[] GenerateGrid3d(TerrainChunk chunk, float scale, int terrainSeed) {
      return GenerateGrid(true, chunk, scale, terrainSeed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float[] GenerateGrid2d(TerrainChunk chunk, float scale, int terrainSeed) {
      return GenerateGrid(false, chunk, scale, terrainSeed);
    }

    private float[] GenerateGrid(bool is3d, TerrainChunk chunk, float scale, int terrainSeed) {
      float[] pixels = TerrainShape.GenerateFastNoiseForChunk(
        is3d,
        chunk,
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
