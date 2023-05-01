using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.InteropServices;
using System;

public class MarchingCubesExample : MonoBehaviour {
  public Vector3Int resolution = Vector3Int.one * 10;
  public float noiseSize = 1f;
  public int noiseOctaves = 3;
  public Vector3 noiseOffset = Vector3.zero;
  public bool multithreaded;

  public float threshold = 0.5f;
  public bool useMiddlePoint = false;

  public bool drawGizmos = true;
  public float gizmosSize = 0.1f;

  private Vector3Int m_gridSize;
  private GridPoint[] m_gridPoints;
  private Vector3 m_gridCubeSize;

  private bool m_rebuildFlag;
  private Mesh m_mesh;
  private MeshFilter m_meshFilter;
  private MeshRenderer m_meshRenderer;



  void Start() {

  }

  void Update() {

  }

  Vector3 GetPointPosition(int x, int y, int z) {
    return new Vector3(
      (float)x / ((float)resolution.x),
      (float)y / ((float)resolution.y),
      (float)z / ((float)resolution.z)
    );
  }

  public Vector3Int GetCoordsFromIndex(int index) {
    int z = index % m_gridSize.x;
    int y = (index / m_gridSize.x) % m_gridSize.y;
    int x = index / (m_gridSize.y * m_gridSize.x);
    return new Vector3Int(x, y, z);
  }

  public int GetIndexFromCoords(int x, int y, int z) {
    return z + y * (resolution.z + 1) + x * (resolution.z + 1) * (resolution.y + 1);
  }

