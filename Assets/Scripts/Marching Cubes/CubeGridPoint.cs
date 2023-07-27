using UnityEngine;

public struct CubeGridPoint {
  public int index;
  public Vector3 position;
  public float value;
  // public byte material;
  public Color color;
  public float roughness;

  public Vector3Int GetCoords(TerrainChunk chunk) {
    int x = index / (chunk.gridSize.y * chunk.gridSize.x);
    int y = (index / chunk.gridSize.x) % chunk.gridSize.y;
    int z = index % chunk.gridSize.x;

    return new Vector3Int(x, y, z);
  }

  public int Get2dIndex(TerrainChunk chunk) {
    int x = index / (chunk.gridSize.y * chunk.gridSize.x);
    int z = index % chunk.gridSize.x;
    int index2D = z * chunk.gridSize.x + x;

    return index2D;
  }

  public override string ToString() {
    return string.Format(
      "pos: {0}, value: {1}",
      position.ToString(),
      value
    );
  }
}