using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Terrain/Shape", order = 0)]
public class TerrainShape : ScriptableObject {
  [Header("Size")]
  public Vector2 mapSize = Vector3.one * 16000f;

  [Header("General Curves")]
  public AnimationCurve curve;
  public AnimationCurve normalizerCurve;

  [Header("Materials")]
  public uint grassId = 1;
  public Color grassColor = Color.green;
  public Color darkGrassColor = Color.Lerp(Color.green, Color.black, 0.5f);
  public uint rockId = 2;
  public Color rockColor = new Color(0.5f, 0.5f, 0.5f);
  public uint sandId = 3;
  public Color sandColor = Color.yellow;
  public Color wetSandColor = Color.yellow;
  public Color darkSandColor = Color.Lerp(Color.yellow, Color.black, 0.5f);
  public uint snowId = 4;
  public Color snowColor = Color.white;

  [Header("Heights")]
  public float seaLevel = 0.5f;
  public float snowHeight = 100f;
  public float sandHeight = 10f;

  [Header("Base Noise Settings")]
  public int terrainSeed = 0;
  public float noiseScale = 1500f;
  public FractalNoise baseNoise = new FractalNoise {
    seed = 0,
    scale = 1f,
    octaves = 8
  };
  public bool updateChunksInEditor = true;

  [Header("Falloff Settings")]
  public bool useFalloff = true;
  public FalloffNoise landMask = new FalloffNoise();
  public FractalNoise landGradientSteepness = new FractalNoise {
    seed = 3,
    scale = 1f,
    noiseType = NoiseType.OpenSimplex2S,
    fractalType = FractalType.FractalFBm,
    octaves = 3
  };

  [Header("Biome Settings")]
  public Biome defaultBiome;
  public bool useBiomes = true;
  public BiomeArray biomes;
  public float biomesNoiseScale = 0.75f;
  public float biomesVoronoiRandomness = 0.5f;
  public FractalNoise biomeWarp = new FractalNoise {
    seed = 4,
    scale = 4f,
    amplitude = 1.5f,
    noiseType = NoiseType.OpenSimplex2S,
    octaves = 6,
    fractalType = FractalType.FractalFBm
  };
  public TemperatureNoise averageTemperature;
  public PrecipitationNoise annualPrecipitation;

  [Header("Plateaus")]
  public bool usePlateaus = true;
  public float absoluteMaximunPlateauHeight = 48f;
  public FractalNoise plateauMask = new FractalNoise {
    seed = 20,
    scale = 1f,
    noiseType = NoiseType.OpenSimplex2S,
    octaves = 2,
    useCurve = true
  };
  public FractalNoise plateauShape = new FractalNoise {
    seed = 22,
    scale = 0.15f,
    noiseType = NoiseType.OpenSimplex2S,
    octaves = 5,
    useCurve = true
  };
  public FractalNoise plateauGround = new FractalNoise {
    seed = 24,
    scale = 2.5f,
    noiseType = NoiseType.OpenSimplex2S,
    octaves = 4
  };

  [Header("Details")]
  public bool useDetails = true;
  public DetailSpawner[] detailSpawners = new DetailSpawner[] { };

  [Header("Grass")]
  public bool useGrass = true;
  public GrassSpawner[] grassSpawners = new GrassSpawner[] { };

  [Header("Debug")]
  public float debugPixelsMultiplier = 1f;
  public int debugBiomeIndex;
  public DebugMode debugMode = DebugMode.None;

  public enum DebugMode {
    None,
    Normals,
    Slope,
    Noise,
    BiomeMasks,
    Custom2d,
    AverageTemperature,
    AnnualPrecipitation,
    TemperatureAndPrecipitation
  }

