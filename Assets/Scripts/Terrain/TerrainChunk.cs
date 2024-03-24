using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
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
      // Calculate a level of detail integer between 1 and the maximun level
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
  public int upsamplingLevel = 0;

  public bool drawGizmos = true;
  public GizmosMode drawGizmosMode;
  public enum GizmosMode {
    WiredCube,
    CubesDensity,
    CubesSurface
  };


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
  private JobHandle? m_terrainJobHandle;
  private NativeList<Vector3> m_jobVertices;
  private NativeList<int> m_jobTriangles;
  private NativeList<Vector3> m_jobUVs;
  private NativeList<Color> m_jobColors;
  private NativeList<VoxelPoint> m_jobPoints;
  private NativeList<uint> m_jobTriangleMaterials;
  private GCHandle m_jobManagedDataHandle;
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
  public uint[] triangleMaterials { get { return m_triangleMaterials; } }
  private uint[] m_triangleMaterials;
  // #endregion

  public Dictionary<Biome, float[]> biomeMasks { get { return m_biomeMasks; } }
  private Dictionary<Biome, float[]> m_biomeMasks = null;

  public Dictionary<int, float[]> biomeMasksById { get { return m_biomeMasksById; } }
  private Dictionary<int, float[]> m_biomeMasksById = null;

  public HashSet<int> biomeIds { get { return m_biomeIds; } }
  private HashSet<int> m_biomeIds = null;

  #region Physics Job
  private JobHandle? m_physicsJobHandle;
  #endregion

  public VoxelGrid grid { get { return m_grid; } }
  private VoxelGrid m_grid;

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

  [ContextMenu("Print Cached Fields")]
  public void PrintCachedFields() {
    UpdateCachedFields();
    Debug.LogFormat("Grid size: {0}", m_gridSize);
    Debug.LogFormat("Position: {0}", m_position);
    Debug.LogFormat("Noise position: {0}", m_noisePosition);
    Debug.LogFormat("Bounds: {0}", m_bounds);
  }

  private void OnDrawGizmosSelected() {
    Gizmos.DrawWireCube(m_bounds.center, m_bounds.size);
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
      samplerFunc = terrainShape.GetSampler();
    } else {
      throw new Exception("No sampler found");
    }

    // Create the managed data for the job
    TerrainMarchingCubesJob.ManagedData managedData = new() {
      samplerFunc = samplerFunc,
    };

    // Store a reference to the sampler function
    m_jobManagedDataHandle = GCHandle.Alloc(managedData);

    // Create the lists for the job
    m_jobVertices = new NativeList<Vector3>(Allocator.Persistent);
    m_jobTriangles = new NativeList<int>(Allocator.Persistent);
    m_jobUVs = new NativeList<Vector3>(Allocator.Persistent);
    m_jobColors = new NativeList<Color>(Allocator.Persistent);
    m_jobTriangleMaterials = new NativeList<uint>(Allocator.Persistent);
    int pointCount = (resolution.x + 1) * (resolution.y + 1) * (resolution.z + 1);
    m_jobPoints = new NativeList<VoxelPoint>(pointCount, Allocator.Persistent);

    // Create job
    TerrainMarchingCubesJob job = new TerrainMarchingCubesJob {
      vertices = m_jobVertices,
      triangles = m_jobTriangles,
      uvs = m_jobUVs,
      colors = m_jobColors,
      triangleMaterials = m_jobTriangleMaterials,
      points = m_jobPoints,
      resolution = resolution,
      position = m_noisePosition,
      scale = scale,
      noiseScale = noiseSize,
      threshold = threshold,
      upsamplingLevel = upsamplingLevel,
      debug = debug,
      managedDataHandle = m_jobManagedDataHandle
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
    if (m_grid != null) {
      m_grid.Dispose();
    }

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
    m_terrainJobHandle = null;
    m_jobVertices.Dispose();
    m_jobTriangles.Dispose();
    m_jobUVs.Dispose();
    m_jobColors.Dispose();
    m_jobPoints.Dispose();
    m_jobTriangleMaterials.Dispose();
    m_jobManagedDataHandle.Free();
  }

  private void CancelTerrainJob() {
    m_terrainJobHandle.Value.Complete();
    DisposeTerrainJob();
  }

  private void DisposePhysicsJob() {
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
          // Copy points and build grid
          // if (m_grid == null) {
          //   m_grid = new VoxelGrid(scale, resolution, threshold);
          // }
          // m_grid.CopyPointsFrom(m_jobPoints);

          // Copy mesh data
          // m_meshVertices = m_jobVertices.ToArray();
          // m_meshTriangles = m_jobTriangles.ToArray();
          // m_meshUVs = m_jobUVs.ToArray();
          // m_meshColors = m_jobColors.ToArray();
          m_triangleMaterials = m_jobTriangleMaterials.ToArray();

          // Get info from the managed data
          var managedData = (TerrainMarchingCubesJob.ManagedData)m_jobManagedDataHandle.Target;
          m_biomeMasks = managedData.biomeMasks;

          m_biomeMasksById = new();
          foreach (var (biome, mask) in m_biomeMasks) {
            m_biomeMasksById.Add(biome.id, mask);
          }

          m_biomeIds = new HashSet<int>(m_biomeMasks.Count);
          foreach (var (biome, mask) in m_biomeMasks) {
            m_biomeIds.Add(biome.id);
          }

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
            Debug.Log($"Total to apply mesh: {timer.Elapsed.TotalMilliseconds} ms");
          }

          // If the object has a collider, start baking the mesh, otherwise,
          // finish the generation process
          if (m_meshCollider) {
            // Schedule the job
            BakeSingleMeshJob job = new BakeSingleMeshJob(mesh.GetInstanceID());
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

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public T GetValue2dAtNormalized2d<T>(T[] values, float x, float y) {
    int index = TextureUtils.GetIndexFrom2d(
      Mathf.FloorToInt(x * gridSize.x),
      Mathf.FloorToInt(y * gridSize.z),
      resolution.x
    );

    return values[index];
  }

  private void OnDrawGizmos() {
    GenerateIfNeeded();

    if (!drawGizmos) return;

    if (drawGizmosMode == GizmosMode.WiredCube) {
      Gizmos.color = Color.white;
      Gizmos.DrawWireCube(m_bounds.center, m_bounds.size);
    } else {
      Vector3 voxelSize = new Vector3(
        (1f / ((float)resolution.x)) * scale.x,
        (1f / ((float)resolution.y)) * scale.y,
        (1f / ((float)resolution.z)) * scale.z
      );
      Vector3 voxelExtents = voxelSize / 2f;

      for (int z = 0; z < resolution.z; z++) {
        for (int y = 0; y < resolution.y; y++) {
          for (int x = 0; x < resolution.x; x++) {
            Vector3 pointPosition = new Vector3(
              ((float)x / ((float)resolution.x)) * scale.x,
              ((float)y / ((float)resolution.y)) * scale.y,
              ((float)z / ((float)resolution.z)) * scale.z
            );
            Vector3 globalPointPosition = transform.TransformPoint(pointPosition + voxelExtents);

            Color color;
            if (m_grid == null) {
              color = Color.black;
            } else if (drawGizmosMode == GizmosMode.CubesDensity) {
              float value = m_grid != null ? m_grid.GetPoint(x, y, z).value : 1f;
              color = new Color(value, value, value);
            } else if (drawGizmosMode == GizmosMode.CubesSurface) {
              // Find the case index
              Vector3Int coords = new Vector3Int(x, y, z);
              int caseIndex = MarchingCubes.FindCaseIndex(grid, threshold, coords);

              if (caseIndex == 0 || caseIndex == 0xFF) {
                continue;
              } else {
                color = Color.white;
              }
            } else {
              color = Color.black;
            }

            Gizmos.color = color;
            Gizmos.DrawCube(globalPointPosition, voxelSize);
          }
        }
      }
    }
  }
}
