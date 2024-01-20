using System;
using System.Runtime.CompilerServices;

[Serializable]
public class CellularNoise {
  public int seed = 0;
  public float scale = 1f;
  public CellularNoiseType noiseType = CellularNoiseType.CellularValue;
  public float jitterModifier = 1f;

  public class Generator : INoiseGenerator {
    private CellularNoise m_settings;
    private FastNoise m_noise;

    public Generator(CellularNoise settings) {
      m_settings = settings;
      m_noise = new FastNoise(settings.noiseType.ToString());
      m_noise.Set("Jitter Modifier", settings.jitterModifier);
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

      return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Generate2d(float x, float y, float scale, int seed) {
      float frequency = 1f / (scale * m_settings.scale);

      // Generate noise from a 2d point
      float value = m_noise.GenSingle2D(x * frequency, y * frequency, m_settings.seed + seed);

      return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float[] GenerateGrid3d(FastNoiseChunk tile, float scale, int terrainSeed) {
      return GenerateGrid(true, tile, scale, terrainSeed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float[] GenerateGrid2d(FastNoiseChunk tile, float scale, int terrainSeed) {
      return GenerateGrid(false, tile, scale, terrainSeed);
    }

    private float[] GenerateGrid(bool is3d, FastNoiseChunk tile, float scale, int terrainSeed) {
      float[] pixels = tile.GenerateGrid(
        is3d,
        m_noise,
        terrainSeed + m_settings.seed,
        m_settings.scale * scale
      );

      return pixels;
    }
  }

  public Generator GetGenerator() {
    return new Generator(this);
  }
}
