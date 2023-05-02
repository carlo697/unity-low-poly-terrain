using UnityEngine;
using System;
using Unity.Collections;
using System.Runtime.InteropServices;
using Unity.Jobs;
using System.Collections.Generic;

public class CubeGrid {
  public Func<float, float, float, float> samplerFunc;
  public Vector3Int resolution;
  public float threshold;
  public bool useMiddlePoint;
  public bool multithreaded;

  private Vector3Int m_sizes;
  private int m_pointsCount;
  private CubeGridPoint[] m_points;
  private Vector3 m_cubeSize;
  private Mesh m_mesh;

  public CubeGrid(
    Func<float, float, float, float> sampler,
    Vector3Int resolution,
    float threshold = 0f,
    bool useMiddlePoint = false,
    bool multithreaded = false
  ) {
    this.samplerFunc = sampler;
    this.resolution = resolution;
    this.threshold = threshold;
    this.useMiddlePoint = useMiddlePoint;
    this.multithreaded = multithreaded;
  }

  public CubeGridPoint GetPoint(int index) {
    return m_points[index];
  }

  public CubeGridPoint GetPoint(int x, int y, int z) {
    int index = GetIndexFromCoords(x, y, z);
    return m_points[index];
  }

  public Vector3 GetPointPosition(int x, int y, int z) {
    return new Vector3(
      (float)x / ((float)resolution.x),
      (float)y / ((float)resolution.y),
      (float)z / ((float)resolution.z)
    );
  }

  public Vector3Int GetCoordsFromIndex(int index) {
    int z = index % m_sizes.x;
    int y = (index / m_sizes.x) % m_sizes.y;
    int x = index / (m_sizes.y * m_sizes.x);
    return new Vector3Int(x, y, z);
  }

  public int GetIndexFromCoords(int x, int y, int z) {
    return z + y * (resolution.z + 1) + x * (resolution.z + 1) * (resolution.y + 1);
  }

  private void InitializeGrid() {
    // Calculations needed to create the grid array
    m_sizes = new Vector3Int(resolution.x + 1, resolution.y + 1, resolution.z + 1);
    m_pointsCount = m_sizes.x * m_sizes.y * m_sizes.z;

    // Initialize the grid with points (all of them will start with a value = 0)
    m_points = new CubeGridPoint[m_sizes.x * m_sizes.y * m_sizes.z];
    for (int z = 0; z < m_sizes.z; z++) {
      for (int y = 0; y < m_sizes.y; y++) {
        for (int x = 0; x < m_sizes.x; x++) {
          // Get 1D index from the coords
          int index = GetIndexFromCoords(x, y, z);

          // Get the position of the point and set it
          Vector3 pointPosition = GetPointPosition(x, y, z);
          m_points[index] = new CubeGridPoint(pointPosition, 0);
        }
      }
    }

    // This is the size of a single cube inside the grid (each cube consists of 8 points)
    m_cubeSize = new Vector3(
      1f / ((float)resolution.x),
      1f / ((float)resolution.y),
      1f / ((float)resolution.z)
    );
  }

  public void MarchCube(
    ICollection<Vector3> vertices,
    int x,
    int y,
    int z,
    Vector3 position
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

    if (useMiddlePoint) {
      // Use the found case to add the vertices and triangles
      for (int i = 0; i <= 16; i++) {
        int edgeIndex = MarchingCubesConsts.cases[caseIndex, i];
        if (edgeIndex == -1) return;

        Vector3 vertexA = MarchingCubesConsts.edgeVertices[edgeIndex, 0];
        Vector3 vertexB = MarchingCubesConsts.edgeVertices[edgeIndex, 1];
        Vector3 middlePoint = (vertexA + vertexB) / 2;

        vertices.Add(position + Vector3.Scale(middlePoint, m_cubeSize));
      }
    } else {
      for (int i = 0; i <= 16; i++) {
        int edgeIndex = MarchingCubesConsts.cases[caseIndex, i];
        if (edgeIndex == -1) return;

        Vector3 vertexA = MarchingCubesConsts.edgeVertices[edgeIndex, 0];
        Vector3 vertexB = MarchingCubesConsts.edgeVertices[edgeIndex, 1];

        // Find the value in the first vertex of the edge
        int indexVertexA = MarchingCubesConsts.edgeCorners[edgeIndex, 0];
        int indexA = GetIndexFromCoords(
          x + MarchingCubesConsts.corners[indexVertexA].x,
          y + MarchingCubesConsts.corners[indexVertexA].y,
          z + MarchingCubesConsts.corners[indexVertexA].z
        );
        float sampleVertexA = m_points[indexA].value;

        // Find the value in the last vertex of the edge
        int indexVertexB = MarchingCubesConsts.edgeCorners[edgeIndex, 1];
        int indexB = GetIndexFromCoords(
          x + MarchingCubesConsts.corners[indexVertexB].x,
          y + MarchingCubesConsts.corners[indexVertexB].y,
          z + MarchingCubesConsts.corners[indexVertexB].z
        );
        float sampleVertexB = m_points[indexB].value;

        // Calculate the difference and interpolate
        float interpolant = (threshold - sampleVertexA) / (sampleVertexB - sampleVertexA);
        Vector3 interpolatedPosition = Vector3.Lerp(vertexA, vertexB, interpolant);

        vertices.Add(position + Vector3.Scale(interpolatedPosition, m_cubeSize));
      }
    }
  }

