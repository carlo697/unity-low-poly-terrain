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
  public bool updateInEditor;
  public TerrainChunkManager terrainManager;

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

  public Vector3 scale {
    get {
      return m_scale;
    }
    set {
      m_scale = value;
      UpdateCachedFields();
    }
  }
  [SerializeField] private Vector3 m_scale = Vector3.one * 10;
  public float noiseSize = 1f;
  public Vector3 noiseOffset = Vector3.zero;
  public TerrainShape terrainShape;
  public bool debug;

  public Vector3Int gridSize { get { return m_gridSize; } }
  private Vector3Int m_gridSize;

  public Vector3 position { get { return m_position; } }
  private Vector3 m_position;

  public Vector3 noisePosition { get { return m_noisePosition; } }
  private Vector3 m_noisePosition;

  public Bounds bounds { get { return m_bounds; } }
  private Bounds m_bounds;

  public int levelOfDetail {
    get {
      int levelOfDetail = 1;
      if (terrainManager) {
        levelOfDetail = 1 + (int)Mathf.Log(
          m_scale.x / terrainManager.chunkScale.x,
          2
        );
      }

      return levelOfDetail;
    }
  }

  public float threshold = 0f;

  public bool drawGizmos = true;
  public float gizmosSize = 0.5f;

  #region Status
  public enum GenerationState {
    None,
    Terrain,
    Physics
  }
  private GenerationState m_generationState = GenerationState.None;
  public bool hasEverBeenGenerated { get { return m_hasEverBeenGenerated; } }
  private bool m_hasEverBeenGenerated;
  public TerrainChunkStatus status { get { return m_status; } }
  private TerrainChunkStatus m_status = TerrainChunkStatus.Spawned;
  private bool m_updateFlag;
  private bool m_destroyFlag;
  public event Action GenerationCompleted;
  #endregion

  #region Components
  public MeshFilter meshFilter { get { return m_meshFilter; } }
  private MeshFilter m_meshFilter;
  public MeshRenderer meshRenderer { get { return m_meshRenderer; } }
  private MeshRenderer m_meshRenderer;
  public MeshCollider meshCollider { get { return m_meshCollider; } }
  private MeshCollider m_meshCollider;
  public Mesh mesh { get { return m_mesh; } }
  private Mesh m_mesh;
  #endregion

  #region Terrain Job
  private GCHandle samplerHandle;
  private NativeList<Vector3> m_jobVertices;
  private NativeList<int> m_jobTriangles;
  private NativeList<Vector3> m_jobUVs;
  private NativeList<Color> m_jobColors;
  private NativeList<VoxelPoint> m_jobPoints;
  private JobHandle? m_terrainJobHandle;
  #endregion

  // #region Raw Mesh Data
  // public Vector3[] meshVertices { get { return m_meshVertices; } }
  // private Vector3[] m_meshVertices;
  // public int[] meshTriangles { get { return m_meshTriangles; } }
  // private int[] m_meshTriangles;
  // public Vector3[] meshUVs { get { return m_meshUVs; } }
  // private Vector3[] m_meshUVs;
  // public Color[] meshColors { get { return meshColors; } }
  // private Color[] m_meshColors;
  // #endregion

  #region Physics Job
  private NativeReference<int> m_meshId;
  private JobHandle? m_physicsJobHandle;
  #endregion

  // public CubeGridPoint[] points { get { return m_points; } }
  // private CubeGridPoint[] m_points;

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
    // Get mesh collider (it's optional)
    m_meshCollider = GetComponent<MeshCollider>();

    // Generate in editor
    if (Application.isEditor && updateInEditor) {
      RequestUpdate();
    }

    UpdateCachedFields();
    GenerateIfNeeded();
  }

  public void UpdateCachedFields() {
    m_gridSize = new Vector3Int(m_resolution.x + 1, m_resolution.y + 1, m_resolution.z + 1);
    m_position = transform.position;
    m_noisePosition = m_position + noiseOffset;
    m_bounds = new Bounds(m_position + m_scale / 2f, m_scale);
  }

  [ContextMenu("InstantRegenerate")]
  public void ScheduleRegeneration() {
    UpdateCachedFields();

    if (m_terrainJobHandle.HasValue) {
      if (debug) {
        Debug.Log("There was already a terrain job in progress");
      }

      CancelTerrainJob();
    } else if (m_physicsJobHandle.HasValue) {
      if (debug) {
        Debug.Log("There was already a physics job in progress");
      }

      CancelPhysicsJob();
    }

    // Create the delegates for sampling the noise
    TerrainSamplerFunc samplerFunc;
    if (terrainShape != null) {
      FastNoiseChunk chunk = new FastNoiseChunk(this);
      samplerFunc = terrainShape.GetSampler(chunk);
    } else {
      throw new Exception("No sampler found");
    }

    // Store a reference to the sampler function
    samplerHandle = GCHandle.Alloc(samplerFunc);

    // Create the lists for the job
    m_jobVertices = new NativeList<Vector3>(Allocator.Persistent);
    m_jobTriangles = new NativeList<int>(Allocator.Persistent);
    m_jobUVs = new NativeList<Vector3>(Allocator.Persistent);
    m_jobColors = new NativeList<Color>(Allocator.Persistent);
    int pointCount = (resolution.x + 1) * (resolution.y + 1) * (resolution.z + 1);
    m_jobPoints = new NativeList<VoxelPoint>(pointCount, Allocator.Persistent);

    // Create job
    TerrainMarchingCubesJob job = new TerrainMarchingCubesJob {
      vertices = m_jobVertices,
      triangles = m_jobTriangles,
      uvs = m_jobUVs,
      colors = m_jobColors,
      points = m_jobPoints,
      samplerHandle = samplerHandle,
      scale = scale,
      resolution = resolution,
      threshold = threshold,
      debug = debug
    };
    m_terrainJobHandle = job.Schedule();
  }

  void Update() {
    if (m_destroyFlag && !m_terrainJobHandle.HasValue && !m_physicsJobHandle.HasValue) {
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

    if (m_terrainJobHandle.HasValue) {
      Debug.Log("Chunk destroyed and there was a terrain job running");
      CancelTerrainJob();
    } else if (m_physicsJobHandle.HasValue) {
      Debug.Log("Chunk destroyed and there was a physics job running");
      CancelPhysicsJob();
    }
  }

  private void DisposeTerrainJob() {
    m_jobVertices.Dispose();
    m_jobTriangles.Dispose();
    m_jobUVs.Dispose();
    m_jobColors.Dispose();
    m_jobPoints.Dispose();
    samplerHandle.Free();
    m_terrainJobHandle = null;
  }

  private void CancelTerrainJob() {
    m_terrainJobHandle.Value.Complete();
    DisposeTerrainJob();
  }

  private void DisposePhysicsJob() {
    m_meshId.Dispose();
    m_physicsJobHandle = null;
  }

  private void CancelPhysicsJob() {
    m_physicsJobHandle.Value.Complete();
    DisposePhysicsJob();
  }

  void LateUpdate() {
    if (m_generationState == GenerationState.Terrain) {
      bool shouldUpdate = !Application.isPlaying || DateTime.Now > lastUpdatedAt.AddSeconds(1d / 32);

      if (m_terrainJobHandle.HasValue && m_terrainJobHandle.Value.IsCompleted && shouldUpdate) {
        lastUpdatedAt = DateTime.Now;

        System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        // Complete the job
        m_terrainJobHandle.Value.Complete();

        if (!m_destroyFlag) {
          // Copy points and mesh data
          // m_points = m_jobPoints.ToArray();
          // m_meshVertices = m_jobVertices.ToArray();
          // m_meshTriangles = m_jobTriangles.ToArray();
          // m_meshUVs = m_jobUVs.ToArray();
          // m_meshColors = m_jobColors.ToArray();

          // Create a mesh
          m_mesh = MarchingCubes.CreateMesh(
            m_jobVertices,
            m_jobTriangles,
            m_jobUVs,
            m_jobColors,
            meshFilter.sharedMesh
          );
          m_mesh.name = gameObject.name;

          // Set mesh
          m_meshFilter.sharedMesh = m_mesh;

          timer.Stop();
          if (debug) {
            Debug.Log($"Total to apply mesh: {timer.ElapsedMilliseconds} ms");
          }

          // If the object has a collider, start baking the mesh, otherwise,
          // finish the generation process
          if (m_meshCollider) {
            // Store the id of the mesh in a native reference
            m_meshId = new NativeReference<int>(Allocator.TempJob);
            m_meshId.Value = mesh.GetInstanceID();

            // Schedule the job
            BakeSingleMeshJob job = new BakeSingleMeshJob(m_meshId);
            m_physicsJobHandle = job.Schedule();

            m_generationState = GenerationState.Physics;
          } else {
            // Set status and call events
            FinishGeneration();
          }
        }

        // Dispose memory
        DisposeTerrainJob();
      }
    } else if (m_generationState == GenerationState.Physics) {
      if (m_physicsJobHandle.HasValue && m_physicsJobHandle.Value.IsCompleted) {
        // Complete the job and dispose memory
        m_physicsJobHandle.Value.Complete();
        DisposePhysicsJob();

        // Assign new mesh to the mesh collider
        m_meshCollider.sharedMesh = m_mesh;

        // Set status and call events
        FinishGeneration();
      }
    }
  }

  private void FinishGeneration() {
    // Flags
    m_status = TerrainChunkStatus.Generated;
    m_generationState = GenerationState.None;
    m_hasEverBeenGenerated = true;

    // Call events
    GenerationCompleted?.Invoke();

    // Debug.Log("completed!");
  }

  public void RequestUpdate() {
    m_updateFlag = true;
    m_status = TerrainChunkStatus.Generating;
    m_generationState = GenerationState.Terrain;
    // Debug.Log("request update!");
  }

  public void GenerateOnEditor() {
    if (updateInEditor && Application.isEditor && !Application.isPlaying) {
      RequestUpdate();
    }
  }

  private void OnValidate() {
    if (m_isAwake) {
      GenerateOnEditor();
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
