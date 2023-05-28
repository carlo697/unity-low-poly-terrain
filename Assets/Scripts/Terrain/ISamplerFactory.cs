using System;
using UnityEngine;

public abstract class ISamplerFactory : MonoBehaviour {
  public abstract void GetSampler(
    TerrainChunk chunk,
    out CubeGridSamplerFunc samplerFunc,
    out CubeGridPostProcessingFunc postProcessingFunc
  );
}
