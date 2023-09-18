using UnityEngine;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;

public enum TerrainChunkStatus {
  Spawned,
  Generating,
  Generated
}

[ExecuteInEditMode]
public class TerrainChunk : MonoBehaviour {
  public static DateTime lastUpdatedAt = DateTime.Now;
  private bool m_isAwake = false;

  public Vector3Int resolution {
    get {
      return m_resolution;
    }
    set {
      m_resolution = value;
      UpdateCachedFields();
    }
  }
  [SerializeField] private Vector3Int m_resolution = Vector3Int.one * 10;

  public Vector3 size {
    get {
      return m_size;
    }
    set {
      m_size = value;
      UpdateCachedFields();
    }
  }
  [SerializeField] private Vector3 m_size = Vector3.one * 10;
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

  public Bounds bounds { get { return m_bounds; } }
  private Bounds m_bounds;

  public float threshold = 0f;
  public bool useMiddlePoint = false;

  public bool drawGizmos = true;
  public float gizmosSize = 0.5f;

  #region Status
  public bool hasEverBeenGenerated { get { return m_hasEverBeenGenerated; } }
  private bool m_hasEverBeenGenerated;
  public TerrainChunkStatus status { get { return m_status; } }
  private TerrainChunkStatus m_status = TerrainChunkStatus.Spawned;
  private bool m_updateFlag;
  private bool m_destroyFlag;
  public event Action GenerationCompleted;
  #endregion

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

  public CubeGridPoint[] points { get { return m_points; } }
  private CubeGridPoint[] m_points;

  private void Awake() {
    m_isAwake = true;
    gameObject.layer = LayerMask.NameToLayer("Ground");

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

    UpdateCachedFields();
  }

  private void Start() {
    UpdateCachedFields();
    GenerateIfNeeded();
  }

  public void UpdateCachedFields() {
    m_gridSize = new Vector3Int(m_resolution.x + 1, m_resolution.y + 1, m_resolution.z + 1);
    m_inverseSize = new Vector3(1f / m_size.x, 1f / m_size.y, 1f / m_size.z);
    m_position = transform.position;
    m_noisePosition = m_position + noiseOffset;
    m_bounds = new Bounds(m_position + m_size / 2f, m_size);
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
    int pointCount = (resolution.x + 1) * (resolution.y + 1) * (resolution.z + 1);
    m_jobPoints = new NativeList<CubeGridPoint>(pointCount, Allocator.Persistent);

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

  public void ScheduleDestroy() {
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
    DisposeJob();
  }

  void LateUpdate() {
    bool shouldUpdate = !Application.isPlaying || DateTime.Now > lastUpdatedAt.AddSeconds(1d / 32);

    if (m_handle.HasValue && m_handle.Value.IsCompleted && shouldUpdate) {
      lastUpdatedAt = DateTime.Now;

      System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
      timer.Start();

      // Complete the job
      m_handle.Value.Complete();

      // Flags
      m_status = TerrainChunkStatus.Generated;
      m_hasEverBeenGenerated = true;

      if (!m_destroyFlag) {
        // Copy points
        m_points = m_jobPoints.ToArray();

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

        // Call events
        GenerationCompleted?.Invoke();
      }

      // Dispose memory
      DisposeJob();

      // Debug.Log("completed!");
    }
  }

  public void RequestUpdate() {
    m_updateFlag = true;
    m_status = TerrainChunkStatus.Generating;
    // Debug.Log("request update!");
  }

  public void GenerateOnEditor() {
    if (Application.isEditor && !Application.isPlaying) {
      RequestUpdate();
    }
  }

  private void OnValidate() {
    if (m_isAwake) {
      GenerateOnEditor();
      m_size = size;
      m_resolution = resolution;
    }
  }

  private void GenerateIfNeeded() {
    if (m_updateFlag && !m_destroyFlag) {
      ScheduleRegeneration();
      m_updateFlag = false;
      m_status = TerrainChunkStatus.Generating;
    }
  }

  private void OnDrawGizmos() {
    GenerateIfNeeded();

    if (!drawGizmos) return;

    Gizmos.color = Color.white;
    Gizmos.DrawWireCube(m_bounds.center, m_bounds.size);

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
