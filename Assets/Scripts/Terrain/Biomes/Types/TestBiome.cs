using UnityEngine;
using FastNoise2Graph;
using System.Buffers;

[CreateAssetMenu(menuName = "Terrain/Biome/TestBiome")]
public class TestBiome : Biome {
  public NoiseTreeGenerator noise = new NoiseTreeGenerator {
    seed = 0,
    scale = 1f
  };

  public override void Generate(TerrainShape shape, FastNoiseChunk chunk, VoxelGrid grid, float[] mask) {
    // Generate the base terrain noise
    float[] baseTerrainPixels = ArrayPool<float>.Shared.Rent(chunk.pointCount3d);
    noise.GetGenerator().GenerateGrid3d(
      baseTerrainPixels,
      chunk,
      shape.noiseScale,
      shape.terrainSeed
    );

    for (int index = 0; index < grid.totalPointCount; index++) {
      Vector3Int coords = grid.GetCoordsFromIndex(index);
      int index2D = coords.z * grid.size.x + coords.x;

      if (mask[index2D] == 0f) {
        continue;
      }

      ref VoxelPoint point = ref grid.points[index];

      // Start sampling
      float densityOutput = 0;
      float heightGradient = point.position.y / chunk.scale.y;
      densityOutput = heightGradient - testHeight;

      // Land output
      float terrainHeight = TextureUtils.Normalize(baseTerrainPixels[index]);
      densityOutput = heightGradient - terrainHeight;

      // Set the density and save the point
      point.value = densityOutput;
    };

    // Initialize colors
    for (int index = 0; index < grid.totalPointCount; index++) {
      ref VoxelPoint point = ref grid.points[index];

      // Snow
      point.color = shape.snowColor;
      point.roughness = 0.15f;
      point.material = shape.snowId;
    }

    ArrayPool<float>.Shared.Return(baseTerrainPixels);
  }
}