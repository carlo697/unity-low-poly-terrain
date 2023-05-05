using UnityEngine;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;

[ExecuteInEditMode]
public class TerrainChunk : MonoBehaviour {
  public Vector3Int resolution = Vector3Int.one * 10;
  public float noiseSize = 1f;
  public int noiseOctaves = 3;
  public Vector3 noiseOffset = Vector3.zero;
  public bool multithreaded = true;

  public float threshold = 0f;
  public bool useMiddlePoint = false;

  public bool drawGizmos = true;
  public float gizmosSize = 0.5f;

  [SerializeField] private bool m_rebuildFlag;
  private MeshFilter m_meshFilter;
  private MeshRenderer m_meshRenderer;

  private GCHandle samplerHandle;
  private NativeList<Vector3> vertices;
  private NativeList<int> triangles;
  JobHandle? handle;

  void Awake() {
    // Add a mesh filter
    m_meshFilter = GetComponent<MeshFilter>();
    if (!m_meshFilter) {
      m_meshFilter = gameObject.AddComponent<MeshFilter>();
    }

    // Add a mesh renderer
    m_meshRenderer = GetComponent<MeshRenderer>();
    if (!m_meshRenderer) {
      m_meshRenderer = gameObject.AddComponent<MeshRenderer>();
    }
  }

  void Start() {
    RegenerateIfNeeded();
  }

  [ContextMenu("InstantRegenerate")]
  public void ScheduleRegeneration() {
    if (handle != null) {
      Debug.Log("There was already a handle running");
      CancelJob();
    }

    // Generate noise
    FractalNoise noise = new FractalNoise(1 / noiseSize, 1f, 0.5f, noiseOctaves);
    Func<float, float, float, float> samplerFunc = (float x, float y, float z) => {
      // For supporting non symmetrical grids we need to mutiply each
      // coord by the resolution to get symmetrical noise
      return noise.Sample(
        (x + noiseOffset.x) * resolution.x,
        (y + noiseOffset.y) * resolution.y,
        (z + noiseOffset.z) * resolution.z
      );
    };

    // Create a new grid
    // Store a reference to the sampler function
    samplerHandle = GCHandle.Alloc(samplerFunc);

    // Create the sub tasks for the job
    vertices = new NativeList<Vector3>(Allocator.Persistent);
    triangles = new NativeList<int>(Allocator.Persistent);

    // Create job
    CubeGridJob job = new CubeGridJob(
      vertices,
      triangles,
      samplerHandle,
      resolution,
      threshold,
      useMiddlePoint,
      multithreaded
    );

    // Execute the job and complete it right away
    this.handle = job.Schedule();
  }

  void Update() {
    RegenerateIfNeeded();
  }

  void DisposeJob() {
    vertices.Dispose();
    triangles.Dispose();
    samplerHandle.Free();
    handle = null;
  }

  void CancelJob() {
    handle.Value.Complete();
    vertices.Dispose();
    triangles.Dispose();
    samplerHandle.Free();
    handle = null;
  }

  void LateUpdate() {
    if (handle != null && handle.Value.IsCompleted) {
      System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
      timer.Start();

      // Complete the job
      handle.Value.Complete();

      // Get the results
      Vector3[] finalVertices = this.vertices.ToArray();
      int[] finalTriangles = this.triangles.ToArray();

      // Dispose memory
      DisposeJob();

      // Create a mesh
      Mesh mesh = CubeGrid.CreateMesh(finalVertices, finalTriangles);
      mesh.name = gameObject.name;
      mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

      // Set vertices and triangles to the mesh
      if (finalVertices.Length > 0) {
        mesh.vertices = finalVertices;
        mesh.triangles = finalTriangles;
        mesh.RecalculateNormals();
      }

      // Set mesh to the mesh filter
      m_meshFilter.sharedMesh = mesh;

      timer.Stop();
      Debug.Log(
        string.Format(
          "Total to apply mesh: {0} ms", timer.ElapsedMilliseconds
        )
      );
      timer.Restart();

      // Check if it has a mesh collider
      MeshCollider collider = GetComponent<MeshCollider>();
      if (collider) {
        collider.sharedMesh = mesh;
      }

      timer.Stop();
      Debug.Log(
        string.Format(
          "Total to apply collider: {0} ms", timer.ElapsedMilliseconds
        )
      );
    }
  }

  public void FlagToRegenerate() {
    m_rebuildFlag = true;
  }

  private void OnValidate() {
    FlagToRegenerate();
  }

  private void RegenerateIfNeeded() {
    if (m_rebuildFlag || gameObject.name == "Chunk1First" && Input.GetKeyDown(KeyCode.Alpha1)) {
      ScheduleRegeneration();
      m_rebuildFlag = false;
    }
  }

  // private void OnValidate() {
  //   FlagToRegenerate();
  // }

  private void OnDrawGizmos() {
    RegenerateIfNeeded();

    if (!drawGizmos) return;

    // for (int z = 0; z < m_grid.resolution.z; z++) {
    //   for (int y = 0; y < m_grid.resolution.y; y++) {
    //     for (int x = 0; x < m_grid.resolution.x; x++) {
    //       Vector3 pointPosition = m_grid.GetPointPosition(x, y, z);
    //       Vector3 globalPointPosition = transform.TransformPoint(pointPosition);

    //       float value = m_grid.GetPoint(x, y, z).value;
    //       Gizmos.color = new Color(value, value, value);
    //       Gizmos.DrawCube(globalPointPosition, Vector3.one * gizmosSize);
    //     }
    //   }
    // }
  }
}
