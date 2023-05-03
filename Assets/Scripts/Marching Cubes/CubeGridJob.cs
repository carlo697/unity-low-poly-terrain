using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.InteropServices;

public struct CubeGridJob : IJob {
  private NativeList<Vector3> vertices;
  private NativeList<int> triangles;
  private GCHandle samplerHandle;
  private Vector3Int resolution;
  private float threshold;
  private bool useMiddlePoint;

  public CubeGridJob(
    NativeList<Vector3> vertices,
    NativeList<int> triangles,
    GCHandle samplerHandle,
    Vector3Int resolution,
    float threshold = 0f,
    bool useMiddlePoint = false,
    bool multithreaded = false
  ) {
    this.vertices = vertices;
    this.triangles = triangles;
    this.samplerHandle = samplerHandle;
    this.resolution = resolution;
    this.threshold = threshold;
    this.useMiddlePoint = useMiddlePoint;
  }

  public void Execute() {
    var samplerFunc = (System.Func<float, float, float, float>)samplerHandle.Target;

    CubeGrid grid = new CubeGrid(
      samplerFunc,
      resolution,
      threshold,
      useMiddlePoint
    );

    Vector3[] vertices;
    int[] triangles;
    grid.Generate(out vertices, out triangles);

    for (int i = 0; i < vertices.Length; i++) {
      this.vertices.Add(vertices[i]);
    }

    for (int i = 0; i < vertices.Length; i++) {
      this.triangles.Add(triangles[i]);
    }
  }
}