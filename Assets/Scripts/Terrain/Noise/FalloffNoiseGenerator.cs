using UnityEngine;
using System;

[Serializable]
public class FalloffNoiseGenerator {
  public Vector2 mapSize = Vector3.one * 16000f;
  public int seed = 2;
  public float scale = 5.5f;
  public float gain = 0.5f;
  public float lacunarity = 2f;
  public int octaves = 9;

  public AnimationCurve falloffGradientCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);
  public AnimationCurve falloffOutputCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

  public float[] GenerateNoise(FastNoiseChunk chunk, float scale, int seed) {
    // Create copies of the curves (for thread safety)
    AnimationCurve falloffGradientCurve = new AnimationCurve(this.falloffGradientCurve.keys);
    AnimationCurve falloffOutputCurve = new AnimationCurve(this.falloffOutputCurve.keys);

    // Noise used to deform the falloff map
    FastNoise falloffNoise = new FastNoise("FractalFBm");
    falloffNoise.Set("Source", new FastNoise("Simplex"));
    falloffNoise.Set("Gain", gain);
    falloffNoise.Set("Lacunarity", lacunarity);
    falloffNoise.Set("Octaves", octaves);
    float[] falloffNoiseGrid = null;

    // Generate the falloff noise texture
    falloffNoiseGrid = chunk.GenerateGrid(
      false,
      falloffNoise,
      seed + this.seed,
      scale * this.scale
    );

    // Generate the final falloff map
    float[] falloffOutputGrid = new float[chunk.resolution.x * chunk.resolution.z];
    for (int _y = 0; _y < chunk.resolution.z; _y++) {
      for (int _x = 0; _x < chunk.resolution.x; _x++) {
        // Transform the coordinates
        int _index2D = _y * chunk.resolution.x + _x;
        float localX = ((float)_x / chunk.resolution.x) * chunk.scale.x;
        float localY = ((float)_y / chunk.resolution.z) * chunk.scale.z;

        // Clamped coordinates for creating the falloff map
        float posX = ((chunk.position.x + localX) / mapSize.x) * 0.5f;
        posX = Mathf.Clamp01(Math.Abs(posX));
        float posY = ((chunk.position.z + localY) / mapSize.y) * 0.5f;
        posY = Mathf.Clamp01(Math.Abs(posY));

        // Create the falloff map
        float falloff = 1f - (1f - posX * posX) * (1f - posY * posY);
        float curvedFalloff = 1f - falloffGradientCurve.Evaluate(falloff);

        // Sample and normalize the noise
        float falloffNoiseSample = TextureUtils.Normalize(falloffNoiseGrid[_index2D]);

        // Combine the falloff map and the noise
        float finalFalloff = falloffNoiseSample * curvedFalloff;
        finalFalloff = falloffOutputCurve.Evaluate(finalFalloff);
        falloffOutputGrid[_index2D] = finalFalloff;
      }
    }

    return falloffOutputGrid;
  }
}
