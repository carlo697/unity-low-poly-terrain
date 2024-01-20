using UnityEngine;
using System.Buffers;

public abstract class Biome : ScriptableObject {
  public new string name = "Biome";
  public Color debugColor = Color.yellow;
  public Range temperatureRange = new Range(-15f, 30f);
  public Range precipitationRange = new Range(0f, 450f);

  public float testHeight = 0.5f;

  public override string ToString() {
    return name;
  }

  public virtual bool CanBePlacedAt(
    Vector2 worldPosition,
    float temperature,
    float precipitation
  ) {
    bool isTemperatureValid = temperatureRange.IsInside(temperature);
    bool isPrecipitationValid = precipitationRange.IsInside(precipitation);
    return isTemperatureValid && isPrecipitationValid;
  }

  public void TestData(
    TerrainShape shape,
    FastNoiseChunk chunk,
    VoxelGrid grid,
    bool colors,
    float[] mask
  ) {
    int pointCount2d = chunk.pointCount2d;
    int pointCount3d = chunk.pointCount3d;

    // Generate the base terrain noise
    INoiseGenerator baseTerrainGenerator = shape.baseNoise.GetGenerator();
    float[] baseTerrainPixels = ArrayPool<float>.Shared.Rent(pointCount3d);
    baseTerrainGenerator.GenerateGrid3d(baseTerrainPixels, chunk, shape.noiseScale, shape.terrainSeed);

    // Generate the falloff map and the gradient maps
    float[] falloffPixels = ArrayPool<float>.Shared.Rent(pointCount2d);
    float[] landGradientSteepnessPixels = ArrayPool<float>.Shared.Rent(pointCount2d);
    float[] landGradientPixels = ArrayPool<float>.Shared.Rent(pointCount2d);
    float[] oceanGradientPixels = ArrayPool<float>.Shared.Rent(pointCount2d);
    if (shape.useFalloff) {
      shape.landMask.GetGenerator().GenerateGrid2d(
        falloffPixels,
        chunk,
        shape.noiseScale,
        shape.terrainSeed
      );
      shape.landGradientSteepness.GetGenerator().GenerateGrid2d(
        landGradientSteepnessPixels,
        chunk,
        shape.noiseScale,
        shape.terrainSeed
      );

      for (int i = 0; i < pointCount2d; i++) {
        float falloff = falloffPixels[i];
        float landGradientSteepness = landGradientSteepnessPixels[i];

        // Get the land gradient from the falloff
        float landGradient;
        if (falloff <= shape.seaLevel) {
          landGradient = 0f;
        } else {
          // landGradient = Mathf.SmoothStep(0f, 1f, (falloff - seaLevel) / landGradientSteepness);
          landGradient = Mathf.Clamp01((falloff - shape.seaLevel) / landGradientSteepness);
        }
        landGradientPixels[i] = landGradient;

        // Get the ocean gradient from the falloff
        float oceanGradient;
        if (falloff > shape.seaLevel) {
          oceanGradient = 0f;
        } else {
          // oceanGradient = Mathf.SmoothStep(0f, 1f, ((seaLevel - falloff) / (landGradientSteepness)));
          oceanGradient = Mathf.Clamp01((shape.seaLevel - falloff) / (landGradientSteepness));
        }
        oceanGradientPixels[i] = oceanGradient;
      }
    }

    // Generate the noises for plateous
    float relativeMaximunPlateauHeight = (1f / chunk.scale.y) * shape.absoluteMaximunPlateauHeight;
    INoiseGenerator plateauMaskGenerator = shape.plateauMask.GetGenerator();
    float[] plateauMaskPixels = ArrayPool<float>.Shared.Rent(chunk.pointCount2d);
    plateauMaskGenerator.GenerateGrid2d(plateauMaskPixels, chunk, shape.noiseScale, shape.terrainSeed);

    INoiseGenerator plateauGroundGenerator = shape.plateauGround.GetGenerator();
    float[] plateauGroundPixels = ArrayPool<float>.Shared.Rent(chunk.pointCount2d);
    plateauGroundGenerator.GenerateGrid2d(plateauGroundPixels, chunk, shape.noiseScale, shape.terrainSeed);

    INoiseGenerator plateauShapeGenerator = shape.plateauShape.GetGenerator();
    float[] plateauShapePixels = ArrayPool<float>.Shared.Rent(chunk.pointCount2d);
    plateauShapeGenerator.GenerateGrid2d(plateauShapePixels, chunk, shape.noiseScale, shape.terrainSeed);

    for (int index = 0; index < grid.totalPointCount; index++) {
      Vector3Int coords = grid.GetCoordsFromIndex(index);
      int index2D = coords.z * grid.size.x + coords.x;

      if (mask[index2D] == 0f) {
        continue;
      }

      ref VoxelPoint point = ref grid.points[index];

      // Start sampling
      float output = 0;
      float heightGradient = point.position.y / chunk.scale.y;
      output = heightGradient - testHeight;

      // Land output
      float terrainHeight = TextureUtils.Normalize(baseTerrainPixels[index]);

      if (shape.usePlateaus) {
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

      terrainHeight *= testHeight;

      if (shape.useFalloff) {
        float landGradient = landGradientPixels[index2D];
        float oceanGradient = oceanGradientPixels[index2D];

        // Use the land gradient to combine the base terrain noise with the falloff map
        // float heightBelowSeaLevel = heightGradient - finalFalloff;
        // float heightAboveSeaLevel = heightGradient - seaLevel - (terrainHeight * (1f - seaLevel));
        // output = Mathf.Lerp(heightBelowSeaLevel, heightAboveSeaLevel, landGradient);

        // Determine the density in the ocean and on land
        float densitySeaLevel = heightGradient - shape.seaLevel;
        float oceanDensity = heightGradient - terrainHeight * shape.seaLevel * 0.5f;
        float landDensity = heightGradient - shape.seaLevel - (terrainHeight * (1f - shape.seaLevel));

        // Use the land and ocean gradients to combine land density and ocean density
        if (oceanGradient > 0f) {
          output = Mathf.LerpUnclamped(densitySeaLevel, oceanDensity, oceanGradient);
        } else {
          output = Mathf.LerpUnclamped(densitySeaLevel, landDensity, landGradient);
        }
      } else {
        output = heightGradient - terrainHeight;
      }

      // Set the density and save the point
      point.value = output;
    };

    ArrayPool<float>.Shared.Return(baseTerrainPixels);
    ArrayPool<float>.Shared.Return(falloffPixels);
    ArrayPool<float>.Shared.Return(landGradientSteepnessPixels);
    ArrayPool<float>.Shared.Return(landGradientPixels);
    ArrayPool<float>.Shared.Return(oceanGradientPixels);
    ArrayPool<float>.Shared.Return(plateauMaskPixels);
    ArrayPool<float>.Shared.Return(plateauGroundPixels);
    ArrayPool<float>.Shared.Return(plateauShapePixels);

    if (!colors) {
      return;
    }

    // Initialize colors
    for (int index = 0; index < grid.totalPointCount; index++) {
      // Coords
      Vector3Int coords = grid.GetCoordsFromIndex(index);
      int index2D = coords.z * grid.size.x + coords.x;

      if (mask[index2D] == 0f) {
        continue;
      }

      ref VoxelPoint point = ref grid.points[index];

      // Approximate normals (currently it's very expensive)
      Vector3 normal = grid.GetPointNormalApproximation(coords.x, coords.y, coords.z);
      float normalizedHeight = point.position.y / chunk.scale.y;

      if (normal.y <= 0.85f) {
        // Rock
        point.color = shape.rockColor;
        point.roughness = 0.35f;
        point.material = shape.rockId;
      } else if (normalizedHeight >= shape.snowHeight) {
        // Snow
        point.color = shape.snowColor;
        point.roughness = 0.15f;
        point.material = shape.snowId;
      } else if (normalizedHeight <= shape.seaLevel) {
        // Underwater Beach Sand
        float t = Mathf.InverseLerp(0f, shape.sandHeight, normalizedHeight);
        point.color = Color.Lerp(shape.darkSandColor, shape.wetSandColor, t);
        point.roughness = 0.15f;
        point.material = shape.sandId;
      } else if (normalizedHeight <= shape.sandHeight) {
        // Beach Sand
        point.color = shape.sandColor;
        point.roughness = 0.25f;
        point.material = shape.sandId;
      } else {
        // Grass
        point.color = debugColor;
        point.roughness = 0.15f;
        point.material = shape.grassId;
      }
    };
  }

  public virtual void Generate(
    TerrainShape shape,
    FastNoiseChunk chunk,
    VoxelGrid grid,
    float[] mask
  ) {
    TestData(shape, chunk, grid, true, mask);
  }

  private void OnValidate() {
    // Find all the chunks
    TerrainChunk[] chunks = GameObject.FindObjectsOfType<TerrainChunk>();

    // Iterate the chunks and update the ones that use this biome
    foreach (var chunk in chunks) {
      if (chunk.terrainShape) {
        Biome[] biomes = chunk.terrainShape.biomes;
        foreach (var biome in biomes) {
          if (biome == this) {
            chunk.GenerateOnEditor();
          }
        }
      }
    }
  }
}
