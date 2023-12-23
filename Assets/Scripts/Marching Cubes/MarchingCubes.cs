using UnityEngine;
using Unity.Collections;

public delegate void VoxelGridSamplerFunc(VoxelGrid grid);

public static class MarchingCubes {
  private static readonly Vector3[] roughnessVectors;

  static MarchingCubes() {
    System.Random random = new System.Random(7091999);

    roughnessVectors = new Vector3[1000];
    for (int i = 0; i < roughnessVectors.Length; i++) {
      roughnessVectors[i] = new Vector3(
        ((float)random.NextDouble() * 2f - 1f),
        0f,
        ((float)random.NextDouble() * 2f - 1f)
      ).normalized;
    }
  }

  private static void MarchCubes(
    VoxelGrid grid,
    float threshold,
    NativeList<Vector3> vertices,
    NativeList<Vector3> uvs,
    NativeList<Color> colors
  ) {
    for (int z = 0; z < grid.size.z - 1; z++) {
      for (int y = 0; y < grid.size.y - 1; y++) {
        for (int x = 0; x < grid.size.x - 1; x++) {
          // Find the case index
          int caseIndex = 0;
          for (int i = 0; i < 8; i++) {
            int sampleIndex = grid.GetIndexFromCoords(
              x + MarchingCubesConsts.corners[i].x,
              y + MarchingCubesConsts.corners[i].y,
              z + MarchingCubesConsts.corners[i].z
            );
            float sample = grid.points[sampleIndex].value;

            if (sample > threshold)
              caseIndex |= 1 << i;
          }

          if (caseIndex == 0 || caseIndex == 0xFF)
            continue;

          Vector3Int coords = new Vector3Int(x, y, z);

          for (int i = 0; i <= 15; i++) {
            int edgeIndex = MarchingCubesConsts.cases[caseIndex, i];
            if (edgeIndex == -1) break;

            Vector3Int coordsA = coords + MarchingCubesConsts.edgeVerticesIndexes[edgeIndex, 0];
            int indexA = grid.GetIndexFromCoords(coordsA.x, coordsA.y, coordsA.z);
            float sampleA = grid.points[indexA].value;
            Vector3 positionA = grid.points[indexA].position;

            Vector3Int coordsB = coords + MarchingCubesConsts.edgeVerticesIndexes[edgeIndex, 1];
            int indexB = grid.GetIndexFromCoords(coordsB.x, coordsB.y, coordsB.z);
            float sampleB = grid.points[indexB].value;
            Vector3 positionB = grid.points[indexB].position;

            // Apply a random position to get a rougher mesh
            if (coordsA.x > 0 && coordsA.x < grid.size.x - 1
              && coordsA.y > 0 && coordsA.y < grid.size.y - 1
              && coordsA.z > 0 && coordsA.z < grid.size.z - 1
            ) {
              positionA +=
                roughnessVectors[indexA % roughnessVectors.Length] * grid.points[indexA].roughness;
            }
            if (coordsB.x > 0 && coordsB.x < grid.size.x - 1
              && coordsB.y > 0 && coordsB.y < grid.size.y - 1
              && coordsB.z > 0 && coordsB.z < grid.size.z - 1
            ) {
              positionB +=
                roughnessVectors[indexB % roughnessVectors.Length] * grid.points[indexB].roughness;
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
        }
      }
    }
  }

  public static void Generate(
    VoxelGrid grid,
    float threshold,
    ref NativeList<Vector3> outputVertices,
    ref NativeList<int> outputTriangles,
    ref NativeList<Vector3> outputUVs,
    ref NativeList<Color> outputColors,
    VoxelGridSamplerFunc samplerFunc = null,
    bool debug = false
  ) {
    var stepTimer = new System.Diagnostics.Stopwatch();
    stepTimer.Start();

    grid.Initialize(samplerFunc);

    stepTimer.Stop();
    if (debug)
      Debug.Log(
        string.Format(
          "Grid: {0} ms, resolution: {1}",
          stepTimer.ElapsedMilliseconds,
          grid.resolution
        )
      );

    stepTimer.Restart();

    // March the cubes to generate the vertices, uvs, colors, etc
    MarchCubes(grid, threshold, outputVertices, outputUVs, outputColors);

    // Loop through the vertices to generate the triangles
    for (int i = 0; i < outputVertices.Length; i += 3) {
      outputTriangles.Add(i);
      outputTriangles.Add(i + 1);
      outputTriangles.Add(i + 2);
    }

    stepTimer.Stop();
    if (debug)
      Debug.Log(
        string.Format(
          "Marching: {0} ms, resolution: {1}",
          stepTimer.ElapsedMilliseconds,
          grid.resolution
        )
      );
  }

  public static Mesh CreateMesh(
    NativeList<Vector3> vertices,
    NativeList<int> triangles,
    NativeList<Vector3> uvs,
    NativeList<Color> colors,
    bool debug = false,
    Mesh meshToReuse = null
  ) {
    var stepTimer = new System.Diagnostics.Stopwatch();
    stepTimer.Start();

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

    stepTimer.Stop();
    if (debug)
      Debug.Log(
        string.Format(
          "Generating mesh: {0} ms",
          stepTimer.ElapsedMilliseconds
        )
      );

    return mesh;
  }
}
