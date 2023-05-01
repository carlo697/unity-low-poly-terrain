using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.InteropServices;

public struct GridJob : IJobParallelFor {
  [ReadOnly] public Vector3Int gridResolution;
  public NativeArray<GridPoint> gridPoints;

  [ReadOnly] public GCHandle samplerHandle;

  [ReadOnly] private Vector3Int m_gridSize;

  public GridJob(
    Vector3Int gridResolution,
    NativeArray<GridPoint> points,
    GCHandle samplerHandle
  ) {
    this.gridResolution = gridResolution;
    this.gridPoints = points;
    this.samplerHandle = samplerHandle;

    // Calculate
    this.m_gridSize = new Vector3Int(
      gridResolution.x + 1,
      gridResolution.y + 1,
      gridResolution.z + 1
    );
  }

  public Vector3Int GetCoordsFromIndex(int index) {
    int z = index % m_gridSize.x;
    int y = (index / m_gridSize.x) % m_gridSize.y;
    int x = index / (m_gridSize.y * m_gridSize.x);
    return new Vector3Int(x, y, z);
  }

  public void Execute(int index) {
    var sampler = (System.Func<float, float, float, float>)samplerHandle.Target;

    // Convert the index to the coordinates inside the grid
    Vector3Int coords = GetCoordsFromIndex(index);

    // Get the point and sample its value
    GridPoint gridPoint = gridPoints[index];
    gridPoint.value = sampler(
      (float)coords.x / ((float)gridResolution.x),
      (float)coords.y / ((float)gridResolution.y),
      (float)coords.z / ((float)gridResolution.z)
    );

    // Assign the point again
    gridPoints[index] = gridPoint;
  }
}