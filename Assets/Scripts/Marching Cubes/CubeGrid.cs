using UnityEngine;
using System;
using Unity.Collections;
using System.Runtime.CompilerServices;

public delegate void CubeGridSamplerFunc(CubeGrid grid);

public class CubeGrid {
  public Vector3 size;
  public Vector3Int resolution;
  public float threshold;

  public Vector3Int gridSize { get { return m_sizes; } }
  public int gridPointCount { get { return m_pointCount; } }
  public Vector3 resolutionSizeRatio { get { return m_resolutionSizeRatio; } }
  public CubeGridPoint[] gridPoints {
    get {
      return m_points;
    }
    set {
      if (value.Length != m_pointCount) {
        throw new Exception("The new array don't have the correct amount of points");
      }

      m_points = value;
    }
  }

  public Vector3Int sizes { get { return m_sizes; } }
  private Vector3Int m_sizes;

  private int m_pointCount;
  private Vector3 m_resolutionSizeRatio;
  private CubeGridPoint[] m_points;

  public static readonly Vector3[] roughnessVectors;

  static CubeGrid() {
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

  public CubeGrid(
    Vector3 size,
    Vector3Int resolution,
    float threshold = 0f
  ) {
    this.size = size;
    this.resolution = resolution;
    this.threshold = threshold;

    // Calculations needed to create the grid array
    m_sizes = new Vector3Int(resolution.x + 1, resolution.y + 1, resolution.z + 1);
    m_pointCount = m_sizes.x * m_sizes.y * m_sizes.z;

    // This value can be useful when the size of the chunk is different from the resolution 
    m_resolutionSizeRatio = new Vector3(
      resolution.x / size.x,
      resolution.y / size.y,
      resolution.z / size.z
    );
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public CubeGridPoint GetPoint(int index) {
    return m_points[index];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public CubeGridPoint GetPoint(int x, int y, int z) {
    int index = GetIndexFromCoords(x, y, z);
    return m_points[index];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref CubeGridPoint GetPointRef(int x, int y, int z) {
    int index = GetIndexFromCoords(x, y, z);
    return ref m_points[index];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Vector3 GetPointPosition(int x, int y, int z) {
    return new Vector3(
      ((float)x / ((float)resolution.x)) * size.x,
      ((float)y / ((float)resolution.y)) * size.y,
      ((float)z / ((float)resolution.z)) * size.z
    );
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Vector3Int GetCoordsFromIndex(int index) {
    int z = index % m_sizes.x;
    int y = (index / m_sizes.x) % m_sizes.y;
    int x = index / (m_sizes.y * m_sizes.x);
    return new Vector3Int(x, y, z);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public int GetIndexFromCoords(int x, int y, int z) {
    return z + y * (m_sizes.z) + x * (m_sizes.z) * (m_sizes.y);
  }

  public Vector3 GetPointNormalApproximation(int x, int y, int z) {
    float value = GetPointRef(x, y, z).value;

    // Approximate normals
    float sumX = 0;
    float sumY = 0;
    float sumZ = 0;

    // Left
    if (x > 0)
      sumX += -1f * (value - GetPointRef(x - 1, y, z).value) * resolutionSizeRatio.x;

    // Right
    if (x < gridSize.x - 1)
      sumX += (value - GetPointRef(x + 1, y, z).value) * resolutionSizeRatio.x;

    // Down
    if (y > 0)
      sumY += -1f * (value - GetPointRef(x, y - 1, z).value) * resolutionSizeRatio.y;

    // Up
    if (y < gridSize.y - 1)
      sumY += (value - GetPointRef(x, y + 1, z).value) * resolutionSizeRatio.y;

    // Back
    if (z > 0)
      sumZ = -1f * (value - GetPointRef(x, y, z - 1).value) * resolutionSizeRatio.z;

    // Forward
    if (z < gridSize.z - 1)
      sumZ += (value - GetPointRef(x, y, z + 1).value) * resolutionSizeRatio.z;

    return new Vector3(-sumX, -sumY, -sumZ).normalized;
  }

  public void InitializeGrid(
    CubeGridSamplerFunc samplerFunc = null
  ) {
    // Initialize the grid with points (all of them will start with a value = 0)
    m_points = new CubeGridPoint[m_pointCount];
    samplerFunc(this);
  }

  private void MarchCubes(
    NativeList<Vector3> vertices,
    NativeList<Vector3> uvs,
    NativeList<Color> colors
  ) {
    for (int z = 0; z < m_sizes.z - 1; z++) {
      for (int y = 0; y < m_sizes.y - 1; y++) {
        for (int x = 0; x < m_sizes.x - 1; x++) {
          // Find the case index
          int caseIndex = 0;
          for (int i = 0; i < 8; i++) {
            int sampleIndex = GetIndexFromCoords(
              x + MarchingCubesConsts.corners[i].x,
              y + MarchingCubesConsts.corners[i].y,
              z + MarchingCubesConsts.corners[i].z
            );
            float sample = m_points[sampleIndex].value;

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
            int indexA = GetIndexFromCoords(coordsA.x, coordsA.y, coordsA.z);
            float sampleA = m_points[indexA].value;
            Vector3 positionA = m_points[indexA].position;

            Vector3Int coordsB = coords + MarchingCubesConsts.edgeVerticesIndexes[edgeIndex, 1];
            int indexB = GetIndexFromCoords(coordsB.x, coordsB.y, coordsB.z);
            float sampleB = m_points[indexB].value;
            Vector3 positionB = m_points[indexB].position;

            // Apply a random position to get a rougher mesh
            if (coordsA.x > 0 && coordsA.x < m_sizes.x - 1
              && coordsA.y > 0 && coordsA.y < m_sizes.y - 1
              && coordsA.z > 0 && coordsA.z < m_sizes.z - 1
            ) {
              positionA +=
                roughnessVectors[indexA % roughnessVectors.Length] * m_points[indexA].roughness;
            }
            if (coordsB.x > 0 && coordsB.x < m_sizes.x - 1
              && coordsB.y > 0 && coordsB.y < m_sizes.y - 1
              && coordsB.z > 0 && coordsB.z < m_sizes.z - 1
            ) {
              positionB +=
                roughnessVectors[indexB % roughnessVectors.Length] * m_points[indexB].roughness;
            }

            // Calculate the difference and interpolate
            float interpolant = (threshold - sampleA) / (sampleB - sampleA);
            Vector3 interpolatedPosition = Vector3.Lerp(positionA, positionB, interpolant);

            vertices.Add(interpolatedPosition);

            // Add vertex color
            colors.Add(Color.Lerp(m_points[indexA].color, m_points[indexB].color, interpolant));

            // Add UVs
            if (interpolant < 0.5f) {
              uvs.Add(new Vector3(
                MaterialBitConverter.MaterialIdToFloat(m_points[indexA].material), 0f, 0f
              ));
            } else {
              uvs.Add(new Vector3(
                MaterialBitConverter.MaterialIdToFloat(m_points[indexB].material), 0f, 0f
              ));
            }
          }
        }
      }
    }
  }

  public void Generate(
    ref NativeList<Vector3> outputVertices,
    ref NativeList<int> outputTriangles,
    ref NativeList<Vector3> outputUVs,
    ref NativeList<Color> outputColors,
    CubeGridSamplerFunc samplerFunc = null,
    bool debug = false
  ) {
    var stepTimer = new System.Diagnostics.Stopwatch();
    stepTimer.Start();

    InitializeGrid(samplerFunc);

    stepTimer.Stop();
    if (debug)
      Debug.Log(
        string.Format(
          "Grid: {0} ms, resolution: {1}",
          stepTimer.ElapsedMilliseconds,
          resolution
        )
      );

    stepTimer.Restart();

    // March the cubes to generate the vertices, uvs, colors, etc
    MarchCubes(outputVertices, outputUVs, outputColors);

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
          resolution
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
