using UnityEngine;

[CreateAssetMenu(menuName = "Terrain/Biome/Desert")]
public class DesertBiome : Biome {
  public override void Generate(TerrainShape shape, FastNoiseChunk chunk, VoxelGrid grid, float[] mask) {
    TestData(shape, chunk, grid, false, mask);

    // Initialize colors
    for (int index = 0; index < grid.totalPointCount; index++) {
      ref VoxelPoint point = ref grid.points[index];

      // Approximate normals
      Vector3Int coords = grid.GetCoordsFromIndex(index);
      Vector3 normal = grid.GetPointNormalApproximation(coords.x, coords.y, coords.z);

      // Position
      float normalizedHeight = point.position.y / chunk.scale.y;

      if (normalizedHeight <= shape.seaLevel) {
        // Underwater Beach Sand
        float t = Mathf.InverseLerp(0f, shape.sandHeight, normalizedHeight);
        point.color = Color.Lerp(shape.darkSandColor, shape.wetSandColor, t);
        point.roughness = 0.15f;
        point.material = shape.sandId;
      } else {
        // Sand
        point.color = shape.sandColor;
        point.roughness = 0.15f;
        point.material = shape.sandId;
      }
    };
  }
}