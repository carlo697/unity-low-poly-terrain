using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.InteropServices;

public struct CubeGridJob : IJob {
  private NativeList<Vector3> vertices;
  private NativeList<int> triangles;
  private NativeList<Vector3> uvs;
  private NativeList<Color> colors;
  private NativeList<CubeGridPoint> points;
  private GCHandle samplerHandle;
  private Vector3 size;
  private Vector3Int resolution;
  private float threshold;
  private bool debug;

  public CubeGridJob(
    NativeList<Vector3> vertices,
    NativeList<int> triangles,
    NativeList<Vector3> uvs,
    NativeList<Color> colors,
    NativeList<CubeGridPoint> points,
    Vector3 size,
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
    this.size = size;
    this.resolution = resolution;
    this.threshold = threshold;
    this.debug = debug;
  }

  public void Execute() {
    var samplerFunc = (CubeGridSamplerFunc)samplerHandle.Target;

    CubeGrid grid = new CubeGrid(
      size,
      resolution,
      threshold
    );

    grid.Generate(
      ref vertices,
      ref triangles,
      ref uvs,
      ref colors,
      samplerFunc,
      debug
    );

    for (int i = 0; i < grid.gridPoints.Length; i++) {
      this.points.Add(grid.gridPoints[i]);
    }
  }
}