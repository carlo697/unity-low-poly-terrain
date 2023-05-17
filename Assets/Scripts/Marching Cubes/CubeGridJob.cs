using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.InteropServices;

public struct CubeGridJob : IJob {
  private NativeList<Vector3> vertices;
  private NativeList<int> triangles;
  private GCHandle samplerHandle;
  private Vector3 size;
  private Vector3Int resolution;
  private float threshold;
  private bool useMiddlePoint;
  private bool debug;

  public CubeGridJob(
    NativeList<Vector3> vertices,
    NativeList<int> triangles,
    GCHandle samplerHandle,
    Vector3 size,
    Vector3Int resolution,
    float threshold = 0f,
    bool useMiddlePoint = false,
    bool debug = false
  ) {
    this.vertices = vertices;
    this.triangles = triangles;
    this.samplerHandle = samplerHandle;
    this.size = size;
    this.resolution = resolution;
    this.threshold = threshold;
    this.useMiddlePoint = useMiddlePoint;
    this.debug = debug;
  }

  public void Execute() {
    var samplerFunc = (System.Func<float, float, float, float>)samplerHandle.Target;

    CubeGrid grid = new CubeGrid(
      samplerFunc,
      size,
      resolution,
      threshold,
      useMiddlePoint
    );

    Vector3[] vertices;
    int[] triangles;
    grid.Generate(out vertices, out triangles, debug);

    for (int i = 0; i < vertices.Length; i++) {
      this.vertices.Add(vertices[i]);
    }

    for (int i = 0; i < vertices.Length; i++) {
      this.triangles.Add(triangles[i]);
    }
  }
}