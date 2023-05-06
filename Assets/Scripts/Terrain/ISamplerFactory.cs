using System;
using UnityEngine;

public abstract class ISamplerFactory : MonoBehaviour {
  public abstract Func<float, float, float, float> GetSampler(TerrainChunk chunk);
}
