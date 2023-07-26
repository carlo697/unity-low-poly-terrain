using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;

[ExecuteInEditMode]
public class TerrainChunk : MonoBehaviour {
  public static DateTime lastUpdatedAt = DateTime.Now;

  public Vector3Int resolution = Vector3Int.one * 10;
  public Vector3 size = Vector3.one * 10;
  public float noiseSize = 1f;
  public Vector3 noiseOffset = Vector3.zero;
  public TerrainShape terrainShape;
  public bool debug;

  public Vector3Int gridSize { get { return m_gridSize; } }
  private Vector3Int m_gridSize;

  public Vector3 inverseSize { get { return m_inverseSize; } }
  private Vector3 m_inverseSize;

  public Vector3 position { get { return m_position; } }
  private Vector3 m_position;

  public Vector3 noisePosition { get { return m_noisePosition; } }
  private Vector3 m_noisePosition;

  public float threshold = 0f;
  public bool useMiddlePoint = false;

  public bool drawGizmos = true;
  public float gizmosSize = 0.5f;

  public bool isJobInProgress { get { return m_handle.HasValue; } }
  public bool isGenerating { get; private set; } = false;
  public bool hasEverBeenGenerated { get; private set; } = false;

  private bool m_generateFlag;
  private bool m_destroyFlag;

  public MeshFilter meshFilter { get { return m_meshFilter; } }
  private MeshFilter m_meshFilter;
  public MeshRenderer meshRenderer { get { return m_meshRenderer; } }
  private MeshRenderer m_meshRenderer;

  private GCHandle samplerHandle;
  private GCHandle postProcessingHandle;
  private NativeList<Vector3> m_jobVertices;
  private NativeList<int> m_jobTriangles;
  private NativeList<Color> m_jobColors;
  private NativeList<CubeGridPoint> m_jobPoints;
  JobHandle? m_handle;

