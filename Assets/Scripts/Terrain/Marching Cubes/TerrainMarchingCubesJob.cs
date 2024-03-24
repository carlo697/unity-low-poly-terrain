using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public delegate void TerrainSamplerFunc(
  Vector3Int resolution,
  Vector3 position,
  Vector3 scale,
  float noiseScale,
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
  public Vector3Int resolution;
  public Vector3 position;
  public Vector3 scale;
  public float noiseScale;
  public float threshold;
  public int upsamplingLevel;
  public bool debug;
  public GCHandle managedDataHandle;

  public void Execute() {
    var totalTimeWatch = new System.Diagnostics.Stopwatch();
    totalTimeWatch.Start();

    var managedData = (ManagedData)managedDataHandle.Target;

    // Generate the grid
    VoxelGrid grid;
    int upsampling = Mathf.FloorToInt(Mathf.Pow(2f, upsamplingLevel));
    if (upsampling >= 2) {
      grid = GenerateGridWithUpsampling(upsampling);
    } else {
      grid = GenerateGrid();
    }

    // Log time
    totalTimeWatch.Stop();
    if (debug) {
      Debug.Log($"Grid: {totalTimeWatch.Elapsed.TotalMilliseconds} ms, resolution: {grid.resolution}");
    }
    totalTimeWatch.Restart();

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

    // Log time
    totalTimeWatch.Stop();
    if (debug) {
      Debug.Log($"Marching: {totalTimeWatch.Elapsed.TotalMilliseconds} ms, resolution: {grid.resolution}");
    }

    for (int i = 0; i < grid.totalPointCount; i++) {
      this.points.Add(grid.points[i]);
    }

    grid.Dispose();
  }

  private VoxelGrid GenerateGrid() {
    var managedData = (ManagedData)managedDataHandle.Target;

    // Create a grid with half the resolution
    VoxelGrid grid = new VoxelGrid(scale, resolution, threshold);
    grid.InitializePositions();

    System.Diagnostics.Stopwatch noiseTimeWatch = new();
    noiseTimeWatch.Start();

    // Create a grid with half the resolution
    managedData.samplerFunc.Invoke(resolution, position, scale, noiseScale, grid, managedData);

    // Log time
    noiseTimeWatch.Stop();
    if (debug) {
      Debug.Log($"Sampling noise: {noiseTimeWatch.Elapsed.TotalMilliseconds}");
    }

    return grid;
  }

  private VoxelGrid GenerateGridWithUpsampling(int upsampling) {
    float inverseUpsampling = 1f / (float)upsampling;
    var managedData = (ManagedData)managedDataHandle.Target;

    // Create a grid with lower resolution
    VoxelGrid lowResGrid = new VoxelGrid(scale, resolution / upsampling, threshold);
    lowResGrid.InitializePositions();

    System.Diagnostics.Stopwatch noiseTimeWatch = new();
    noiseTimeWatch.Start();

    // Sample the grid of low resolution
    managedData.samplerFunc.Invoke(
      resolution / upsampling,
      position,
      scale,
      noiseScale,
      lowResGrid,
      managedData
    );

    noiseTimeWatch.Stop();
    if (debug) {
      Debug.Log($"Sampling noise: {noiseTimeWatch.Elapsed.TotalMilliseconds}");
    }

    // Create the final grid with full resolution
    VoxelGrid grid = new VoxelGrid(scale, resolution, threshold);
    System.Diagnostics.Stopwatch trilinearWatch = new();
    trilinearWatch.Start();

    // Apply final data to the full resolution grid using trilinear interpolation
    for (int i = 0; i < grid.totalPointCount; i++) {
      // Get the current point
      Vector3Int coords = grid.GetCoordsFromIndex(i);
      ref VoxelPoint targetPoint = ref grid.GetPointRef(coords.x, coords.y, coords.z);

      // Convert the coordinates to normalized values
      Vector3 normalizedPosition = grid.GetPointNormalizedPosition(coords.x, coords.y, coords.z);

      // Get the coordinates of the current voxel at the low resolution grid
      Vector3Int lowResCoords = new Vector3Int(
        Mathf.Min(Mathf.FloorToInt(normalizedPosition.x * lowResGrid.resolution.x), lowResGrid.maxCoords.x),
        Mathf.Min(Mathf.FloorToInt(normalizedPosition.y * lowResGrid.resolution.y), lowResGrid.maxCoords.y),
        Mathf.Min(Mathf.FloorToInt(normalizedPosition.z * lowResGrid.resolution.z), lowResGrid.maxCoords.z)
      );

      // ref VoxelPoint p000 = ref lowResGrid.GetPointRef(lowResCoords.x, lowResCoords.y, lowResCoords.z);
      // ref VoxelPoint p100 = ref lowResGrid.GetPointRef(lowResCoords.x + 1, lowResCoords.y, lowResCoords.z);
      // ref VoxelPoint p110 = ref lowResGrid.GetPointRef(lowResCoords.x + 1, lowResCoords.y + 1, lowResCoords.z);
      // ref VoxelPoint p010 = ref lowResGrid.GetPointRef(lowResCoords.x, lowResCoords.y + 1, lowResCoords.z);
      // ref VoxelPoint p001 = ref lowResGrid.GetPointRef(lowResCoords.x, lowResCoords.y, lowResCoords.z + 1);
      // ref VoxelPoint p101 = ref lowResGrid.GetPointRef(lowResCoords.x + 1, lowResCoords.y, lowResCoords.z + 1);
      // ref VoxelPoint p111 = ref lowResGrid.GetPointRef(lowResCoords.x + 1, lowResCoords.y + 1, lowResCoords.z + 1);
      // ref VoxelPoint p011 = ref lowResGrid.GetPointRef(lowResCoords.x, lowResCoords.y + 1, lowResCoords.z + 1);

      // Cache some values for better performance
      int sizeX = lowResCoords.x * lowResGrid.size.z * lowResGrid.size.y;
      int sizeOffsetX = (lowResCoords.x + 1) * lowResGrid.size.z * lowResGrid.size.y;
      int sizeY = lowResCoords.y * lowResGrid.size.z;
      int sizeOffsetY = (lowResCoords.y + 1) * lowResGrid.size.z;
      int sizeOffsetZ = (lowResCoords.z + 1);

      // Get the eight corners of the voxel at the low resolution grid
      int i000 = lowResCoords.z + sizeY + sizeX;
      ref VoxelPoint p000 = ref lowResGrid.points[i000];
      int i100 = lowResCoords.z + sizeY + sizeOffsetX;
      ref VoxelPoint p100 = ref lowResGrid.points[i100];
      int i110 = lowResCoords.z + sizeOffsetY + sizeOffsetX;
      ref VoxelPoint p110 = ref lowResGrid.points[i110];
      int i010 = lowResCoords.z + sizeOffsetY + sizeX;
      ref VoxelPoint p010 = ref lowResGrid.points[i010];
      int i001 = sizeOffsetZ + sizeY + sizeX;
      ref VoxelPoint p001 = ref lowResGrid.points[i001];
      int i101 = sizeOffsetZ + sizeY + sizeOffsetX;
      ref VoxelPoint p101 = ref lowResGrid.points[i101];
      int i111 = sizeOffsetZ + sizeOffsetY + sizeOffsetX;
      ref VoxelPoint p111 = ref lowResGrid.points[i111];
      int i011 = sizeOffsetZ + sizeOffsetY + sizeX;
      ref VoxelPoint p011 = ref lowResGrid.points[i011];

      // Start trilinear interpolation
      float diffX = (coords.x - lowResCoords.x * upsampling) * inverseUpsampling;
      float diffY = (coords.y - lowResCoords.y * upsampling) * inverseUpsampling;
      float diffZ = (coords.z - lowResCoords.z * upsampling) * inverseUpsampling;
      // float v00 = Mathf.LerpUnclamped(p000.value, p100.value, diffX);
      // float v01 = Mathf.LerpUnclamped(p001.value, p101.value, diffX);
      // float v10 = Mathf.LerpUnclamped(p010.value, p110.value, diffX);
      // float v11 = Mathf.LerpUnclamped(p011.value, p111.value, diffX);
      // float v0 = Mathf.LerpUnclamped(v00, v10, diffY);
      // float v1 = Mathf.LerpUnclamped(v01, v11, diffY);
      // float value = Mathf.LerpUnclamped(v0, v1, diffZ);
      float v00 = p000.value + (p100.value - p000.value) * diffX;
      float v01 = p001.value + (p101.value - p001.value) * diffX;
      float v10 = p010.value + (p110.value - p010.value) * diffX;
      float v11 = p011.value + (p111.value - p011.value) * diffX;
      float v0 = v00 + (v10 - v00) * diffY;
      float v1 = v01 + (v11 - v01) * diffY;
      float value = v0 + (v1 - v0) * diffZ;

      // Final values for the point
      targetPoint.color = p000.color;
      targetPoint.material = p000.material;
      targetPoint.roughness = p000.roughness;
      targetPoint.value = value;

    }

    trilinearWatch.Stop();

    if (debug) {
      Debug.Log($"Trilinear upsampling: {trilinearWatch.Elapsed.TotalMilliseconds}");
    }

    lowResGrid.Dispose();

    return grid;
  }
}