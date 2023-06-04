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

  [Header("Heights")]
  public float snowHeight = 100f;
  public float sandHeight = 10f;

  [Header("Base Noise Settings")]
  public int seed = 0;
  public float noiseSize = 32f;
  public int noiseOctaves = 5;
  public bool updateChunksInEditor = true;

  [Header("Falloff Settings")]
  public float seaLevel = 0.5f;
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

  public override void GetSampler(
    TerrainChunk chunk,
    out CubeGridSamplerFunc samplerFunc,
    out CubeGridPostProcessingFunc postProcessingFunc
  ) {
    // Create copies of the curves
    AnimationCurve curve = new AnimationCurve(this.curve.keys);
    AnimationCurve normalizerCurve = new AnimationCurve(this.normalizerCurve.keys);
    AnimationCurve falloffGradientCurve = new AnimationCurve(this.falloffGradientCurve.keys);
    AnimationCurve falloffOutputCurve = new AnimationCurve(this.falloffOutputCurve.keys);

    // Main noise for the terrain
    // FastNoiseLite noise = new FastNoiseLite(seed + 1);
    // noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
    // noise.SetFrequency(1f);
    // noise.SetFractalType(FastNoiseLite.FractalType.FBm);
    // noise.SetFractalOctaves(noiseOctaves);
    FastNoise noise = new FastNoise("FractalFBm");
    noise.Set("Source", new FastNoise("Simplex"));
    noise.Set("Gain", 0.5f);
    noise.Set("Lacunarity", 2f);
    noise.Set("Octaves", noiseOctaves);
    float[] noiseGrid = null;

    // Noise used to deform the falloff map
    FastNoise falloffNoise = new FastNoise("FractalFBm");
    falloffNoise.Set("Source", new FastNoise("Simplex"));
    falloffNoise.Set("Gain", 0.5f);
    falloffNoise.Set("Lacunarity", 2f);
    falloffNoise.Set("Octaves", falloffNoiseOctaves);
    float[] falloffNoiseGrid = null;

    // Debug color texture
    float[] debugFalloff = null;

    // FastNoiseLite cavesNoise = new FastNoiseLite(seed);
    // cavesNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
    // cavesNoise.SetFrequency(8f / noiseMultiplier);

    // Noise for a cave system
    // FastNoiseLite cavesNoise = new FastNoiseLite(seed);
    // cavesNoise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
    // cavesNoise.SetFrequency(4f);
    // cavesNoise.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.EuclideanSq);
    // cavesNoise.SetCellularReturnType(FastNoiseLite.CellularReturnType.Distance);
    // cavesNoise.SetCellularJitter(1f);
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

    samplerFunc = (CubeGridPoint point) => {
      int gridLengthX = chunk.resolution.x + 1;
      int gridLengthY = chunk.resolution.y + 1;
      int gridLengthZ = chunk.resolution.z + 1;

      // Generate the noise inside the sampler the first time it's called
      if (noiseGrid == null) {
        int gridSizeNormalizer = Mathf.RoundToInt(chunkWorldSize.x / 32f);

        int xStart = Mathf.RoundToInt(chunkWorldPosition.x / gridSizeNormalizer);
        int yStart = 0;
        int zStart = Mathf.RoundToInt(chunkWorldPosition.z / gridSizeNormalizer);

        // Generate the falloff noise texture
        if (useFalloff) {
          falloffNoiseGrid = new float[gridLengthX * gridLengthZ];
          noise.GenUniformGrid2D(
            falloffNoiseGrid,
            xStart,
            zStart,
            gridLengthX,
            gridLengthZ,
            (noiseMultiplier * gridSizeNormalizer) / falloffNoiseSize,
            seed + 2
          );

          if (useFalloffAsColor) {
            debugFalloff = new float[gridLengthX * gridLengthZ];
          }
        }

        // Generate the base terrain noise
        noiseGrid = new float[gridLengthX * gridLengthY * gridLengthZ];
        noise.GenUniformGrid3D(
          noiseGrid,
          zStart,
          yStart,
          xStart,
          gridLengthX,
          gridLengthY,
          gridLengthZ,
          noiseMultiplier * gridSizeNormalizer,
          seed
        );
      }

      // Coordinates to sample the point in world space
      // float finalX = ((chunkWorldPosition.x + point.position.x) * noiseMultiplier) + chunk.noiseOffset.x;
      // float finalY = (point.position.y * noiseMultiplier) + chunk.noiseOffset.y;
      // float finalZ = ((chunkWorldPosition.z + point.position.z) * noiseMultiplier) + chunk.noiseOffset.z;

      // 3d coords inside the 3d grid
      int x = point.index / (gridLengthY * gridLengthX);
      // int y = (point.index / gridLengthX) % gridLengthY;
      int z = point.index % gridLengthX;

      // 2d coords inside 2d grids
      int index2D = z * gridLengthX + x;

      // Start sampling
      float output = 0;
      float heightGradient = point.position.y * inverseChunkWorldSize.y;

      if (useFalloff) {
        // Clamped coordinates for creating the falloff texture
        float posX = (chunkWorldPosition.x + point.position.x) / mapSize.x / 2f;
        posX = Mathf.Clamp01(Math.Abs(posX));
        float posY = (chunkWorldPosition.z + point.position.z) / mapSize.y / 2f;
        posY = Mathf.Clamp01(Math.Abs(posY));

        // Create the falloff map
        float falloff = 1f - (1f - posX * posX) * (1f - posY * posY);
        float curvedFalloff = 1f - falloffGradientCurve.Evaluate(falloff);

        // Sample and normalize the noise
        float falloffNoiseSample = Normalize(falloffNoiseGrid[index2D]);

        // Combine the falloff map and the noise
        float finalFalloff = falloffNoiseSample * curvedFalloff;
        finalFalloff = falloffOutputCurve.Evaluate(finalFalloff);

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
        float heightAboveSeaLevel = heightGradient - 0.5f - (Normalize(noiseGrid[point.index]) * 0.5f);
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
        output = heightGradient - Normalize(noiseGrid[point.index]);
      }

      // Add terrain noise
      // height -= Normalize(noise.GetNoise(finalX, finalY, finalZ));
      // height -= Normalize(noise.GenSingle3D(finalX, finalY, finalZ, seed));
      // height -= Normalize(noiseGrid[point.index]);

      // height += ((caves.GetNoise(finalX, finalZ) + 1f) / 2f) * 0.1f;
      // height += 1f - Mathf.Abs(noise.GetNoise(finalX, 0, finalZ));

      // Caves
      // float caves3D = cavesNoise.GetNoise(finalX, finalY, finalZ) + 0.2f;
      // height = Denormalize(
      //   Normalize(normalizerCurve.Evaluate(height)) + Normalize(normalizerCurve.Evaluate(caves3D))
      // );

      point.value = output;
      return point;
    };

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
              float height = point.position.y + chunkWorldPosition.y;

              if (height >= snowHeight) {
                point.color = snowColor;
              } else if (height <= sandHeight) {
                point.color = sandColor;
              } else {
                float normalizedGrassHeight = Mathf.InverseLerp(sandHeight, snowHeight, height);
                point.color = Color.Lerp(grassColor, darkGrassColor, normalizedGrassHeight);
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