  private CubeGridPoint[] m_points;

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
    UpdateCachedFields();
    GenerateIfNeeded();
  }

  public void UpdateCachedFields() {
    m_gridSize = new Vector3Int(resolution.x + 1, resolution.y + 1, resolution.z + 1);
    m_inverseSize = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);
    m_position = transform.position;
    m_noisePosition = position + noiseOffset;
  }

  [ContextMenu("InstantRegenerate")]
  public void ScheduleRegeneration() {
    UpdateCachedFields();

    if (m_handle.HasValue) {
      if (debug)
        Debug.Log("There was already a handle running");
      CancelJob();
    }

    // Create the delegates for sampling the noise
    CubeGridSamplerFunc samplerFunc;
    CubeGridPostProcessingFunc postProcessingFunc;
    if (terrainShape != null) {
      terrainShape.GetSampler(this, out samplerFunc, out postProcessingFunc);
    } else {
      throw new Exception("No sampler found");
    }

    // Store a reference to the sampler function
    samplerHandle = GCHandle.Alloc(samplerFunc);
    postProcessingHandle = GCHandle.Alloc(postProcessingFunc);

    // Create the lists for the job
    m_jobVertices = new NativeList<Vector3>(Allocator.Persistent);
    m_jobTriangles = new NativeList<int>(Allocator.Persistent);
    m_jobColors = new NativeList<Color>(Allocator.Persistent);
    m_jobPoints = new NativeList<CubeGridPoint>(Allocator.Persistent);

    // Create job
    CubeGridJob job = new CubeGridJob(
      m_jobVertices,
      m_jobTriangles,
      m_jobColors,
      m_jobPoints,
      size,
      resolution,
      samplerHandle,
      postProcessingHandle,
      threshold,
      useMiddlePoint,
      debug
    );
    this.m_handle = job.Schedule();
  }

  void Update() {
    if (m_destroyFlag && !m_handle.HasValue) {
      Destroy(gameObject);
    } else {
      GenerateIfNeeded();
    }
  }

  public void DestroyOnNextFrame() {
    m_destroyFlag = true;
  }

  private void OnDestroy() {
    if (!Application.isEditor) {
      Destroy(m_meshFilter.sharedMesh);
    }

    if (m_handle.HasValue) {
      Debug.Log("Chunk destroyed and there was a job running");
      CancelJob();
    }
  }

  void DisposeJob() {
    m_jobVertices.Dispose();
    m_jobTriangles.Dispose();
    m_jobColors.Dispose();
    m_jobPoints.Dispose();
    samplerHandle.Free();
    postProcessingHandle.Free();
    m_handle = null;
  }

  void CancelJob() {
    m_handle.Value.Complete();
    m_jobVertices.Dispose();
    m_jobTriangles.Dispose();
    m_jobColors.Dispose();
    m_jobPoints.Dispose();
    samplerHandle.Free();
    postProcessingHandle.Free();
    m_handle = null;
  }

  void LateUpdate() {
    bool shouldUpdate = !Application.isPlaying || DateTime.Now > lastUpdatedAt.AddSeconds(1d / 32);

    if (m_handle.HasValue && m_handle.Value.IsCompleted && shouldUpdate) {
      lastUpdatedAt = DateTime.Now;

      System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
      timer.Start();

      // Complete the job
      m_handle.Value.Complete();

      // Copy points
      m_points = m_jobPoints.ToArray();

      // Flags
      isGenerating = false;
      hasEverBeenGenerated = true;

      if (!m_destroyFlag) {
        // Create a mesh
        Mesh mesh = CubeGrid.CreateMesh(
          m_jobVertices,
          m_jobTriangles,
          m_jobColors,
          debug,
          meshFilter.sharedMesh
        );
        mesh.name = gameObject.name;

        // Set mesh
        m_meshFilter.sharedMesh = mesh;

        timer.Stop();
        if (debug)
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
        if (debug)
          Debug.Log(
            string.Format(
              "Total to apply collider: {0} ms", timer.ElapsedMilliseconds
            )
          );
      }

      // Dispose memory
      DisposeJob();
    }
  }

  public void GenerateOnNextFrame() {
    m_generateFlag = true;
    isGenerating = true;
  }

  public void GenerateOnEditor() {
    if (Application.isEditor && !Application.isPlaying) {
      GenerateOnNextFrame();
    }
  }

  private void OnValidate() {
    GenerateOnEditor();
  }

  private void GenerateIfNeeded() {
    if (m_generateFlag && !m_destroyFlag) {
      ScheduleRegeneration();
      m_generateFlag = false;
    }
  }

  private void OnDrawGizmos() {
    GenerateIfNeeded();

    if (!drawGizmos) return;

    Gizmos.color = Color.white;
    Gizmos.DrawWireCube(
      transform.position + size / 2f,
      size
    );

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

  public static Vector3 RandomPointInBounds(Bounds bounds) {
    return new Vector3(
      UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
      bounds.max.y,
      UnityEngine.Random.Range(bounds.min.z, bounds.max.z)
    );
  }

  [ContextMenu("Test Raycasts")]
  public void TestRaycast() {
    // Check if it has a mesh collider
    MeshCollider collider = GetComponent<MeshCollider>();

    if (collider) {
      System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
      timer.Start();

      Bounds bounds = collider.bounds;
      RaycastHit[] hits = new RaycastHit[10];
      float distance = collider.bounds.size.y;
      int totalHits = 0;

      if (collider) {
        for (int i = 0; i < 10000; i++) {
          Ray ray = new Ray(RandomPointInBounds(bounds), Vector3.down);
          int hitCount = Physics.RaycastNonAlloc(ray, hits, distance);

          for (int j = 0; j < hitCount; j++) {
            RaycastHit hit = hits[j];
            totalHits++;
          }
        }
      }

      timer.Stop();

      if (debug) {
        Debug.LogFormat("{0} ms", timer.ElapsedMilliseconds);
        Debug.LogFormat("Total hits: {0}", totalHits);
      }
    }
  }

  [ContextMenu("Test Looping Points")]
  public void TestLoopingPoints() {
    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    float total = 0;

    for (int i = 0; i < 20; i++) {
      for (int j = 0; j < m_points.Length; j++) {
        CubeGridPoint point = m_points[j];
        total += point.value;
      }
    }

    timer.Stop();

    if (debug) {
      Debug.LogFormat("{0} ms", timer.ElapsedMilliseconds);
      Debug.LogFormat("Total value: {0}", total);
    }
  }
}
