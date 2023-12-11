public interface TerrainNoiseGenerator {
  public float Generate3d(float x, float y, float z, int seed);
  public float Generate2d(float x, float y, int seed);
  public float[] GenerateGrid3d(TerrainChunk chunk, float scale, int terrainSeed);
  public float[] GenerateGrid2d(TerrainChunk chunk, float scale, int terrainSeed);
}