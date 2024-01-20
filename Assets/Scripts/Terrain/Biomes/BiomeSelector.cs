using UnityEngine;
using Unity.Mathematics;
using System.Buffers;
using System.Collections.Generic;

public static class BiomeSelector {
  private static Biome SelectBiomeAt(
    float2 worldPosition,
    Biome[] biomes,
    float value,
    float temperature,
    float precipitation
  ) {
    var selectableBiomes = ArrayPool<Biome>.Shared.Rent(biomes.Length);
    int selectableBiomesCount = 0;

    for (int i = 0; i < biomes.Length; i++) {
      Biome biome = biomes[i];
      if (biome.CanBePlacedAt(worldPosition, temperature, precipitation)) {
        selectableBiomes[selectableBiomesCount] = biome;
        selectableBiomesCount++;
      }
    }

    int randomInteger = Mathf.RoundToInt(value * 1_000_000);
    int biomeIndex = randomInteger % selectableBiomesCount;
    Biome selectedBiome = selectableBiomes[biomeIndex];

    ArrayPool<Biome>.Shared.Return(selectableBiomes);

    return selectedBiome;
  }

  public static Dictionary<Biome, float[]> DetermineBiomes(
    FastNoiseChunk chunk,
    int terrainSeed,
    Biome[] biomes,
    float baseNoiseScale,
    float voronoiNoiseScale,
    float voronoiRandomness,
    INoiseGenerator voronoiWarpGenerator,
    INoiseGenerator temperatureGenerator,
    INoiseGenerator precipitationGenerator
  ) {
    int2 size2d = new int2(chunk.resolution2d.x, chunk.resolution2d.y);
    float2 scale2d = (float2)chunk.scale2d;
    float2 position2d = (float2)chunk.position2d;
    float noiseFrequency = 1f / voronoiNoiseScale;

    var data = ArrayPool<Voronoi.EdgeDistance>.Shared.Rent(9);
    Dictionary<Biome, float[]> masks = new Dictionary<Biome, float[]>();

    float[] warpPixels = voronoiWarpGenerator.GenerateGrid2d(chunk, baseNoiseScale, terrainSeed);

    for (int y = 0; y < size2d.x; y++) {
      for (int x = 0; x < size2d.y; x++) {
        int index = TextureUtils.GetIndexFrom2d(x, y, size2d.y);

        float warping = warpPixels[index];

        // Get the world position of the pixel
        float2 normalizedLocalPosition = new float2(x, y) / (size2d - new float2(1f, 1f));
        float2 worldPosition = position2d + scale2d * normalizedLocalPosition;
        float2 samplePosition = worldPosition * noiseFrequency + new float2(warping, warping);

        // Get the edge distances to the 9 nearest voronoi cells
        Voronoi.VoronoiEdgeDistancesAt(data, samplePosition, voronoiRandomness);

        // Iterate the 9 distances
        for (int i = 0; i < 9; i++) {
          Voronoi.EdgeDistance voronoiPixel = data[i];

          // Convert the distance to the range 0-1 and make them overlap each other
          float distanceToEdge = math.saturate(voronoiPixel.distanceToEdge * 3f + 0.5f);

          // Discard the distance
          if (distanceToEdge <= 0f) {
            continue;
          }

          // Smooth the result
          distanceToEdge = Mathf.SmoothStep(0f, 1f, distanceToEdge);

          // Information needed to assign a biome
          float2 worldCellPosition = (voronoiPixel.cell) / noiseFrequency;
          float temperature = temperatureGenerator.Generate2d(
            worldCellPosition.x,
            worldCellPosition.y,
            voronoiNoiseScale,
            terrainSeed
          );
          float precipitation = precipitationGenerator.Generate2d(
            worldCellPosition.x,
            worldCellPosition.y,
            voronoiNoiseScale,
            terrainSeed
          );

          // Assign a biome
          Biome targetBiome = SelectBiomeAt(
            worldCellPosition,
            biomes,
            voronoiPixel.id,
            temperature,
            precipitation
          );

          float[] targetMask;
          if (!masks.TryGetValue(targetBiome, out targetMask)) {
            // Create the mask if it doesn't exist
            targetMask = masks[targetBiome] = new float[size2d.x * size2d.y];
          }

          // Write to the mask
          targetMask[index] = math.saturate(targetMask[index] + distanceToEdge);
        }
      }
    }

    ArrayPool<Voronoi.EdgeDistance>.Shared.Return(data);

    return masks;
  }
}