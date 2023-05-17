using UnityEngine;
using System;

public class TerrainNoise : ISamplerFactory {

  public float noiseSize = 32f;
  public int noiseOctaves = 5;
  public bool updateChunksInEditor = true;

  public AnimationCurve curve;
  public AnimationCurve normalizerCurve;

  private int seed = 0;

  private float Normalize(float value) {
    return ((value + 1f) / 2f);
  }

  private float Denormalize(float value) {
    return (value * 2f) - 1f;
  }

  public override Func<float, float, float, float> GetSampler(
    TerrainChunk chunk
  ) {
    // Create copies of the curves
    AnimationCurve curve = new AnimationCurve(this.curve.keys);
    AnimationCurve normalizerCurve = new AnimationCurve(this.normalizerCurve.keys);

    // Main noise for the terrain
    FastNoiseLite noise = new FastNoiseLite(seed + 1);
    noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
    noise.SetFrequency(1f);
    noise.SetFractalType(FastNoiseLite.FractalType.FBm);
    noise.SetFractalOctaves(noiseOctaves);

    // FastNoiseLite cavesNoise = new FastNoiseLite(seed);
    // cavesNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
    // cavesNoise.SetFrequency(8f / noiseMultiplier);

    // Noise for a cave system
    FastNoiseLite cavesNoise = new FastNoiseLite(seed);
    cavesNoise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
    cavesNoise.SetFrequency(4f);
    cavesNoise.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.EuclideanSq);
    cavesNoise.SetCellularReturnType(FastNoiseLite.CellularReturnType.Distance);
    cavesNoise.SetCellularJitter(1f);
    // cavesNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
    // cavesNoise.SetFractalOctaves(2);

    // Variables needed to sample the point in world space
    float noiseMultiplier = 1 / (noiseSize * chunk.noiseSize);
    Vector3 chunkWorldPosition = chunk.transform.position;
    Vector3 chunkWorldSize = chunk.size;
    Vector3 inverseChunkWorldSize = new Vector3(
      1f / chunkWorldSize.x,
      1f / chunkWorldSize.y,
      1f / chunkWorldSize.z
    );

    return (float x, float y, float z) => {
      // Sample the point in world space
      float finalX = ((chunkWorldPosition.x + x) * noiseMultiplier) + chunk.noiseOffset.x;
      float finalY = ((chunkWorldPosition.y + y) * noiseMultiplier) + chunk.noiseOffset.y;
      float finalZ = ((chunkWorldPosition.z + z) * noiseMultiplier) + chunk.noiseOffset.z;

      // Start sampling
      float height = y * inverseChunkWorldSize.y;

      // Add terrain noise
      height -= Normalize(noise.GetNoise(finalX, finalY, finalZ));
      // height += ((caves.GetNoise(finalX, finalZ) + 1f) / 2f) * 0.1f;
      // height += 1f - Mathf.Abs(noise.GetNoise(finalX, 0, finalZ));

      // Caves
      // float caves3D = cavesNoise.GetNoise(finalX, finalY, finalZ) + 0.2f;
      // height = Denormalize(
      //   Normalize(normalizerCurve.Evaluate(height)) + Normalize(normalizerCurve.Evaluate(caves3D))
      // );

      return height;
    };
  }

  private void OnValidate() {
    if (updateChunksInEditor) {
      TerrainChunk[] chunks = GameObject.FindObjectsOfType<TerrainChunk>();
      foreach (var chunk in chunks) {
        if (chunk.samplerFactory == this) {
          chunk.GenerateOnEditor();
        }
      }
    }
  }
}
