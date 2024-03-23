using UnityEngine;
using Unity.Collections;

public static class MarchingCubes {
  public static int FindCaseIndex(VoxelGrid grid, float threshold, Vector3Int coords) {
    int caseIndex = 0;
    for (int i = 0; i < 8; i++) {
      int sampleIndex = grid.GetIndexFromCoords(
        coords.x + MarchingCubesConsts.corners[i].x,
        coords.y + MarchingCubesConsts.corners[i].y,
        coords.z + MarchingCubesConsts.corners[i].z
      );
      float sample = grid.points[sampleIndex].value;

      if (sample > threshold) {
        caseIndex |= 1 << i;
      }
    }

    return caseIndex;
  }

  public static void MarchCubes(
    VoxelGrid grid,
    float threshold,
    ref NativeList<Vector3> vertices,
    ref NativeList<int> triangles,
    ref NativeList<Vector3> uvs,
    ref NativeList<Color> colors,
    ref NativeList<uint> triangleMaterials
  ) {
    Vector3[] currentVertices = new Vector3[3];

    for (int z = 0; z < grid.resolution.z; z++) {
      for (int y = 0; y < grid.resolution.y; y++) {
        for (int x = 0; x < grid.resolution.x; x++) {
          Vector3Int coords = new Vector3Int(x, y, z);

          // Find the case index
          int caseIndex = FindCaseIndex(grid, threshold, coords);

          // Skip first and last case since they don't have geometry
          if (caseIndex == 0 || caseIndex == 0xFF) {
            continue;
          }

          for (int triangle = 0; triangle < 5; triangle++) {
            int nextVertexIndex = vertices.Length;
            bool skip = false;

            for (int vertex = 0; vertex < 3; vertex++) {
              int caseVertexIndex = (triangle * 3) + vertex;
              int edgeIndex = MarchingCubesConsts.cases[caseIndex, caseVertexIndex];
              if (edgeIndex == -1) {
                skip = true;
                break;
              }

              Vector3Int coordsA = coords + MarchingCubesConsts.edgeVerticesIndexes[edgeIndex, 0];
              int indexA = grid.GetIndexFromCoords(coordsA);
              float sampleA = grid.points[indexA].value;
              Vector3 positionA = grid.GetPointPosition(coordsA.x, coordsA.y, coordsA.z);

              Vector3Int coordsB = coords + MarchingCubesConsts.edgeVerticesIndexes[edgeIndex, 1];
              int indexB = grid.GetIndexFromCoords(coordsB);
              float sampleB = grid.points[indexB].value;
              Vector3 positionB = grid.GetPointPosition(coordsB.x, coordsB.y, coordsB.z);

              // Apply a random position to get a rougher mesh
              if (coordsA.x > 0 && coordsA.x < grid.resolution.x
                && coordsA.y > 0 && coordsA.y < grid.resolution.y
                && coordsA.z > 0 && coordsA.z < grid.resolution.z
              ) {
                XorshiftStar rngA = new XorshiftStar((ulong)indexA);
                Vector3 roughnessOffsetA = rngA.NextFlatVector3();
                positionA += roughnessOffsetA * grid.points[indexA].roughness;
              }
              if (coordsB.x > 0 && coordsB.x < grid.resolution.x
                && coordsB.y > 0 && coordsB.y < grid.resolution.y
                && coordsB.z > 0 && coordsB.z < grid.resolution.z
              ) {
                XorshiftStar rngB = new XorshiftStar((ulong)indexB);
                Vector3 roughnessOffsetB = rngB.NextFlatVector3();
                positionB += roughnessOffsetB * grid.points[indexB].roughness;
              }

              // Calculate the difference and interpolate
              float interpolant = (threshold - sampleA) / (sampleB - sampleA);
              Vector3 interpolatedPosition = Vector3.Lerp(positionA, positionB, interpolant);

              currentVertices[vertex] = interpolatedPosition;

              // Add vertex color
              float colorInterpolant = ((interpolant - 0.5f) * 4f) + 0.5f;
              colors.Add(Color.Lerp(grid.points[indexA].color, grid.points[indexB].color, colorInterpolant));
            }

            if (skip) {
              break;
            }

            vertices.Add(currentVertices[0]);
            vertices.Add(currentVertices[1]);
            vertices.Add(currentVertices[2]);

            triangles.Add(nextVertexIndex);
            triangles.Add(nextVertexIndex + 1);
            triangles.Add(nextVertexIndex + 2);

            // Get the center of the triangle
            Vector3 middlePoint = (currentVertices[0] + currentVertices[1] + currentVertices[2]) / 3f;

            // Get the index of the closest point to the triangle center
            int indexSample = grid.GetNearestIndexAt(middlePoint);

            // Add vertex color
            // Color color = grid.points[indexSample].color;
            // colors.Add(color);
            // colors.Add(color);
            // colors.Add(color);

            // Add UVs
            Vector3 uv = Vector3.zero;
            uvs.Add(uv);
            uvs.Add(uv);
            uvs.Add(uv);

            // Add material
            triangleMaterials.Add(grid.points[indexSample].material);
          }
        }
      }
    }
  }

  public static Mesh CreateMesh(
    NativeList<Vector3> vertices,
    NativeList<int> triangles,
    NativeList<Vector3> uvs,
    NativeList<Color> colors,
    Mesh meshToReuse = null
  ) {
    // Create a mesh
    Mesh mesh;
    if (meshToReuse) {
      mesh = meshToReuse;
      mesh.Clear();
    } else {
      mesh = new Mesh();
    }
    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

    // Set vertices and triangles to the mesh
    mesh.Clear();
    if (vertices.Length > 0) {
      mesh.SetVertices<Vector3>(vertices);
      mesh.SetIndices<int>(triangles, MeshTopology.Triangles, 0);
      mesh.SetUVs<Vector3>(0, uvs);
      mesh.SetColors<Color>(colors);
      mesh.RecalculateNormals();
    }

    return mesh;
  }
}
