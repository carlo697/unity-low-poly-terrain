using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.InteropServices;

public struct CubeGridJob : IJob {
  private NativeList<Vector3> vertices;
  private NativeList<int> triangles;
  private NativeList<Color> colors;
  private GCHandle samplerHandle;
  private GCHandle postProcessingHandle;
  private Vector3 size;
  private Vector3Int resolution;
  private float threshold;
  private bool useMiddlePoint;
  private bool debug;

  public CubeGridJob(
    NativeList<Vector3> vertices,
    NativeList<int> triangles,
    NativeList<Color> colors,
    GCHandle samplerHandle,
    GCHandle postProcessingHandle,
    Vector3 size,
    Vector3Int resolution,
    float threshold = 0f,
    bool useMiddlePoint = false,
    bool debug = false
  ) {
    this.vertices = vertices;
    this.triangles = triangles;
    this.colors = colors;
    this.samplerHandle = samplerHandle;
    this.postProcessingHandle = postProcessingHandle;
    this.size = size;
    this.resolution = resolution;
    this.threshold = threshold;
    this.useMiddlePoint = useMiddlePoint;
    this.debug = debug;
  }

  public void Execute() {
    var samplerFunc = (CubeGridSamplerFunc)samplerHandle.Target;
    var postProcessingFunc = (CubeGridPostProcessingFunc)postProcessingHandle.Target;

    CubeGrid grid = new CubeGrid(
      samplerFunc,
      postProcessingFunc,
      size,
      resolution,
      threshold,
      useMiddlePoint
    );

    Vector3[] vertices;
    int[] triangles;
    Color[] colors;
    grid.Generate(out vertices, out triangles, out colors, debug);

    for (int i = 0; i < vertices.Length; i++) {
      this.vertices.Add(vertices[i]);
    }

    for (int i = 0; i < vertices.Length; i++) {
      this.triangles.Add(triangles[i]);
    }

    for (int i = 0; i < colors.Length; i++) {
      this.colors.Add(colors[i]);
    }
  }
}