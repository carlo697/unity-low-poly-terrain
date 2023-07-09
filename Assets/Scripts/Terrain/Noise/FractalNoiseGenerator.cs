using System;

[Serializable]
public class FractalNoiseGenerator : TerrainNoiseGenerator {
  public bool is3d = false;
  public int seed = 0;
  public float scale = 1f;
  public float gain = 0.5f;
  public float lacunarity = 2f;
  public int octaves = 1;

  public float[] GenerateNoise(TerrainChunk chunk, float frequency, int seed) {
    FastNoise noise = new FastNoise("FractalFBm");
    noise.Set("Source", new FastNoise("Simplex"));
    noise.Set("Gain", gain);
    noise.Set("Lacunarity", lacunarity);
    noise.Set("Octaves", octaves);
    return TerrainShape.GenerateFastNoiseForChunk(
      is3d,
      chunk,
      noise,
      seed + this.seed,
      (1f / scale) * frequency
    );
  }
}