  public TerrainSamplerFunc GetSampler(FastNoiseChunk chunk) {
    return (VoxelGrid grid) => {
      int pixelCount2d = chunk.resolution.x * chunk.resolution.z;

      Biome[] biomes = this.biomes;

      // Create copies of the curves
      AnimationCurve curve = new AnimationCurve(this.curve.keys);
      AnimationCurve normalizerCurve = new AnimationCurve(this.normalizerCurve.keys);

      TemperatureNoise.Generator temperatureGenerator = averageTemperature.GetGenerator();
      PrecipitationNoise.Generator precipitationGenerator = annualPrecipitation.GetGenerator();

      // Determine the biomes in this chunk
      Dictionary<Biome, float[]> selectedBiomes = null;
      if (useBiomes || debugMode == DebugMode.BiomeMasks) {
        selectedBiomes = BiomeSelector.DetermineBiomes(
          chunk,
          terrainSeed,
          biomes,
          noiseScale,
          noiseScale * biomesNoiseScale,
          biomesVoronoiRandomness,
          biomeWarp.GetGenerator(),
          temperatureGenerator,
          precipitationGenerator
        );
      }

      // Array to store the debug pixels
      float[] debug2dPixels = null;
      Color[] debug2dColors = null;
      if (debugMode == DebugMode.Custom2d || debugMode == DebugMode.BiomeMasks) {
        debug2dPixels = new float[pixelCount2d];
      } else if (debugMode == DebugMode.AverageTemperature) {
        debug2dPixels = temperatureGenerator.GenerateGrid2d(chunk, noiseScale, terrainSeed);
      } else if (debugMode == DebugMode.AnnualPrecipitation) {
        debug2dPixels = precipitationGenerator.GenerateGrid2d(chunk, noiseScale, terrainSeed);
      } else if (debugMode == DebugMode.TemperatureAndPrecipitation) {
        debug2dColors = new Color[pixelCount2d];
        float[] precipitationPixels = precipitationGenerator.GenerateGrid2d(chunk, noiseScale, terrainSeed);
        float[] temperaturePixels = temperatureGenerator.GenerateGrid2d(chunk, noiseScale, terrainSeed);

        for (int i = 0; i < pixelCount2d; i++) {
          float temperature = temperatureGenerator.Normalize(temperaturePixels[i]);
          float precipitation = precipitationGenerator.Normalize(precipitationPixels[i]);
          debug2dColors[i] = new Color(temperature, precipitation, 0f);
        }
      }

      // Debug the masks for biomes
      if (debugMode == DebugMode.BiomeMasks) {
        // Find in the array the biome to debug
        Biome debugBiome = null;
        if (debugBiomeIndex >= 0 && debugBiomeIndex < biomes.Length) {
          debugBiome = biomes[debugBiomeIndex];
        }

        if (debugBiome == null) {
          // If the provided biome index is not valid, then we'll just sum up the masks of all biomes
          foreach (var (biome, mask) in selectedBiomes) {
            for (int i = 0; i < mask.Length; i++) {
              debug2dPixels[i] = Mathf.Clamp01(debug2dPixels[i] + mask[i]);
            }
          }
        } else if (debugBiome && selectedBiomes.ContainsKey(debugBiome)) {
          // The biome was found so we'll use its mask
          debug2dPixels = selectedBiomes[debugBiome];
        } else {
          // The biome is not present, we'll use an empty mask
          debug2dPixels = new float[grid.size.x * grid.size.z];
        }
      }

      if (useBiomes) {
        // The the voxel grids for each biome
        Dictionary<Biome, VoxelGrid> grids = new();

        // We create these arrays so we don't need to use a foreach to iterate over
        // the biomes
        int biomeCount = selectedBiomes.Count;
        float[][] masksArray = new float[biomeCount][];
        VoxelGrid[] gridsArray = new VoxelGrid[biomeCount];

        int biomeArrayIndex = 0;
        foreach (var (biome, mask) in selectedBiomes) {
          // Initialize the grid
          VoxelGrid biomeGrid = new VoxelGrid(grid.scale, grid.resolution, grid.threshold);
          biomeGrid.CopyPointsFrom(grid);
          grids[biome] = biomeGrid;

          // Add the biome to the array
          masksArray[biomeArrayIndex] = mask;
          gridsArray[biomeArrayIndex] = biomeGrid;

          // Generate the data
          biome.Generate(this, chunk, biomeGrid, mask);
          biomeArrayIndex++;
        }

        // Blend all the biomes into the final grid
        for (int pointIndex = 0; pointIndex < grid.totalPointCount; pointIndex++) {
          ref VoxelPoint finalPoint = ref grid.points[pointIndex];

          // Coords for 2d maps
          Vector3Int coords = grid.GetCoordsFromIndex(pointIndex);
          int pointIndex2D = coords.z * grid.size.x + coords.x;

          // Values needed to calculate the average data for this point
          float weightSum = 0f;
          float maximunWeight = float.MinValue;
          int prominentBiomeIndex = 0;

          // Sum all the weights and find the index of the biome with the highest weight
          for (int biomeIndex = 0; biomeIndex < biomeCount; biomeIndex++) {
            float weight = masksArray[biomeIndex][pointIndex2D];
            weightSum += weight;

            if (weight > maximunWeight) {
              maximunWeight = weight;
              prominentBiomeIndex = biomeIndex;
            }
          }

          // We'll multiply each weight by this value so that the sum of weights equal one
          float inverseWeightSum = 1f / weightSum;

          // Final values for the point
          float value = 0f;
          Color color = default;
          float roughness = 0f;
          uint material = gridsArray[prominentBiomeIndex].points[pointIndex].material;

          // Iterate the pixel on the biomes to get averages
          for (int biomeIndex = 0; biomeIndex < biomeCount; biomeIndex++) {
            float weight = masksArray[biomeIndex][pointIndex2D] * inverseWeightSum;

            if (weight <= 0f) {
              continue;
            }

            ref VoxelPoint biomePoint = ref gridsArray[biomeIndex].points[pointIndex];
            value += biomePoint.value * weight;
            color += biomePoint.color * weight;
            roughness += biomePoint.roughness * weight;
          }

          // Set the final values
          finalPoint.value = value;
          finalPoint.color = color;
          finalPoint.roughness = roughness;
          finalPoint.material = material;
        }

        // for (int index = 0; index < grid.totalPointCount; index++) {
        //   ref VoxelPoint point = ref grid.points[index];

        //   // Start sampling
        //   float output = 0;
        //   float heightGradient = point.position.y / chunk.scale.y;
        //   output = heightGradient - float.Epsilon;

        //   // Set the density
        //   point.value = output;
        //   point.color = Color.black;
        //   point.roughness = 0f;
        //   point.material = grassId;
        // };

        // Dispose the grids created for the biomes
        foreach (var item in grids) {
          item.Value.Dispose();
        }
      } else {
        // Create a mask fill with 1f
        float[] mask = new float[pixelCount2d];
        for (int i = 0; i < mask.Length; i++) {
          mask[i] = 1f;
        }

        // Generate the default biome on the grid
        defaultBiome.Generate(this, chunk, grid, mask);
      }

      if (debugMode != DebugMode.None) {
        if (debugMode == DebugMode.Noise) {
          INoiseGenerator baseTerrainGenerator = baseNoise.GetGenerator();
          float[] baseTerrainPixels = baseTerrainGenerator.GenerateGrid3d(chunk, noiseScale, terrainSeed);

          for (int index = 0; index < grid.totalPointCount; index++) {
            ref VoxelPoint point = ref grid.points[index];
            point.value = baseTerrainPixels[index] * -1f;
          }
        }

        // Initialize colors
        for (int index = 0; index < grid.totalPointCount; index++) {
          ref VoxelPoint point = ref grid.points[index];

          Vector3Int coords = grid.GetCoordsFromIndex(index);
          int index2D = coords.z * grid.size.x + coords.x;

          Vector3 normal = grid.GetPointNormalApproximation(coords.x, coords.y, coords.z);

          switch (debugMode) {
            case DebugMode.Normals:
              point.color = new Color(normal.x, normal.y, normal.z) * debugPixelsMultiplier;
              break;
            case DebugMode.Slope:
              point.color = Color.white * normal.y * debugPixelsMultiplier;
              break;
            case DebugMode.Noise:
              point.color = Color.white * debugPixelsMultiplier;
              break;
            case DebugMode.BiomeMasks:
              point.color = Color.white * debug2dPixels[index2D] * debugPixelsMultiplier;
              break;
            case DebugMode.AverageTemperature:
              float temperature = temperatureGenerator.Normalize(debug2dPixels[index2D]);
              point.color = Color.white * temperature * debugPixelsMultiplier;
              break;
            case DebugMode.AnnualPrecipitation:
              float precipitation = precipitationGenerator.Normalize(debug2dPixels[index2D]);
              point.color = Color.white * precipitation * debugPixelsMultiplier;
              break;
            case DebugMode.TemperatureAndPrecipitation:
              point.color = debug2dColors[index2D] * debugPixelsMultiplier;
              break;
            default:
              break;
          }
        };
      }
    };
  }

  private void OnValidate() {
    if (updateChunksInEditor) {
      TerrainChunk[] chunks = GameObject.FindObjectsOfType<TerrainChunk>();
      foreach (var chunk in chunks) {
        if (chunk.terrainShape == this) {
          chunk.GenerateOnEditor();
        }
      }
    }
  }
}
