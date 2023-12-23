using UnityEngine;
using Unity.Collections;
using System;
using System.Runtime.CompilerServices;

public delegate void VoxelGridSamplerFunc(VoxelGrid grid);

public class VoxelGrid {
  public Vector3 scale;
  public Vector3Int resolution;
  public float threshold;

  public Vector3Int size { get { return m_size; } }
  private Vector3Int m_size;

  public int totalPointCount { get { return m_totalPointCount; } }
  private int m_totalPointCount;

  public VoxelPoint[] points {
    get {
      return m_points;
    }
    set {
      if (value.Length != m_totalPointCount) {
        throw new Exception("The new array don't have the correct amount of points");
      }

      m_points = value;
    }
  }
  private VoxelPoint[] m_points;

  private Vector3 m_resolutionScaleRatio;
  private static readonly Vector3[] roughnessVectors;

  static VoxelGrid() {
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

  public VoxelGrid(
    Vector3 scale,
    Vector3Int resolution,
    float threshold = 0f
  ) {
    this.scale = scale;
    this.resolution = resolution;
    this.threshold = threshold;

    // Calculations needed to create the grid array
    m_size = new Vector3Int(resolution.x + 1, resolution.y + 1, resolution.z + 1);
    m_totalPointCount = m_size.x * m_size.y * m_size.z;

    // This value can be useful when the size of the chunk is different from the resolution 
    m_resolutionScaleRatio = new Vector3(
      resolution.x / scale.x,
      resolution.y / scale.y,
      resolution.z / scale.z
    );
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public VoxelPoint GetPoint(int index) {
    return m_points[index];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public VoxelPoint GetPoint(int x, int y, int z) {
    int index = GetIndexFromCoords(x, y, z);
    return m_points[index];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref VoxelPoint GetPointRef(int x, int y, int z) {
    int index = GetIndexFromCoords(x, y, z);
    return ref m_points[index];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Vector3 GetPointPosition(int x, int y, int z) {
    return new Vector3(
      ((float)x / ((float)resolution.x)) * scale.x,
      ((float)y / ((float)resolution.y)) * scale.y,
      ((float)z / ((float)resolution.z)) * scale.z
    );
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Vector3Int GetCoordsFromIndex(int index) {
    int z = index % m_size.x;
    int y = (index / m_size.x) % m_size.y;
    int x = index / (m_size.y * m_size.x);
    return new Vector3Int(x, y, z);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public int GetIndexFromCoords(int x, int y, int z) {
    return z + y * (m_size.z) + x * (m_size.z) * (m_size.y);
  }

  public Vector3 GetPointNormalApproximation(int x, int y, int z) {
    float value = GetPointRef(x, y, z).value;

    // Approximate normals
    float sumX = 0;
    float sumY = 0;
    float sumZ = 0;

    // Left
    if (x > 0)
      sumX += -1f * (value - GetPointRef(x - 1, y, z).value) * m_resolutionScaleRatio.x;

    // Right
    if (x < size.x - 1)
      sumX += (value - GetPointRef(x + 1, y, z).value) * m_resolutionScaleRatio.x;

    // Down
    if (y > 0)
      sumY += -1f * (value - GetPointRef(x, y - 1, z).value) * m_resolutionScaleRatio.y;

    // Up
    if (y < size.y - 1)
      sumY += (value - GetPointRef(x, y + 1, z).value) * m_resolutionScaleRatio.y;

    // Back
    if (z > 0)
      sumZ = -1f * (value - GetPointRef(x, y, z - 1).value) * m_resolutionScaleRatio.z;

    // Forward
    if (z < size.z - 1)
      sumZ += (value - GetPointRef(x, y, z + 1).value) * m_resolutionScaleRatio.z;

    return new Vector3(-sumX, -sumY, -sumZ).normalized;
  }

  public void InitializeGrid(
    VoxelGridSamplerFunc samplerFunc = null
  ) {
    // Initialize the grid with points (all of them will start with a value = 0)
    m_points = new VoxelPoint[m_totalPointCount];
    samplerFunc(this);
  }

  private void MarchCubes(
    NativeList<Vector3> vertices,
    NativeList<Vector3> uvs,
    NativeList<Color> colors
  ) {
    for (int z = 0; z < m_size.z - 1; z++) {
      for (int y = 0; y < m_size.y - 1; y++) {
        for (int x = 0; x < m_size.x - 1; x++) {
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
            if (coordsA.x > 0 && coordsA.x < m_size.x - 1
              && coordsA.y > 0 && coordsA.y < m_size.y - 1
              && coordsA.z > 0 && coordsA.z < m_size.z - 1
            ) {
              positionA +=
                roughnessVectors[indexA % roughnessVectors.Length] * m_points[indexA].roughness;
            }
            if (coordsB.x > 0 && coordsB.x < m_size.x - 1
              && coordsB.y > 0 && coordsB.y < m_size.y - 1
              && coordsB.z > 0 && coordsB.z < m_size.z - 1
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
    VoxelGridSamplerFunc samplerFunc = null,
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
