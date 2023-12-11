using UnityEngine;

[CreateAssetMenu(menuName = "Terrain/Shape", order = 0)]
public class TerrainShape : ScriptableObject, ISamplerFactory {
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
  public bool useFalloff;
  public FalloffNoiseGenerator falloffNoise = new FalloffNoiseGenerator();

  [Header("Plateaus")]
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
  public DebugMode debugMode = DebugMode.None;

  public enum DebugMode {
    None,
    Value,
    Normals,
    Slope,
    Falloff,
    PlateauMask,
    PlateauShape,
    PlateauShapeAndMask,
    PlateauGround,
  }

  public static float Normalize(float value) {
    return ((value + 1f) / 2f);
  }

  public static float Denormalize(float value) {
    return (value * 2f) - 1f;
  }

  public static float[] GenerateFastNoiseForChunk(
    bool is3D,
    TerrainChunk chunk,
    FastNoise noise,
    int seed,
    float scale = 1f
  ) {
    // Variables needed to sample the point in world space
    float sizeNormalizer = chunk.size.x / 32f;
    float noiseFrequency = 1f / (chunk.noiseSize * scale);

    // Apply offset
    Vector3 offset = chunk.noisePosition * noiseFrequency;
    FastNoise offsetNode = new FastNoise("Domain Offset");
    offsetNode.Set("Source", noise);
    if (is3D) {
      offsetNode.Set("OffsetX", offset.z);
      offsetNode.Set("OffsetY", offset.y);
      offsetNode.Set("OffsetZ", offset.x);
    } else {
      offsetNode.Set("OffsetX", offset.x);
      offsetNode.Set("OffsetY", offset.z);
    }

    // Apply scale to noise
    float scaleNodeValue = noiseFrequency * sizeNormalizer;
    FastNoise scaleNode = new FastNoise("Domain Axis Scale");
    scaleNode.Set("Source", offsetNode);
    scaleNode.Set("ScaleX", scaleNodeValue);
    scaleNode.Set("ScaleY", scaleNodeValue);
    scaleNode.Set("ScaleZ", scaleNodeValue);

    float[] pixels;
    if (is3D) {
      pixels = new float[chunk.gridSize.x * chunk.gridSize.y * chunk.gridSize.z];
      scaleNode.GenUniformGrid3D(
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
      scaleNode.GenUniformGrid2D(
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

  public CubeGridSamplerFunc GetSampler(
    TerrainChunk chunk
  ) {
    return (CubeGrid grid) => {
      // Create copies of the curves
      AnimationCurve curve = new AnimationCurve(this.curve.keys);
      AnimationCurve normalizerCurve = new AnimationCurve(this.normalizerCurve.keys);

      // Debug pixels
      float[] debugFalloff = null;

      // Generate the base terrain noise
      TerrainNoiseGenerator baseTerrainGenerator = baseNoise.GetGenerator();
      float[] baseTerrainPixels = baseTerrainGenerator.GenerateGrid3d(chunk, noiseScale, terrainSeed);

      // Generate the falloff map
      float[] falloffPixels = null;
      if (useFalloff) {
        falloffPixels = falloffNoise.GenerateNoise(chunk, noiseScale, terrainSeed);

        if (debugMode == DebugMode.Falloff) {
          debugFalloff = new float[chunk.gridSize.x * chunk.gridSize.z];
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
            float heightGradient = point.position.y * chunk.inverseSize.y;

            // Land output
            float terrainHeight = Normalize(baseTerrainPixels[point.index]);

            // Overall shape of Plateaus
            float plateauMaskNoise = Normalize(plateauMaskPixels[index2D]);
            float plateauShapeNoise = Normalize(plateauShapePixels[index2D]) * plateauMaskNoise;

            // The height of the terrain on top of plateaus
            float plateauGroundNoise = Normalize(plateauGroundPixels[index2D]);
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

            if (useFalloff) {
              // Sample the falloff map
              float finalFalloff = falloffPixels[index2D];

              // Land gradient
              float landGradient;
              float start = seaLevel;
              if (finalFalloff <= start) {
                landGradient = 0f;
              } else {
                landGradient = Mathf.SmoothStep(0f, 1f, (finalFalloff - start) / (falloffNoise.landGap));
              }

              // Use the land gradient to combine the base terrain noise with the falloff map
              float heightBelowSeaLevel = heightGradient - finalFalloff;
              float heightAboveSeaLevel = heightGradient - seaLevel - (terrainHeight * (1f - seaLevel));
              output = Mathf.Lerp(heightBelowSeaLevel, heightAboveSeaLevel, landGradient);

              if (debugMode == DebugMode.Falloff) {
                debugFalloff[index2D] = 1f - output;
              }

              // height = Mathf.Lerp(heightGradient, height, finalFalloff);
              // height = Mathf.Lerp(heightGradient, height, finalFalloff);
              // height = heightGradient - finalFalloff;
              // height = heightGradient - landGradient;
              // height = heightGradient - (landGradient * 0.8f);
              // height = Mathf.Lerp(height, heightGradient - seaLevel, borderGradient);
            } else {
              output = heightGradient - terrainHeight;
            }

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
            } else if (useFalloff && debugMode == DebugMode.Falloff) {
              point.color = Color.Lerp(Color.black, Color.white, debugFalloff[index2D]);
            } else if (debugMode == DebugMode.PlateauMask) {
              point.color = Color.Lerp(Color.black, Color.white, plateauMaskPixels[index2D]);
            } else if (debugMode == DebugMode.PlateauShape) {
              point.color = Color.Lerp(Color.black, Color.white, plateauShapePixels[index2D]);
            } else if (debugMode == DebugMode.PlateauShapeAndMask) {
              float plateauMaskNoise = Normalize(plateauMaskPixels[index2D]);
              float plateauShapeNoise = Normalize(plateauShapePixels[index2D]) * plateauMaskNoise;

              point.color = Color.Lerp(Color.black, Color.white, plateauShapeNoise);
            } else if (debugMode == DebugMode.PlateauGround) {
              point.color = Color.Lerp(Color.black, Color.white, plateauGroundPixels[index2D]);
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
              } else if (normalizedHeight <= sandHeight) {
                // Beach Sand
                float t = Mathf.InverseLerp(0f, sandHeight, normalizedHeight);
                point.color = Color.Lerp(darkSandColor, sandColor, t);
                point.roughness = 0.05f;
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