  public Mesh Render() {
    var stepTimer = new System.Diagnostics.Stopwatch();
    stepTimer.Start();

    InitializeGrid();

    if (multithreaded) {
      // Store a reference to the sampler function
      GCHandle samplerHandle = GCHandle.Alloc(samplerFunc);

      // Create the sub tasks for the job
      NativeArray<CubeGridPoint> jobPoints =
        new NativeArray<CubeGridPoint>(
          m_points,
          Allocator.TempJob
        );

      // Create job
      CubeGridSampleJob job = new CubeGridSampleJob(resolution, jobPoints, samplerHandle);

      // Execute the job and complete it right away
      JobHandle jobHandle = job.Schedule(jobPoints.Length, 40000); //  80000
      jobHandle.Complete();

      // Get the results
      m_points = job.gridPoints.ToArray();

      // Dispose memory
      jobPoints.Dispose();
      samplerHandle.Free();

    } else {
      // Loop through a 3D grid
      for (int z = 0; z < m_sizes.z; z++) {
        for (int y = 0; y < m_sizes.y; y++) {
          for (int x = 0; x < m_sizes.x; x++) {
            // Get 1D index from the coords
            int index = GetIndexFromCoords(x, y, z);

            // Get the point and set the value
            CubeGridPoint point = m_points[index];
            point.value = samplerFunc(
              point.position.x,
              point.position.y,
              point.position.z
            );

            // Save the point
            m_points[index] = point;
          }
        }
      }
    }

    stepTimer.Stop();
    Debug.Log(string.Format("Grid: {0} ms", stepTimer.ElapsedMilliseconds));

    stepTimer.Restart();

    // Loop through the points to generate the vertices
    List<Vector3> vertices = new List<Vector3>();
    for (int z = 0; z < m_sizes.z - 1; z++) {
      for (int y = 0; y < m_sizes.y - 1; y++) {
        for (int x = 0; x < m_sizes.x - 1; x++) {
          Vector3 pointPosition = GetPointPosition(x, y, z);
          MarchCube(vertices, x, y, z, pointPosition);
        }
      }
    }

    stepTimer.Stop();
    Debug.Log(string.Format("Marching: {0} ms", stepTimer.ElapsedMilliseconds));

    stepTimer.Restart();

    // Loop through the vertices to generate the triangles
    List<int> triangles = new List<int>();
    for (int i = 0; i < vertices.Count; i += 3) {
      triangles.Add(i);
      triangles.Add(i + 1);
      triangles.Add(i + 2);
    }

    // Create a mesh
    if (!m_mesh) {
      m_mesh = new Mesh();
    }
    m_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

    // Set vertices and triangles to the mesh
    m_mesh.Clear();
    if (vertices.Count > 0) {
      m_mesh.vertices = vertices.ToArray();
      m_mesh.triangles = triangles.ToArray();
      m_mesh.RecalculateNormals();
    }

    stepTimer.Stop();
    Debug.Log(
      string.Format(
        "Set vertices, triangles, and recalculate normals: {0} ms",
        stepTimer.ElapsedMilliseconds
      )
    );

    return m_mesh;
  }
}
