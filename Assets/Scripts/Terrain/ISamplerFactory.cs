public interface ISamplerFactory {
  public abstract CubeGridSamplerFunc GetSampler(TerrainChunk chunk);
}
