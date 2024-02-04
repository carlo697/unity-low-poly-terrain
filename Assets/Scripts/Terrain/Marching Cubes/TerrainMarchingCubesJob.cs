using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public delegate void TerrainSamplerFunc(
  VoxelGrid grid,
  TerrainMarchingCubesJob.ManagedData managedData
);

public struct TerrainMarchingCubesJob : IJob {
  public class ManagedData {
    public TerrainSamplerFunc samplerFunc;
    public Dictionary<Biome, float[]> biomeMasks;
  }

  public NativeList<Vector3> vertices;
  public NativeList<int> triangles;
  public NativeList<Vector3> uvs;
  public NativeList<Color> colors;
  public NativeList<VoxelPoint> points;
  public NativeList<uint> triangleMaterials;
  public GCHandle samplerHandle;
  public Vector3 scale;
  public Vector3Int resolution;
  public float threshold;
  public bool debug;
  public GCHandle managedDataHandle;

  public void Execute() {
    var timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    var managedData = (ManagedData)managedDataHandle.Target;

    // Create an instance of the grid and use it
    using (VoxelGrid grid = new VoxelGrid(scale, resolution, threshold)) {
      grid.InitializePositions();
      managedData.samplerFunc.Invoke(grid, managedData);

      timer.Stop();
      if (debug) {
        Debug.Log($"Grid: {timer.Elapsed.TotalMilliseconds} ms, resolution: {grid.resolution}");
      }
      timer.Restart();

      // Apply marching cubes to the grid to generate vertices, uvs, colors, etc
      MarchingCubes.MarchCubes(
        grid,
        threshold,
        ref vertices,
        ref triangles,
        ref uvs,
        ref colors,
        ref triangleMaterials
      );

      timer.Stop();
      if (debug) {
        Debug.Log($"Marching: {timer.Elapsed.TotalMilliseconds} ms, resolution: {grid.resolution}");
      }

      for (int i = 0; i < grid.points.Length; i++) {
        this.points.Add(grid.points[i]);
      }
    }
  }
}