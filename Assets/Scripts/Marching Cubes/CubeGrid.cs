using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Collections;
using System.Runtime.CompilerServices;

public delegate CubeGridPoint CubeGridSamplerFunc(CubeGridPoint point);
public delegate void CubeGridPostProcessingFunc(CubeGrid grid);

public class CubeGrid {
  public Vector3 size;
  public Vector3Int resolution;
  public float threshold;
  public bool useMiddlePoint;

  public Vector3Int gridSize { get { return m_sizes; } }
  public Vector3 resolutionSizeRatio { get { return m_resolutionSizeRatio; } }
  public CubeGridPoint[] gridPoints { get { return m_points; } }

  private Vector3Int m_sizes;
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
    float threshold = 0f,
    bool useMiddlePoint = false
  ) {
    this.size = size;
    this.resolution = resolution;
    this.threshold = threshold;
    this.useMiddlePoint = useMiddlePoint;

    // Calculations needed to create the grid array
    m_sizes = new Vector3Int(resolution.x + 1, resolution.y + 1, resolution.z + 1);

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
    CubeGridPoint point = GetPoint(x, y, z);

    // Approximate normals
    float sumX = 0;
    float sumY = 0;
    float sumZ = 0;

    // Left
    if (x > 0)
      sumX += -1f * (point.value - GetPoint(x - 1, y, z).value) * resolutionSizeRatio.x;

    // Right
    if (x < gridSize.x - 1)
      sumX += (point.value - GetPoint(x + 1, y, z).value) * resolutionSizeRatio.x;

    // Down
    if (y > 0)
      sumY += -1f * (point.value - GetPoint(x, y - 1, z).value) * resolutionSizeRatio.y;

    // Up
    if (y < gridSize.y - 1)
      sumY += (point.value - GetPoint(x, y + 1, z).value) * resolutionSizeRatio.y;

    // Back
    if (z > 0)
      sumZ = -1f * (point.value - GetPoint(x, y, z - 1).value) * resolutionSizeRatio.z;

    // Forward
    if (z < gridSize.z - 1)
      sumZ += (point.value - GetPoint(x, y, z + 1).value) * resolutionSizeRatio.z;

    return new Vector3(-sumX, -sumY, -sumZ).normalized;
  }

  public void InitializeGrid(
    CubeGridSamplerFunc samplerFunc = null,
    CubeGridPostProcessingFunc postProcessingFunc = null
  ) {
    // Initialize the grid with points (all of them will start with a value = 0)
    m_points = new CubeGridPoint[m_sizes.x * m_sizes.y * m_sizes.z];
    for (int z = 0; z < m_sizes.z; z++) {
      for (int y = 0; y < m_sizes.y; y++) {
        for (int x = 0; x < m_sizes.x; x++) {
          // Get 1D index from the coords
          int index = GetIndexFromCoords(x, y, z);

          // Get the position of the point
          Vector3 pointPosition = GetPointPosition(x, y, z);

          // // Apply a random position to get a rougher mesh
          // if (x > 0 && x < m_sizes.x - 1 && y > 0 && y < m_sizes.y - 1 && z > 0 && z < m_sizes.z - 1) {
          //   int randomVectorIndex = index % RandomVectors.vectors.Length;
          //   pointPosition += RandomVectors.vectors[randomVectorIndex] * m_roughness;
          // }

          // Create the point and store it
          if (samplerFunc != null) {
            m_points[index] = samplerFunc(new CubeGridPoint(
              index,
              pointPosition,
              0
            ));
          } else {
            m_points[index] = new CubeGridPoint(
              index,
              pointPosition,
              0
            );
          }
        }
      }
    }

    // Call post processing
    if (postProcessingFunc != null)
      postProcessingFunc(this);
  }

  public void MarchSingleCube(
    ICollection<Vector3> vertices,
    ICollection<Color> colors,
    int x,
    int y,
    int z
  ) {
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
      return;

    Vector3Int coords = new Vector3Int(x, y, z);

    if (useMiddlePoint) {
      // Use the found case to add the vertices and triangles
      for (int i = 0; i <= 16; i++) {
        int edgeIndex = MarchingCubesConsts.cases[caseIndex, i];
        if (edgeIndex == -1) return;

        Vector3Int coordsA = coords + MarchingCubesConsts.edgeVerticesIndexes[edgeIndex, 0];
        int indexA = GetIndexFromCoords(coordsA.x, coordsA.y, coordsA.z);
        Vector3 positionA = m_points[indexA].position;

        Vector3Int coordsB = coords + MarchingCubesConsts.edgeVerticesIndexes[edgeIndex, 1];
        int indexB = GetIndexFromCoords(coordsB.x, coordsB.y, coordsB.z);
        Vector3 positionB = m_points[indexB].position;

        Vector3 middlePoint = (positionA + positionB) / 2;

        vertices.Add(middlePoint);
      }
    } else {
      for (int i = 0; i <= 16; i++) {
        int edgeIndex = MarchingCubesConsts.cases[caseIndex, i];
        if (edgeIndex == -1) return;

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
          positionA += roughnessVectors[indexA % roughnessVectors.Length] * m_points[indexA].roughness;
        }
        if (coordsB.x > 0 && coordsB.x < m_sizes.x - 1
          && coordsB.y > 0 && coordsB.y < m_sizes.y - 1
          && coordsB.z > 0 && coordsB.z < m_sizes.z - 1
        ) {
          positionB += roughnessVectors[indexB % roughnessVectors.Length] * m_points[indexB].roughness;
        }

        // Calculate the difference and interpolate
        float interpolant = (threshold - sampleA) / (sampleB - sampleA);
        Vector3 interpolatedPosition = Vector3.Lerp(positionA, positionB, interpolant);

        vertices.Add(interpolatedPosition);
        colors.Add(Color.Lerp(m_points[indexA].color, m_points[indexB].color, interpolant));
      }
    }
  }

  public void Generate(
    out Vector3[] outputVertices,
    out int[] outputTriangles,
    out Color[] outputColors,
    CubeGridSamplerFunc samplerFunc = null,
    CubeGridPostProcessingFunc postProcessingFunc = null,
    bool debug = false
  ) {
    var stepTimer = new System.Diagnostics.Stopwatch();
    stepTimer.Start();

    InitializeGrid(samplerFunc, postProcessingFunc);

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

    // Loop through the points to generate the vertices
    List<Vector3> vertices = new List<Vector3>();
    List<Color> colors = new List<Color>();
    for (int z = 0; z < m_sizes.z - 1; z++) {
      for (int y = 0; y < m_sizes.y - 1; y++) {
        for (int x = 0; x < m_sizes.x - 1; x++) {
          MarchSingleCube(vertices, colors, x, y, z);
        }
      }
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

    stepTimer.Restart();

    // Loop through the vertices to generate the triangles
    List<int> triangles = new List<int>();
    for (int i = 0; i < vertices.Count; i += 3) {
      triangles.Add(i);
      triangles.Add(i + 1);
      triangles.Add(i + 2);
    }

    outputVertices = vertices.ToArray();
    outputTriangles = triangles.ToArray();
    outputColors = colors.ToArray();
  }

  public static Mesh CreateMesh(
    NativeList<Vector3> vertices,
    NativeList<int> triangles,
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
