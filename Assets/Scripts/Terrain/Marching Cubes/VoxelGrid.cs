using UnityEngine;
using Unity.Collections;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public class VoxelGrid : IDisposable {
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

  public VoxelGrid(Vector3 scale, Vector3Int resolution, float threshold = 0f) {
    this.scale = scale;
    this.resolution = resolution;
    this.threshold = threshold;

    // Calculations needed to create the grid array
    m_size = new Vector3Int(resolution.x + 1, resolution.y + 1, resolution.z + 1);
    m_totalPointCount = m_size.x * m_size.y * m_size.z;

    // Create the grid array
    m_points = ArrayPool<VoxelPoint>.Shared.Rent(m_totalPointCount);

    // This value can be useful when the size of the chunk is different from the resolution 
    m_resolutionScaleRatio = new Vector3(
      resolution.x / scale.x,
      resolution.y / scale.y,
      resolution.z / scale.z
    );
  }

  public void InitializePositions() {
    for (int index = 0; index < m_totalPointCount; index++) {
      ref VoxelPoint point = ref m_points[index];
      point.position = GetPointPosition(index);
    }
  }

  public void CopyPointsFrom(NativeList<VoxelPoint> points) {
    points.AsArray().CopyTo(m_points);
  }

  public void CopyPointsFrom(VoxelGrid grid) {
    Array.Copy(grid.points, m_points, m_totalPointCount);
  }

  public void Dispose() {
    ArrayPool<VoxelPoint>.Shared.Return(m_points);
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
  public Vector3 GetPointPosition(int index) {
    Vector3Int coords = GetCoordsFromIndex(index);

    return new Vector3(
      ((float)coords.x / ((float)resolution.x)) * scale.x,
      ((float)coords.y / ((float)resolution.y)) * scale.y,
      ((float)coords.z / ((float)resolution.z)) * scale.z
    );
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
  public Vector3 NormalizeLocalPosition(Vector3 position) {
    return new Vector3(
      position.x / scale.x,
      position.y / scale.y,
      position.z / scale.z
    );
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public int GetNearestIndexAt(Vector3 position) {
    Vector3Int coordsSample = GetNearestCoordsAt(position);
    return GetIndexFromCoords(coordsSample);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Vector3Int GetNearestCoordsAt(Vector3 position) {
    Vector3 normalizedMiddlePoint = NormalizeLocalPosition(position);
    return new Vector3Int(
      Mathf.RoundToInt(normalizedMiddlePoint.x * resolution.x),
      Mathf.RoundToInt(normalizedMiddlePoint.y * resolution.y),
      Mathf.RoundToInt(normalizedMiddlePoint.z * resolution.z)
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
    float sumX = 0f;
    float sumY = 0f;
    float sumZ = 0f;

    // Left
    if (x > 0)
      sumX = (value - GetPointRef(x - 1, y, z).value) * m_resolutionScaleRatio.x;

    // Right
    if (x < resolution.x)
      sumX -= (value - GetPointRef(x + 1, y, z).value) * m_resolutionScaleRatio.x;

    // Down
    if (y > 0)
      sumY = (value - GetPointRef(x, y - 1, z).value) * m_resolutionScaleRatio.y;

    // Up
    if (y < resolution.y)
      sumY -= (value - GetPointRef(x, y + 1, z).value) * m_resolutionScaleRatio.y;

    // Back
    if (z > 0)
      sumZ = (value - GetPointRef(x, y, z - 1).value) * m_resolutionScaleRatio.z;

    // Forward
    if (z < resolution.z)
      sumZ -= (value - GetPointRef(x, y, z + 1).value) * m_resolutionScaleRatio.z;

    float inverseSquareRoot = MathUtils.FastInvSqrt(sumX * sumX + sumY * sumY + sumZ * sumZ);
    return new Vector3(sumX * inverseSquareRoot, sumY * inverseSquareRoot, sumZ * inverseSquareRoot);
  }
}
