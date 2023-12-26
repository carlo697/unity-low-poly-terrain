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
    ref NativeList<Color> colors
  ) {
    for (int z = 0; z < grid.size.z - 1; z++) {
      for (int y = 0; y < grid.size.y - 1; y++) {
        for (int x = 0; x < grid.size.x - 1; x++) {
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
              Vector3 positionA = grid.points[indexA].position;

              Vector3Int coordsB = coords + MarchingCubesConsts.edgeVerticesIndexes[edgeIndex, 1];
              int indexB = grid.GetIndexFromCoords(coordsB);
              float sampleB = grid.points[indexB].value;
              Vector3 positionB = grid.points[indexB].position;

              // Apply a random position to get a rougher mesh
              if (coordsA.x > 0 && coordsA.x < grid.size.x - 1
                && coordsA.y > 0 && coordsA.y < grid.size.y - 1
                && coordsA.z > 0 && coordsA.z < grid.size.z - 1
              ) {
                XorshiftStar rngA = new XorshiftStar((ulong)indexA);
                Vector3 roughnessOffsetA = rngA.NextFlatVector3();
                positionA += roughnessOffsetA * grid.points[indexA].roughness;
              }
              if (coordsB.x > 0 && coordsB.x < grid.size.x - 1
                && coordsB.y > 0 && coordsB.y < grid.size.y - 1
                && coordsB.z > 0 && coordsB.z < grid.size.z - 1
              ) {
                XorshiftStar rngB = new XorshiftStar((ulong)indexB);
                Vector3 roughnessOffsetB = rngB.NextFlatVector3();
                positionB += roughnessOffsetB * grid.points[indexB].roughness;
              }

              // Calculate the difference and interpolate
              float interpolant = (threshold - sampleA) / (sampleB - sampleA);
              Vector3 interpolatedPosition = Vector3.Lerp(positionA, positionB, interpolant);

              vertices.Add(interpolatedPosition);

              // Add vertex color
              colors.Add(Color.Lerp(grid.points[indexA].color, grid.points[indexB].color, interpolant));

              // Add UVs
              if (interpolant < 0.5f) {
                uvs.Add(new Vector3(
                  MaterialBitConverter.MaterialIdToFloat(grid.points[indexA].material), 0f, 0f
                ));
              } else {
                uvs.Add(new Vector3(
                  MaterialBitConverter.MaterialIdToFloat(grid.points[indexB].material), 0f, 0f
                ));
              }
            }

            if (skip) {
              break;
            }

            triangles.Add(nextVertexIndex);
            triangles.Add(nextVertexIndex + 1);
            triangles.Add(nextVertexIndex + 2);
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
