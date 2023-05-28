using UnityEngine;
using System;

public class TerrainNoise : ISamplerFactory {
  // Materials
  public Color grassColor = Color.green;
  public Color darkGrassColor = Color.Lerp(Color.green, Color.black, 0.5f);
  public Color snowColor = Color.white;
  public Color dirtColor = Color.yellow;
  public Color sandColor = Color.yellow;

  // Heights
  public float snowHeight = 100f;
  public float sandHeight = 10f;

  // Noise
  public float noiseSize = 32f;
  public int noiseOctaves = 5;
  public bool updateChunksInEditor = true;

  // Curves
  public AnimationCurve curve;
  public AnimationCurve normalizerCurve;

  private int seed = 0;

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
      // Generate the noise inside the sampler the first time it's called
      if (noiseGrid == null) {
        int gridLengthX = chunk.resolution.x + 1;
        int gridLengthY = chunk.resolution.y + 1;
        int gridLengthZ = chunk.resolution.z + 1;
        int gridSizeNormalizer = Mathf.RoundToInt(chunkWorldSize.x / 32f);

        // Generate the base terrain noise
        noiseGrid = new float[gridLengthX * gridLengthY * gridLengthZ];
        noise.GenUniformGrid3D(
          noiseGrid,
          Mathf.RoundToInt(chunkWorldPosition.z / gridSizeNormalizer),
          Mathf.RoundToInt(0),
          Mathf.RoundToInt(chunkWorldPosition.x / gridSizeNormalizer),
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

      // Start sampling
      float height = point.position.y * inverseChunkWorldSize.y;

      // Add terrain noise
      // height -= Normalize(noise.GetNoise(finalX, finalY, finalZ));
      // height -= Normalize(noise.GenSingle3D(finalX, finalY, finalZ, seed));
      height -= Normalize(noiseGrid[point.index]);
      // height += ((caves.GetNoise(finalX, finalZ) + 1f) / 2f) * 0.1f;
      // height += 1f - Mathf.Abs(noise.GetNoise(finalX, 0, finalZ));

      // Caves
      // float caves3D = cavesNoise.GetNoise(finalX, finalY, finalZ) + 0.2f;
      // height = Denormalize(
      //   Normalize(normalizerCurve.Evaluate(height)) + Normalize(normalizerCurve.Evaluate(caves3D))
      // );

      point.value = height;
      return point;
    };

    postProcessingFunc = (CubeGrid grid) => {
      Color black = Color.black;

      for (int z = 0; z < grid.gridSize.z; z++) {
        for (int y = 0; y < grid.gridSize.y; y++) {
          for (int x = 0; x < grid.gridSize.x; x++) {
            int index = grid.GetIndexFromCoords(x, y, z);
            CubeGridPoint point = grid.gridPoints[index];

            float height = point.position.y + chunkWorldPosition.y;

            if (height >= snowHeight) {
              point.color = snowColor;
            } else if (height <= sandHeight) {
              point.color = sandColor;
            } else {
              float normalizedGrassHeight = Mathf.InverseLerp(sandHeight, snowHeight, height);
              point.color = Color.Lerp(grassColor, darkGrassColor, normalizedGrassHeight);
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
