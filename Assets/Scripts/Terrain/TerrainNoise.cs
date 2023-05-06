using UnityEngine;
using System;

public class TerrainNoise : ISamplerFactory {

  public float noiseSize = 32f;
  public int noiseOctaves = 5;

  public override Func<float, float, float, float> GetSampler(
    TerrainChunk chunk
  ) {
    float noiseMultiplier = noiseSize * chunk.noiseSize;

    FractalNoise noise = new FractalNoise(
      1 / noiseMultiplier,
      1f,
      0.5f,
      noiseOctaves
    );

    return (float x, float y, float z) => {
      // For supporting non symmetrical grids we need to mutiply each
      // coord by the resolution to get symmetrical noise
      // return y - noise.Sample(
      //   (x + chunk.noiseOffset.x) * chunk.resolution.x,
      //   (y + chunk.noiseOffset.y) * chunk.resolution.y,
      //   (z + chunk.noiseOffset.z) * chunk.resolution.z
      // );

      float xMultiplier = (x + chunk.noiseOffset.x) * chunk.resolution.x;
      float yMultiplier = (y + chunk.noiseOffset.y) * chunk.resolution.y;
      float zMultiplier = (z + chunk.noiseOffset.z) * chunk.resolution.z;

      float heightFalloff = y - 0.1f;
      // float baseNoise = ((noise.Sample(
      //   xMultiplier,
      //   yMultiplier,
      //   zMultiplier
      // ) + 1) / 2) * 0.95f;
      float baseHeight =
        ((noise.Sample(xMultiplier, 0, zMultiplier) + 1f) / 2f)
        * 0.95f;
      return heightFalloff - baseHeight;
    };
  }
}
