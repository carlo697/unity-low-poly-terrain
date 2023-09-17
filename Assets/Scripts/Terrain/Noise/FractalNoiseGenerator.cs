using UnityEngine;
using System;

[Serializable]
public class FractalNoiseGenerator : BasicNoiseSettings, TerrainNoiseGenerator {
  public bool is3d = false;
  public bool useDomainWarp = false;
  public float domainWarpAmplitude = 1f;
  public float domainWarpFrequency = 0.5f;

  public float[] GenerateNoise(TerrainChunk chunk, float frequency, int seed) {
    FastNoise noise = new FastNoise(fractalType.ToString());
    noise.Set("Source", new FastNoise(noiseType.ToString()));
    noise.Set("Gain", gain);
    noise.Set("Weighted Strength", weightedStrength);
    noise.Set("Lacunarity", lacunarity);
    noise.Set("Octaves", octaves);

    if (amplitude != 1f) {
      FastNoise multiply = new FastNoise("Multiply");
      multiply.Set("LHS", noise);
      multiply.Set("RHS", amplitude);
      noise = multiply;
    }

    if (useDomainWarp) {
      FastNoise domainWarp = new FastNoise("Domain Warp Gradient");
      domainWarp.Set("Source", noise);
      domainWarp.Set("Warp Amplitude", domainWarpAmplitude);
      domainWarp.Set("Warp Frequency", domainWarpFrequency);
      noise = domainWarp;
    }

    float[] pixels = TerrainShape.GenerateFastNoiseForChunk(
      is3d,
      chunk,
      noise,
      seed + this.seed,
      (1f / scale) * frequency
    );

    if (useCurve) {
      AnimationCurve curve = new AnimationCurve(this.curve.keys);
      for (int index = 0; index < pixels.Length; index++) {
        pixels[index] = curve.Evaluate(pixels[index]);
      }
    }

    return pixels;
  }
}
