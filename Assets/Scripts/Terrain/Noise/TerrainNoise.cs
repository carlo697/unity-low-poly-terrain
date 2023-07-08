using UnityEngine;
using System;

public class TerrainNoise : ISamplerFactory {
  [Header("Size")]
  public Vector2 mapSize = Vector3.one * 16000f;

  [Header("General Curves")]
  public AnimationCurve curve;
  public AnimationCurve normalizerCurve;

  [Header("Materials")]
  public Color grassColor = Color.green;
  public Color darkGrassColor = Color.Lerp(Color.green, Color.black, 0.5f);
  public Color snowColor = Color.white;
  public Color dirtColor = Color.yellow;
  public Color sandColor = Color.yellow;
  public Color darkSandColor = Color.Lerp(Color.yellow, Color.black, 0.5f);

  [Header("Heights")]
  public float seaLevel = 0.5f;
  public float snowHeight = 100f;
  public float sandHeight = 10f;

  [Header("Base Noise Settings")]
  public int seed = 0;
  public float noiseSize = 32f;
  public int noiseOctaves = 5;
  public bool updateChunksInEditor = true;

  [Header("Falloff Settings")]
  public float seaBorderBeforeThreshold = 0.05f;
  public float seaBorderAfterThreshold = 0.1f;
  public float landGap = 0.1f;
  public bool useFalloff;
  public float falloffNoiseSize = 1f;
  public int falloffNoiseOctaves = 8;
  public AnimationCurve falloffGradientCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);
  public AnimationCurve falloffOutputCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

  [Header("Debug")]
  public bool useFalloffAsColor;

  private float Normalize(float value) {
    return ((value + 1f) / 2f);
  }

  private float Denormalize(float value) {
    return (value * 2f) - 1f;
  }

  public float[] GenerateChunkNoisePixels(
    bool is3D,
    TerrainChunk chunk,
    FastNoise noise,
    int seed,
    float frequencyMultiplier = 1f
  ) {
    // Variables needed to sample the point in world space
    int gridSizeNormalizer = Mathf.RoundToInt(chunk.size.x / 32f);

    // Calculate offset
    float offsetX = chunk.noisePosition.x / gridSizeNormalizer;
    float offsetY = chunk.noisePosition.z / gridSizeNormalizer;

    float noiseMultiplier = 1 / (chunk.noiseSize * this.noiseSize);
    float noiseSize = noiseMultiplier * frequencyMultiplier;
    float frequency = noiseSize * gridSizeNormalizer;

    // Apply offset
    FastNoise offsetNoise = new FastNoise("Domain Offset");
    offsetNoise.Set("Source", noise);
    if (is3D) {
      offsetNoise.Set("OffsetX", chunk.noisePosition.z * noiseSize);
      offsetNoise.Set("OffsetY", 0f);
      offsetNoise.Set("OffsetZ", chunk.noisePosition.x * noiseSize);
    } else {
      offsetNoise.Set("OffsetX", chunk.noisePosition.x * noiseSize);
      offsetNoise.Set("OffsetY", chunk.noisePosition.z * noiseSize);
    }

    // Apply scale
    FastNoise scaleNoise = new FastNoise("Domain Axis Scale");
    scaleNoise.Set("Source", offsetNoise);
    scaleNoise.Set("ScaleX", frequency);
    scaleNoise.Set("ScaleY", frequency);
    scaleNoise.Set("ScaleZ", frequency);

    float[] pixels;
    if (is3D) {
      pixels = new float[chunk.gridSize.x * chunk.gridSize.y * chunk.gridSize.z];
      scaleNoise.GenUniformGrid3D(
        pixels,
        0,
        0,
        0,
        chunk.gridSize.x,
        chunk.gridSize.y,
        chunk.gridSize.x,
        1f,
        seed
      );
    } else {
      pixels = new float[chunk.gridSize.x * chunk.gridSize.z];
      scaleNoise.GenUniformGrid2D(
        pixels,
        0,
        0,
        chunk.gridSize.x,
        chunk.gridSize.z,
        1f,
        seed
      );
    }

    return pixels;
  }

  public float[] GenerateFalloffPixels(TerrainChunk chunkData) {
    // Create copies of the curves (for thread safety)
    AnimationCurve falloffGradientCurve = new AnimationCurve(this.falloffGradientCurve.keys);
    AnimationCurve falloffOutputCurve = new AnimationCurve(this.falloffOutputCurve.keys);

    // Noise used to deform the falloff map
    FastNoise falloffNoise = new FastNoise("FractalFBm");
    falloffNoise.Set("Source", new FastNoise("Simplex"));
    falloffNoise.Set("Gain", 0.5f);
    falloffNoise.Set("Lacunarity", 2f);
    falloffNoise.Set("Octaves", falloffNoiseOctaves);
    float[] falloffNoiseGrid = null;

    // Generate the falloff noise texture
    falloffNoiseGrid = GenerateChunkNoisePixels(false, chunkData, falloffNoise, seed + 2, 1f / falloffNoiseSize);

    // Generate the final falloff map
    float[] falloffOutputGrid = new float[chunkData.gridSize.x * chunkData.gridSize.z];
    for (int _y = 0; _y < chunkData.gridSize.z; _y++) {
      for (int _x = 0; _x < chunkData.gridSize.x; _x++) {
        // Transform the coordinates
        int _index2D = _y * chunkData.gridSize.x + _x;
        float localX = ((float)_x / chunkData.resolution.x) * chunkData.size.x;
        float localY = ((float)_y / chunkData.resolution.z) * chunkData.size.z;

        // Clamped coordinates for creating the falloff map
        float posX = ((chunkData.noisePosition.x + localX) / mapSize.x) * 0.5f;
        posX = Mathf.Clamp01(Math.Abs(posX));
        float posY = ((chunkData.noisePosition.z + localY) / mapSize.y) * 0.5f;
        posY = Mathf.Clamp01(Math.Abs(posY));

        // Create the falloff map
        float falloff = 1f - (1f - posX * posX) * (1f - posY * posY);
        float curvedFalloff = 1f - falloffGradientCurve.Evaluate(falloff);

        // Sample and normalize the noise
        float falloffNoiseSample = Normalize(falloffNoiseGrid[_index2D]);

        // Combine the falloff map and the noise
        float finalFalloff = falloffNoiseSample * curvedFalloff;
        finalFalloff = falloffOutputCurve.Evaluate(finalFalloff);
        falloffOutputGrid[_index2D] = finalFalloff;
      }
    }

    return falloffOutputGrid;
  }

  public float[] GenerateBaseTerrainPixels(TerrainChunk chunkData) {
    FastNoise noise = new FastNoise("FractalFBm");
    noise.Set("Source", new FastNoise("Simplex"));
    noise.Set("Gain", 0.5f);
    noise.Set("Lacunarity", 2f);
    noise.Set("Octaves", noiseOctaves);
    return GenerateChunkNoisePixels(true, chunkData, noise, seed);
  }

  public override void GetSampler(
    TerrainChunk chunk,
    out CubeGridSamplerFunc samplerFunc,
    out CubeGridPostProcessingFunc postProcessingFunc
  ) {
    // Create copies of the curves
    AnimationCurve curve = new AnimationCurve(this.curve.keys);
    AnimationCurve normalizerCurve = new AnimationCurve(this.normalizerCurve.keys);

    // Pixels of noises
    float[] baseTerrainPixels = null;
    float[] falloffPixels = null;

    // Debug pixels
    float[] debugFalloff = null;

    samplerFunc = (CubeGridPoint point) => {
      // Generate the noise inside the sampler the first time it's called
      if (baseTerrainPixels == null) {
        // Generate the falloff map
        if (useFalloff) {
          falloffPixels = GenerateFalloffPixels(chunk);

          if (useFalloffAsColor) {
            debugFalloff = new float[chunk.gridSize.x * chunk.gridSize.z];
          }
        }

        // Generate the base terrain noise
        baseTerrainPixels = GenerateBaseTerrainPixels(chunk);
      }

      // Coords for 2d maps
      int index2D = point.Get2dIndex(chunk);

      // Start sampling
      float output = 0;
      float heightGradient = point.position.y * chunk.inverseSize.y;

      if (useFalloff) {
        // Sample the falloff map
        float finalFalloff = falloffPixels[index2D];

        // Land gradient
        float landGradient;
        float start = seaLevel;
        if (finalFalloff <= start) {
          landGradient = 0f;
        } else {
          landGradient = Mathf.SmoothStep(0f, 1f, (finalFalloff - start) / (landGap));
        }

        // Use the land gredient to combine the base terrain noise with the falloff map
        float heightBelowSeaLevel = heightGradient - finalFalloff;
        float heightAboveSeaLevel =
          heightGradient - seaLevel - (Normalize(baseTerrainPixels[point.index]) * (1f - seaLevel));
        output = Mathf.Lerp(heightBelowSeaLevel, heightAboveSeaLevel, landGradient);

        if (useFalloffAsColor) {
          debugFalloff[index2D] = 1f - output;
        }

        // height = Mathf.Lerp(heightGradient, height, finalFalloff);
        // height = Mathf.Lerp(heightGradient, height, finalFalloff);
        // height = heightGradient - finalFalloff;
        // height = heightGradient - landGradient;
        // height = heightGradient - (landGradient * 0.8f);
        // height = Mathf.Lerp(height, heightGradient - seaLevel, borderGradient);
      } else {
        output = heightGradient - Normalize(baseTerrainPixels[point.index]);
      }

      point.value = output;
      return point;
    };

    // Add color to the grid volume
    postProcessingFunc = (CubeGrid grid) => {
      Color black = Color.black;

      for (int z = 0; z < grid.gridSize.z; z++) {
        for (int y = 0; y < grid.gridSize.y; y++) {
          for (int x = 0; x < grid.gridSize.x; x++) {
            int index = grid.GetIndexFromCoords(x, y, z);
            CubeGridPoint point = grid.gridPoints[index];

            if (useFalloff && useFalloffAsColor) {
              int index2D = z * grid.gridSize.x + x;
              point.color = Color.Lerp(Color.black, Color.white, debugFalloff[index2D]);
            } else {
              float normalizedHeight = point.position.y / chunk.size.y;

              if (normalizedHeight >= snowHeight) {
                point.color = snowColor;
              } else if (normalizedHeight <= sandHeight) {
                float t = Mathf.InverseLerp(0f, sandHeight, normalizedHeight);
                point.color = Color.Lerp(darkSandColor, sandColor, t);
              } else {
                float t = Mathf.InverseLerp(sandHeight, snowHeight, normalizedHeight);
                point.color = Color.Lerp(grassColor, darkGrassColor, t);
              }
            }

            grid.gridPoints[index] = point;
          }
        }
      }
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
