using UnityEngine;
using System;

[Serializable]
public class FractalNoiseGenerator : TerrainNoiseGenerator {
  public bool is3d = false;
  public int seed = 0;
  public float scale = 1f;
  public float amplitude = 1f;
  public NoiseType noiseType = NoiseType.Simplex;
  public float gain = 0.5f;
  public float lacunarity = 2f;
  public int octaves = 1;
  public FractalType fractalType = FractalType.FractalFBm;
  public bool useCurve = false;
  public AnimationCurve curve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);
  public bool useDomainWarp = false;
  public float domainWarpAmplitude = 1f;
  public float domainWarpFrequency = 0.5f;

  public enum FractalType {
    [InspectorName("Fractal FBm")]
    FractalFBm,
    FractalRidged
  };

  public enum NoiseType {
    Value,
    Perlin,
    Simplex,
    OpenSimplex2,
    OpenSimplex2S
  };

  public float[] GenerateNoise(TerrainChunk chunk, float frequency, int seed) {
    FastNoise noise = new FastNoise(fractalType.ToString());
    noise.Set("Source", new FastNoise(noiseType.ToString()));
    noise.Set("Gain", gain);
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
