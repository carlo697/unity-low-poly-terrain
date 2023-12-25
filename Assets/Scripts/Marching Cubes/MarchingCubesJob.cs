using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.InteropServices;

public struct MarchingCubesJob : IJob {
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
    var stepTimer = new System.Diagnostics.Stopwatch();
    stepTimer.Start();

    var samplerFunc = (VoxelGridSamplerFunc)samplerHandle.Target;

    // Create an instance of the grid
    VoxelGrid grid = new VoxelGrid(
      scale,
      resolution,
      threshold
    );

    // Fill up the grid with values using the sampler function
    grid.Initialize(samplerFunc);

    stepTimer.Stop();
    if (debug) {
      Debug.Log(
        string.Format(
          "Grid: {0} ms, resolution: {1}",
          stepTimer.ElapsedMilliseconds,
          grid.resolution
        )
      );
    }
    stepTimer.Restart();

    // Apply marching cubes to the grid to generate vertices, uvs, colors, etc
    MarchingCubes.MarchCubes(
      grid,
      threshold,
      ref vertices,
      ref triangles,
      ref uvs,
      ref colors
    );

    stepTimer.Stop();
    if (debug) {
      Debug.Log(
        string.Format(
          "Marching: {0} ms, resolution: {1}",
          stepTimer.ElapsedMilliseconds,
          grid.resolution
        )
      );
    }

    for (int i = 0; i < grid.points.Length; i++) {
      this.points.Add(grid.points[i]);
    }
  }
}