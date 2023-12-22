using UnityEngine;

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
  public FractalNoiseGenerator baseNoise = new FractalNoiseGenerator {
    seed = 0,
    scale = 1f,
    octaves = 8
  };
  public bool updateChunksInEditor = true;

  [Header("Falloff Settings")]
  public bool useFalloff = true;
  public FalloffNoiseGenerator falloffMask = new FalloffNoiseGenerator {
    seed = 2,
    scale = 5.5f,
    octaves = 8
  };
  public FractalNoiseGenerator landGradientSteepness = new FractalNoiseGenerator {
    seed = 3,
    scale = 1f,
    noiseType = NoiseType.OpenSimplex2S,
    fractalType = FractalType.FractalFBm,
    octaves = 3
  };

  [Header("Plateaus")]
  public bool usePlateaus = true;
  public float absoluteMaximunPlateauHeight = 48f;
  public FractalNoiseGenerator plateauMask = new FractalNoiseGenerator {
    seed = 20,
    scale = 1f,
    noiseType = NoiseType.OpenSimplex2S,
    octaves = 2,
    useCurve = true
  };
  public FractalNoiseGenerator plateauShape = new FractalNoiseGenerator {
    seed = 22,
    scale = 0.15f,
    noiseType = NoiseType.OpenSimplex2S,
    octaves = 5,
    useCurve = true
  };
  public FractalNoiseGenerator plateauGround = new FractalNoiseGenerator {
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
  public float debugHeightmapMultiplier = 1f;
  public DebugMode debugMode = DebugMode.None;

  public enum DebugMode {
    None,
    Value,
    Normals,
    Slope,
    Falloff,
    LandGradient,
    OceanGradient,
    OceanAndLandGradient,
    LandGradientSteepness,
    PlateauMask,
    PlateauShape,
    PlateauShapeAndMask,
    PlateauGround,
    Noise,
  }

  public CubeGridSamplerFunc GetSampler(FastNoiseChunk chunk) {
    return (CubeGrid grid) => {
      // Create copies of the curves
      AnimationCurve curve = new AnimationCurve(this.curve.keys);
      AnimationCurve normalizerCurve = new AnimationCurve(this.normalizerCurve.keys);

      // Debug pixels
      float[] debug2dPixels = null;
      if (debugMode == DebugMode.Falloff
        || debugMode == DebugMode.LandGradient
        || debugMode == DebugMode.OceanGradient
        || debugMode == DebugMode.OceanAndLandGradient
        || debugMode == DebugMode.LandGradientSteepness
      ) {
        debug2dPixels = new float[chunk.resolution.x * chunk.resolution.z];
      }

      // Generate the base terrain noise
      TerrainNoiseGenerator baseTerrainGenerator = baseNoise.GetGenerator();
      float[] baseTerrainPixels = baseTerrainGenerator.GenerateGrid3d(chunk, noiseScale, terrainSeed);

      // Generate the falloff map and the gradient maps
      float[] falloffPixels = null;
      float[] landGradientSteepnessPixels = null;
      float[] landGradientPixels = null;
      float[] oceanGradientPixels = null;
      if (useFalloff) {
        falloffPixels = falloffMask.GenerateNoise(chunk, noiseScale, terrainSeed);
        landGradientSteepnessPixels =
          landGradientSteepness.GetGenerator().GenerateGrid2d(chunk, noiseScale, terrainSeed);

        landGradientPixels = new float[falloffPixels.Length];
        oceanGradientPixels = new float[falloffPixels.Length];

        for (int i = 0; i < landGradientPixels.Length; i++) {
          float falloff = falloffPixels[i];
          float landGradientSteepness = landGradientSteepnessPixels[i];

          // Get the land gradient from the falloff
          float landGradient;
          if (falloff <= seaLevel) {
            landGradient = 0f;
          } else {
            // landGradient = Mathf.SmoothStep(0f, 1f, (falloff - seaLevel) / landGradientSteepness);
            landGradient = Mathf.Clamp01((falloff - seaLevel) / landGradientSteepness);
          }
          landGradientPixels[i] = landGradient;

          // Get the ocean gradient from the falloff
          float oceanGradient;
          if (falloff > seaLevel) {
            oceanGradient = 0f;
          } else {
            // oceanGradient = Mathf.SmoothStep(0f, 1f, ((seaLevel - falloff) / (landGradientSteepness)));
            oceanGradient = Mathf.Clamp01((seaLevel - falloff) / (landGradientSteepness));
          }
          oceanGradientPixels[i] = oceanGradient;
        }
      }

      // Generate the noises for plateous
      float relativeMaximunPlateauHeight = (1f / chunk.size.y) * absoluteMaximunPlateauHeight;
      TerrainNoiseGenerator plateauMaskGenerator = plateauMask.GetGenerator();
      float[] plateauMaskPixels = plateauMaskGenerator.GenerateGrid2d(chunk, noiseScale, terrainSeed);
      TerrainNoiseGenerator plateauGroundGenerator = plateauGround.GetGenerator();
      float[] plateauGroundPixels = plateauGroundGenerator.GenerateGrid2d(chunk, noiseScale, terrainSeed);
      TerrainNoiseGenerator plateauShapeGenerator = plateauShape.GetGenerator();
      float[] plateauShapePixels = plateauShapeGenerator.GenerateGrid2d(chunk, noiseScale, terrainSeed);

      for (int z = 0; z < grid.sizes.z; z++) {
        for (int y = 0; y < grid.sizes.y; y++) {
          for (int x = 0; x < grid.sizes.x; x++) {
            // Get 1D index from the coords
            int index = grid.GetIndexFromCoords(x, y, z);

            // Get the position of the point
            Vector3 pointPosition = grid.GetPointPosition(x, y, z);

            CubeGridPoint point = new CubeGridPoint {
              index = index,
              position = pointPosition
            };

            // Coords for 2d maps
            int index2D = z * grid.gridSize.x + x;

            // Start sampling
            float output = 0;
            float heightGradient = point.position.y / chunk.size.y;

            if (debugMode == DebugMode.Noise) {
              point.value = baseTerrainPixels[point.index] * -1f;
              grid.gridPoints[index] = point;
              continue;
            }

            // Land output
            float terrainHeight = TextureUtils.Normalize(baseTerrainPixels[point.index]);

            if (usePlateaus) {
              // Overall shape of Plateaus
              float plateauMaskNoise = TextureUtils.Normalize(plateauMaskPixels[index2D]);
              float plateauShapeNoise =
                TextureUtils.Normalize(plateauShapePixels[index2D]) * plateauMaskNoise;

              // The height of the terrain on top of plateaus
              float plateauGroundNoise = TextureUtils.Normalize(plateauGroundPixels[index2D]);
              float plateauHeight = Mathf.LerpUnclamped(
                terrainHeight,
                plateauGroundNoise,
                relativeMaximunPlateauHeight
              );

              // 2nd Mask
              float threshold = 0.02f;
              float plateau2ndMask = plateauMaskNoise - terrainHeight;
              plateau2ndMask = Mathf.SmoothStep(0f, 1f, plateau2ndMask / threshold);
              plateauShapeNoise *= plateau2ndMask;

              // Use plateauHeight only if it's taller than terrainHeight
              terrainHeight = Mathf.LerpUnclamped(terrainHeight, plateauHeight, plateauShapeNoise);
            }

            if (useFalloff) {
              float landGradient = landGradientPixels[index2D];
              float oceanGradient = oceanGradientPixels[index2D];

              // Use the land gradient to combine the base terrain noise with the falloff map
              // float heightBelowSeaLevel = heightGradient - finalFalloff;
              // float heightAboveSeaLevel = heightGradient - seaLevel - (terrainHeight * (1f - seaLevel));
              // output = Mathf.Lerp(heightBelowSeaLevel, heightAboveSeaLevel, landGradient);

              // Determine the density in the ocean and on land
              float densitySeaLevel = heightGradient - seaLevel;
              float oceanDensity = heightGradient - terrainHeight * seaLevel * 0.5f;
              float landDensity = heightGradient - seaLevel - (terrainHeight * (1f - seaLevel));

              // Use the land and ocean gradients to combine land density and ocean density
              if (oceanGradient > 0f) {
                output = Mathf.LerpUnclamped(densitySeaLevel, oceanDensity, oceanGradient);
              } else {
                output = Mathf.LerpUnclamped(densitySeaLevel, landDensity, landGradient);
              }

              if (debugMode == DebugMode.Falloff) {
                debug2dPixels[index2D] = 1f - output;
              } else if (debugMode == DebugMode.LandGradient) {
                debug2dPixels[index2D] = landGradient;
              } else if (debugMode == DebugMode.OceanGradient) {
                debug2dPixels[index2D] = oceanGradient;
              } else if (debugMode == DebugMode.OceanAndLandGradient) {
                debug2dPixels[index2D] = Mathf.Max(oceanGradient, landGradient);
              } else if (debugMode == DebugMode.LandGradientSteepness) {
                debug2dPixels[index2D] = landGradientSteepnessPixels[index2D];
              }
            } else {
              output = heightGradient - terrainHeight;
            }

            // Set the density and save the point
            point.value = output;
            grid.gridPoints[index] = point;
          }
        }
      }

      // Initialize colors
      Color black = Color.black;
      for (int z = 0; z < grid.gridSize.z; z++) {
        for (int y = 0; y < grid.gridSize.y; y++) {
          for (int x = 0; x < grid.gridSize.x; x++) {
            int index = grid.GetIndexFromCoords(x, y, z);
            int index2D = z * grid.gridSize.x + x;
            ref CubeGridPoint point = ref grid.gridPoints[index];
            point.roughness = 0.1f;

            // Approximate normals
            Vector3 normal = grid.GetPointNormalApproximation(x, y, z);

            if (debugMode == DebugMode.Value) {
              point.color = Color.Lerp(Color.black, Color.white, point.value * 100f);
            } else if (debugMode == DebugMode.Normals) {
              point.color = new Color(normal.x, normal.y, normal.z);
            } else if (debugMode == DebugMode.Slope) {
              point.color = Color.Lerp(Color.black, Color.white, normal.y);
            } else if (debugMode == DebugMode.Falloff
              || debugMode == DebugMode.LandGradient
              || debugMode == DebugMode.OceanGradient
              || debugMode == DebugMode.OceanAndLandGradient
              || debugMode == DebugMode.LandGradientSteepness
            ) {
              point.color = Color.white * debug2dPixels[index2D] * debugHeightmapMultiplier;
            } else if (debugMode == DebugMode.PlateauMask) {
              point.color = Color.Lerp(Color.black, Color.white, plateauMaskPixels[index2D]);
            } else if (debugMode == DebugMode.PlateauShape) {
              point.color = Color.Lerp(Color.black, Color.white, plateauShapePixels[index2D]);
            } else if (debugMode == DebugMode.PlateauShapeAndMask) {
              float plateauMaskNoise = TextureUtils.Normalize(plateauMaskPixels[index2D]);
              float plateauShapeNoise =
                TextureUtils.Normalize(plateauShapePixels[index2D]) * plateauMaskNoise;

              point.color = Color.Lerp(Color.black, Color.white, plateauShapeNoise);
            } else if (debugMode == DebugMode.PlateauGround) {
              point.color = Color.Lerp(Color.black, Color.white, plateauGroundPixels[index2D]);
            } else if (debugMode == DebugMode.Noise) {
              point.color = Color.white * 0.5f;
            } else {
              float normalizedHeight = point.position.y / chunk.size.y;

              if (normal.y <= 0.85f) {
                // Rock
                point.color = rockColor;
                point.roughness = 0.5f;
                point.material = rockId;
              } else if (normalizedHeight >= snowHeight) {
                // Snow
                point.color = snowColor;
                point.roughness = 0.05f;
                point.material = snowId;
              } else if (normalizedHeight <= seaLevel) {
                // Underwater Beach Sand
                float t = Mathf.InverseLerp(0f, sandHeight, normalizedHeight);
                point.color = Color.Lerp(darkSandColor, wetSandColor, t);
                point.roughness = 0.05f;
                point.material = sandId;
              } else if (normalizedHeight <= sandHeight) {
                // Beach Sand
                point.color = sandColor;
                point.roughness = 0.1f;
                point.material = sandId;
              } else {
                // Grass
                float t = Mathf.InverseLerp(sandHeight, snowHeight, normalizedHeight);
                point.color = Color.Lerp(grassColor, darkGrassColor, t);
                point.material = grassId;
              }
            }
          }
        }
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
