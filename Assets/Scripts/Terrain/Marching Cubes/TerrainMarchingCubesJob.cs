using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.InteropServices;

public delegate void TerrainSamplerFunc(VoxelGrid grid);

public struct TerrainMarchingCubesJob : IJob {
  public NativeList<Vector3> vertices;
  public NativeList<int> triangles;
  public NativeList<Vector3> uvs;
  public NativeList<Color> colors;
  public NativeList<VoxelPoint> points;
  public GCHandle samplerHandle;
  public Vector3 scale;
  public Vector3Int resolution;
  public float threshold;
  public bool debug;

  public void Execute() {
    var timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    var samplerFunc = (TerrainSamplerFunc)samplerHandle.Target;

    // Create an instance of the grid
    VoxelGrid grid = new VoxelGrid(
      scale,
      resolution,
      threshold
    );

    samplerFunc.Invoke(grid);

    timer.Stop();
    if (debug) {
      Debug.Log($"Grid: {timer.ElapsedMilliseconds} ms, resolution: {grid.resolution}");
    }
    timer.Restart();

    // Apply marching cubes to the grid to generate vertices, uvs, colors, etc
    MarchingCubes.MarchCubes(
      grid,
      threshold,
      ref vertices,
      ref triangles,
      ref uvs,
      ref colors
    );

    timer.Stop();
    if (debug) {
      Debug.Log($"Marching: {timer.ElapsedMilliseconds} ms, resolution: {grid.resolution}");
    }

    for (int i = 0; i < grid.points.Length; i++) {
      this.points.Add(grid.points[i]);
    }
  }
}