  [ContextMenu("InstantRegenerate")]
  public void InstantRegenerate() {
    var totalTimer = new System.Diagnostics.Stopwatch();
    totalTimer.Start();

    var stepTimer = new System.Diagnostics.Stopwatch();
    stepTimer.Start();

    // Calculations needed to create the grid array
    m_gridSize = new Vector3Int(resolution.x + 1, resolution.y + 1, resolution.z + 1);
    int pointsCount = m_gridSize.x * m_gridSize.y * m_gridSize.z;

    // Initialize the grid with points (all of them will start with a value = 0)
    m_gridPoints = new GridPoint[m_gridSize.x * m_gridSize.y * m_gridSize.z];
    for (int z = 0; z < m_gridSize.z; z++) {
      for (int y = 0; y < m_gridSize.y; y++) {
        for (int x = 0; x < m_gridSize.x; x++) {
          // Get 1D index from the coords
          int index = GetIndexFromCoords(x, y, z);

          // Get the position of the point and set it
          Vector3 pointPosition = GetPointPosition(x, y, z);
          m_gridPoints[index] = new GridPoint(pointPosition, 0);
        }
      }
    }

    // This is the size of a single cube inside the grid (each cube consists of 8 points)
    m_gridCubeSize = new Vector3(
      1f / ((float)resolution.x),
      1f / ((float)resolution.y),
      1f / ((float)resolution.z)
    );

    // Generate noise
    FractalNoise noise = new FractalNoise(1 / noiseSize, 1f, 0.5f, noiseOctaves);

    if (multithreaded) {
      // This function will be used in the threads to sample the grid
      Func<float, float, float, float> jobSampler = (float x, float y, float z) => {
        return noise.Sample(
          x + noiseOffset.x,
          y + noiseOffset.y,
          z + noiseOffset.z
        );
      };

      // Create the sub tasks for the job
      NativeArray<GridPoint> jobPoints = new NativeArray<GridPoint>(pointsCount, Allocator.TempJob);

      // Create job
      GridJob job = new GridJob(resolution, jobPoints, GCHandle.Alloc(jobSampler));

      // Execute the job and complete it right away
      JobHandle jobHandle = job.Schedule(jobPoints.Length, 40000); //  80000
      jobHandle.Complete();

      // Get the results
      m_gridPoints = job.gridPoints.ToArray();

      // Dispose memory
      jobPoints.Dispose();
    } else {
      // Loop through a 3D grid
      for (int z = 0; z < m_gridSize.z; z++) {
        for (int y = 0; y < m_gridSize.y; y++) {
          for (int x = 0; x < m_gridSize.x; x++) {
            // Get 1D index from the coords
            int index = GetIndexFromCoords(x, y, z);

            // Get the point and set the value
            GridPoint point = m_gridPoints[index];
            point.value = noise.Sample(
              point.position.x + noiseOffset.x,
              point.position.y + noiseOffset.y,
              point.position.z + noiseOffset.z
            );

            // Save the point
            m_gridPoints[index] = point;
          }
        }
      }
    }

    stepTimer.Stop();
    Debug.Log(string.Format("Grid: {0} ms", stepTimer.ElapsedMilliseconds));

    stepTimer.Restart();

    // Loop through the points to generate the vertices
    List<Vector3> vertices = new List<Vector3>();
    for (int z = 0; z < m_gridSize.z - 1; z++) {
      for (int y = 0; y < m_gridSize.y - 1; y++) {
        for (int x = 0; x < m_gridSize.x - 1; x++) {
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
    m_mesh.name = gameObject.name;
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

    stepTimer.Restart();

    // Add a mesh filter
    m_meshFilter = GetComponent<MeshFilter>();
    if (!m_meshFilter) {
      m_meshFilter = gameObject.AddComponent<MeshFilter>();
    }
    m_meshFilter.sharedMesh = m_mesh;

    // Add a mesh renderer
    m_meshRenderer = GetComponent<MeshRenderer>();
    if (!m_meshRenderer) {
      m_meshRenderer = gameObject.AddComponent<MeshRenderer>();
    }

    // Check if it has a mesh collider
    MeshCollider collider = GetComponent<MeshCollider>();
    if (collider) {
      collider.sharedMesh = m_mesh;
    }

    stepTimer.Stop();
    Debug.Log(
      string.Format(
        "Apply to components: {0} ms",
        stepTimer.ElapsedMilliseconds
      )
    );

    totalTimer.Stop();
    Debug.Log(string.Format("Total: {0} ms", totalTimer.ElapsedMilliseconds));
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
      float sample = m_gridPoints[sampleIndex].value;

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

        vertices.Add(position + Vector3.Scale(middlePoint, m_gridCubeSize));
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
        float sampleVertexA = m_gridPoints[indexA].value;

        // Find the value in the last vertex of the edge
        int indexVertexB = MarchingCubesConsts.edgeCorners[edgeIndex, 1];
        int indexB = GetIndexFromCoords(
          x + MarchingCubesConsts.corners[indexVertexB].x,
          y + MarchingCubesConsts.corners[indexVertexB].y,
          z + MarchingCubesConsts.corners[indexVertexB].z
        );
        float sampleVertexB = m_gridPoints[indexB].value;

        // Calculate the difference and interpolate
        float interpolant = (threshold - sampleVertexA) / (sampleVertexB - sampleVertexA);
        Vector3 interpolatedPosition = Vector3.Lerp(vertexA, vertexB, interpolant);

        vertices.Add(position + Vector3.Scale(interpolatedPosition, m_gridCubeSize));
      }
    }
  }



  public void FlagToRegenerate() {
    m_rebuildFlag = true;
  }

  private void RegenerateIfNeeded() {
    if (m_rebuildFlag || m_gridPoints == null || !m_mesh) {
      InstantRegenerate();
      m_rebuildFlag = false;
    }
  }

  private void OnValidate() {
    FlagToRegenerate();
  }

  private void OnDrawGizmos() {
    RegenerateIfNeeded();

    if (!drawGizmos) return;

    for (int z = 0; z < resolution.z; z++) {
      for (int y = 0; y < resolution.y; y++) {
        for (int x = 0; x < resolution.x; x++) {
          Vector3 pointPosition = GetPointPosition(x, y, z);
          Vector3 globalPointPosition = transform.TransformPoint(pointPosition);

          int index = GetIndexFromCoords(x, y, z);
          float value = m_gridPoints[index].value;
          Gizmos.color = new Color(value, value, value);
          Gizmos.DrawCube(globalPointPosition, Vector3.one * gizmosSize);
        }
      }
    }
  }
}
