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
    var samplerFunc = (VoxelGridSamplerFunc)samplerHandle.Target;

    VoxelGrid grid = new VoxelGrid(
      scale,
      resolution,
      threshold
    );

    MarchingCubes.Generate(
      grid,
      threshold,
      ref vertices,
      ref triangles,
      ref uvs,
      ref colors,
      samplerFunc,
      debug
    );

    for (int i = 0; i < grid.points.Length; i++) {
      this.points.Add(grid.points[i]);
    }
  }
}