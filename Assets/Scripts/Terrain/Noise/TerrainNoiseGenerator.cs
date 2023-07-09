

public interface TerrainNoiseGenerator {
  public float[] GenerateNoise(TerrainChunk chunk, float frequency, int seed);
}