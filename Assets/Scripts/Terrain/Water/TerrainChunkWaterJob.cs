using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.InteropServices;

public struct TerrainChunkWaterJob : IJob {
  private Vector2Int resolution;
  private Vector2 size;
  private NativeList<Vector3> vertices;
  private NativeList<int> triangles;
  private NativeList<Vector3> normals;

  public TerrainChunkWaterJob(
    Vector2Int resolution,
    Vector2 size,
    NativeList<Vector3> vertices,
    NativeList<int> triangles,
    NativeList<Vector3> normals
  ) {
    this.resolution = resolution;
    this.size = size;
    this.vertices = vertices;
    this.triangles = triangles;
    this.normals = normals;
  }

  public void Execute() {
    int numVertices = (resolution.x + 1) * (resolution.y + 1);
    vertices.Length = numVertices;
    triangles.Length = resolution.x * resolution.y * 6;
    normals.Length = numVertices;

    for (int y = 0; y <= resolution.y; y++) {
      for (int x = 0; x <= resolution.x; x++) {
        int index = y * (resolution.x + 1) + x;
        float xPos = (float)x / resolution.x;
        float yPos = (float)y / resolution.y;
        vertices[index] = new Vector3(xPos * size.x, 0f, yPos * size.y);
        normals[index] = Vector3.up;
      }
    }

    int triangleIndex = 0;
    for (int y = 0; y < resolution.y; y++) {
      for (int x = 0; x < resolution.x; x++) {
        int vertexIndex = y * (resolution.x + 1) + x;
        triangles[triangleIndex + 1] = vertexIndex + 1;
        triangles[triangleIndex + 2] = vertexIndex;
        triangles[triangleIndex] = vertexIndex + resolution.x + 2;
        triangles[triangleIndex + 3] = vertexIndex + resolution.x + 1;
        triangles[triangleIndex + 4] = vertexIndex + resolution.x + 2;
        triangles[triangleIndex + 5] = vertexIndex;
        triangleIndex += 6;
      }
    }
  }
}