
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

[RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshRenderer))]
public class TerrainChunkWater : MonoBehaviour {
  public Vector2Int resolution = new Vector2Int(32, 32);
  public Vector2 size = new Vector2(32f, 32f);
  public float seaLevel;

  private MeshFilter m_meshFilter;
  private NativeList<Vector3> m_vertices;
  private NativeList<int> m_triangles;
  private JobHandle? m_handle;

  void Awake() {
    m_meshFilter = GetComponent<MeshFilter>();
  }

  void Start() {
    // Create the lists for the job
    m_vertices = new NativeList<Vector3>(Allocator.TempJob);
    m_triangles = new NativeList<int>(Allocator.TempJob);

    // Create job
    TerrainChunkWaterJob job = new TerrainChunkWaterJob(
      resolution,
      size,
      m_vertices,
      m_triangles
    );
    this.m_handle = job.Schedule();
  }

  void DisposeJob() {
    m_vertices.Dispose();
    m_triangles.Dispose();
    m_handle = null;
  }

  void CancelJob() {
    m_handle.Value.Complete();
    m_handle = null;
  }

  void LateUpdate() {
    if (m_handle.HasValue && m_handle.Value.IsCompleted) {
      // Complete the job
      m_handle.Value.Complete();

      // Create mesh
      Mesh mesh = new Mesh();
      mesh.SetVertices<Vector3>(m_vertices);
      mesh.SetIndices<int>(m_triangles, MeshTopology.Triangles, 0);
      mesh.RecalculateNormals();

      // Extend bounds vertically
      Bounds newBounds = mesh.bounds;
      newBounds.size = new Vector3(newBounds.size.x, 2f, newBounds.size.z);
      mesh.bounds = newBounds;

      // Dispose memory
      DisposeJob();

      // Apply mesh
      m_meshFilter.sharedMesh = mesh;
    }
  }

  void OnDestroy() {
    Destroy(m_meshFilter.sharedMesh);

    if (m_handle.HasValue) {
      Debug.Log("Chunk destroyed and there was a job running");
      CancelJob();
    }
  }
}
