using UnityEngine;
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
  public int GetIndexFromCoords(Vector3Int coords) {
    return GetIndexFromCoords(coords.x, coords.y, coords.z);
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

  public void Initialize(VoxelGridSamplerFunc samplerFunc = null) {
    m_points = new VoxelPoint[m_totalPointCount];
    samplerFunc?.Invoke(this);
  }
}
