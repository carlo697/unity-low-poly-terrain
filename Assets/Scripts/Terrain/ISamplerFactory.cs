using System;
using UnityEngine;

public abstract class ISamplerFactory : MonoBehaviour {
  public abstract CubeGridSamplerFunc GetSampler(TerrainChunk chunk);
}
