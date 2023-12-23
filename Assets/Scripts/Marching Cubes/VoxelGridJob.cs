using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.InteropServices;

public struct VoxelGridJob : IJob {
  private NativeList<Vector3> vertices;
  private NativeList<int> triangles;
  private NativeList<Vector3> uvs;
  private NativeList<Color> colors;
  private NativeList<VoxelPoint> points;
  private GCHandle samplerHandle;
  private Vector3 scale;
  private Vector3Int resolution;
  private float threshold;
  private bool debug;

  public VoxelGridJob(
    NativeList<Vector3> vertices,
    NativeList<int> triangles,
    NativeList<Vector3> uvs,
    NativeList<Color> colors,
    NativeList<VoxelPoint> points,
    Vector3 scale,
    Vector3Int resolution,
    GCHandle samplerHandle,
    float threshold = 0f,
    bool debug = false
  ) {
    this.vertices = vertices;
    this.triangles = triangles;
    this.uvs = uvs;
    this.colors = colors;
    this.points = points;
    this.samplerHandle = samplerHandle;
    this.scale = scale;
    this.resolution = resolution;
    this.threshold = threshold;
    this.debug = debug;
  }

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