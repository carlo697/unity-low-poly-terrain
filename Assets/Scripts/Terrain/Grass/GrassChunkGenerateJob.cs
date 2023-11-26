using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.InteropServices;

public struct GrassChunkGenerateJob : IJob {
  public Vector3 chunkPosition;
  public int chunkSeed;
  public Vector3 cameraPosition;
  public NativeList<GrassInstance> instances;
  [ReadOnly] public GCHandle spawners;
  [ReadOnly] public Mesh.MeshDataArray meshDataArray;
  public bool logTime;

  public void Execute() {
    var timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    // Determine seed
    ulong seed = (ulong)(this.chunkSeed + chunkPosition.GetHashCode());
    int totalGrassPatches = 0;

    // Extract mesh data
    Mesh.MeshData data = meshDataArray[0];
    const int submesh = 0;
    int indexCount = data.GetSubMesh(submesh).indexCount;
    NativeArray<int> triangles = new NativeArray<int>(indexCount, Allocator.Temp);
    NativeArray<Vector3> vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
    NativeArray<Vector3> uvs = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
    data.GetIndices(triangles, submesh);
    data.GetVertices(vertices);
    data.GetUVs(0, uvs);

    GrassSpawner[] spawners = (GrassSpawner[])this.spawners.Target;

    for (int spawnerIdx = 0; spawnerIdx < spawners.Length; spawnerIdx++) {
      GrassSpawner spawner = spawners[spawnerIdx];
      spawner.InitializeForGeneration();
    }

    // Iterate the triangles of the mesh
    for (int i = 0; i < triangles.Length; i += 3) {
      Vector3 a = vertices[i];
      Vector3 b = vertices[i + 1];
      Vector3 c = vertices[i + 2];
      Vector3 ab = b - a;
      Vector3 ac = c - a;

      // Calculate normal
      Vector3 unnormalizedNormal = Vector3.Cross(ab, ac);

      // Calculate area of triangle
      float area = unnormalizedNormal.magnitude / 2f;

      // Get material of the triangle
      uint materialId = MaterialBitConverter.FloatToMaterialId(uvs[i].x);

      // Determine what spawners to use in this triangle
      for (int spawnerIdx = 0; spawnerIdx < spawners.Length; spawnerIdx++) {
        GrassSpawner spawner = spawners[spawnerIdx];
        totalGrassPatches += spawner.Spawn(
          instances,
          chunkPosition,
          (ulong)i + seed,
          i,
          a,
          b,
          c,
          area,
          unnormalizedNormal,
          materialId
        );
      }
    }

    // Free memory
    triangles.Dispose();
    vertices.Dispose();
    uvs.Dispose();

    if (logTime) {
      Debug.LogFormat(
        "Grass: {0} ms, Triangles: {1}, Instances: {2}",
        timer.ElapsedMilliseconds,
        indexCount / 3,
        instances.Length
      );
    }
  }
}