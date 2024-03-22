using UnityEngine;
using FastNoise2Graph;
using System;
using System.Runtime.CompilerServices;

[Serializable]
public class NoiseTreeGenerator {
  public int seed = 0;
  public float scale = 1f;
  public NoiseTree tree;

  [Header("Curve")]
  public bool useCurve = false;
  public AnimationCurve curve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);

  public class Generator : INoiseGenerator {
    private NoiseTreeGenerator m_settings;
    private FastNoise2Graph.FastNoise m_noise;
    private AnimationCurve m_curve;

    public Generator(NoiseTreeGenerator settings) {
      m_settings = settings;

      m_noise = settings.tree.GetFastNoiseSafe();

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
