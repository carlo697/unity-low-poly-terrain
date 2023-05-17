using UnityEngine;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections;

public class TerrainManager : MonoBehaviour {
  struct DistanceToCameraComparer : IComparer<Vector3> {
    public Vector3 cameraPosition;

    public DistanceToCameraComparer(Vector3 cameraPosition) {
      this.cameraPosition = cameraPosition;
    }

    public int Compare(Vector3 a, Vector3 b) {
      float distanceA =
      (a.x - cameraPosition.x) * (a.x - cameraPosition.x)
      + (a.z - cameraPosition.z) * (a.z - cameraPosition.z);

      float distanceB =
        (b.x - cameraPosition.x) * (b.x - cameraPosition.x)
        + (b.z - cameraPosition.z) * (b.z - cameraPosition.z);

      return distanceA.CompareTo(distanceB);
    }
  }

  public struct ChunkPositionsSortingJob : IJob {
    public Vector3 cameraPosition;
    public NativeList<Vector3> chunks;

    public void Execute() {
      chunks.Sort(new DistanceToCameraComparer(cameraPosition));
    }
  }

  public class ChunkData {
    public Vector3 worldPosition;
    public Vector3 coords;
    public TerrainChunk component;
    public GameObject gameObject;
    public bool needsUpdate = true;
    public float resolution = 1f;

    public ChunkData(
      Vector3 worldPosition,
      Vector3 coords,
      TerrainChunk component
    ) {
      this.worldPosition = worldPosition;
      this.coords = coords;
      this.component = component;
      this.gameObject = component.gameObject;
    }
  }

  public float viewDistance = 100f;
  public Vector3 chunkSize = new Vector3(32f, 64f, 32f);
  public Vector3Int chunkResolution = new Vector3Int(32, 64, 32);
  public float waterLevel = 40f;
  public Material chunkMaterial;
  public bool debug;

  public int noiseOctaves = 3;
  public float lodDistance = 75f;

  private List<ChunkData> m_chunks = new List<ChunkData>();
  private Dictionary<Vector3, ChunkData> m_chunkDictionary =
    new Dictionary<Vector3, ChunkData>();

  private List<Vector3> m_visibleChunkPositions = new List<Vector3>();
  private HashSet<Vector3> m_visibleChunkPositionsHashSet =
    new HashSet<Vector3>();

  public float updatePeriod = 0.3f;
  private float m_updateTimer = 0.0f;
  public float generatePeriod = 0.02f;
  private float m_generateTimer = 0.0f;
  public int maxNumberOfChunksToGenerate = 15;
  public bool multithreadedSorting = true;

  private bool m_isSortingReady = true;
  private JobHandle? m_sortingHandle;
  private NativeList<Vector3> m_sortingJobChunks;
  private Vector3 m_lastCameraPosition;

  [SerializeField] private TerrainNoise m_terrainNoise;

  private void Awake() {
    if (!m_terrainNoise) {
      m_terrainNoise = GetComponent<TerrainNoise>();
    }
  }

  private void CreateChunk(Vector3 worldPosition) {
    // Create empty GameObject
    GameObject gameObject = new GameObject(string.Format(
      "{0}, {1}", worldPosition.x, worldPosition.z
    ));

    // Set position and parent
    gameObject.transform.position = new Vector3(
      worldPosition.x,
      -waterLevel,
      worldPosition.z
    );
    gameObject.transform.SetParent(transform);

    Vector3 coords = GetChunkCoordsAt(worldPosition);

    // Create chunk component
    TerrainChunk chunk = gameObject.AddComponent<TerrainChunk>();

    // Add to the list
    ChunkData data = new ChunkData(worldPosition, coords, chunk);
    m_chunks.Add(data);
    m_chunkDictionary.Add(worldPosition, data);

    // Add mesh collider
    gameObject.AddComponent<MeshCollider>();

    // Set variables
    chunk.debug = debug;
    chunk.samplerFactory = m_terrainNoise;
    chunk.size = chunkSize;
    chunk.resolution = chunkResolution;
    chunk.GetComponent<MeshRenderer>().sharedMaterial = chunkMaterial;
  }

  private Vector3 GetChunkCoordsAt(Vector3 worldPosition) {
    return new Vector3(
      Mathf.Floor(worldPosition.x / chunkSize.x),
      0f,
      Mathf.Floor(worldPosition.z / chunkSize.z)
    );
  }

  private Vector3 GetChunkPositionFromCoords(Vector3 chunkCoords) {
    return new Vector3(
     chunkCoords.x * chunkSize.x,
     0f,
     chunkCoords.z * chunkSize.z
   );
  }

  private Vector3 GetChunkPositionAt(Vector3 worldPosition) {
    Vector3 chunkCoords = GetChunkCoordsAt(worldPosition);
    return GetChunkPositionFromCoords(chunkCoords);
  }

  private Vector3 FlatY(Vector3 worldPosition) {
    return new Vector3(
      worldPosition.x,
      0f,
      worldPosition.z
    );
  }

  private void UpdateVisibleChunkPositions(Vector3 worldPosition) {
    // Get the chunk the player is standing right now
    Vector3 mainChunkCoords = GetChunkCoordsAt(worldPosition);
    Vector3 mainChunkPosition = GetChunkPositionFromCoords(mainChunkCoords);

    int visibleX = Mathf.CeilToInt(viewDistance / chunkSize.x);
    int visibleZ = Mathf.CeilToInt(viewDistance / chunkSize.z);

    // Build a list of the coords of the visible chunks
    m_visibleChunkPositions.Clear();
    m_visibleChunkPositionsHashSet.Clear();
    for (
      int z = (int)mainChunkCoords.z - visibleZ;
      z < mainChunkCoords.z + visibleZ;
      z++) {
      for (
        int x = (int)mainChunkCoords.x - visibleX;
        x < mainChunkCoords.x + visibleX;
        x++
      ) {
        Vector3 coords = new Vector3(x, 0, z);
        Vector3 position = GetChunkPositionFromCoords(coords);

        //Create a bounds that encloses the chunk
        Bounds bounds = new Bounds(
          new Vector3(
            position.x + chunkSize.x / 2f,
            position.y + chunkSize.y / 2f,
            position.z + chunkSize.z / 2f
          ),
          chunkSize
        );

        // Check if a sphere of radius 'distance' is touching the chunk
        float distanceToChunk = Mathf.Sqrt(bounds.SqrDistance(worldPosition));
        if (distanceToChunk <= viewDistance) {
          m_visibleChunkPositions.Add(position);
          m_visibleChunkPositionsHashSet.Add(position);
        }
      }
    }

    // Sort the array by measuring the distance from the chunk to the camera
    m_lastCameraPosition = worldPosition;
    m_isSortingReady = false;
    if (multithreadedSorting) {
      m_sortingJobChunks = m_visibleChunkPositions.ToNativeList(Allocator.TempJob);
      ChunkPositionsSortingJob job = new ChunkPositionsSortingJob {
        cameraPosition = worldPosition,
        chunks = m_sortingJobChunks
      };
      m_sortingHandle = job.Schedule();
    } else {
      m_visibleChunkPositions.Sort(new DistanceToCameraComparer(worldPosition));
      m_isSortingReady = true;
    }
  }

  private void UpdateFollowingVisibleChunks() {
    // Check if the chunks are already there
    foreach (Vector3 position in m_visibleChunkPositions) {
      bool foundChunk = m_chunkDictionary.ContainsKey(position);

      if (!foundChunk) {
        CreateChunk(position);
      }
    }

    // Delete chunks that are out of view
    for (int i = m_chunks.Count - 1; i >= 0; i--) {
      ChunkData chunk = m_chunks[i];
      Vector3 chunkPosition = chunk.worldPosition;
      // Find a chunk with the same position
      bool foundPosition = m_visibleChunkPositionsHashSet.Contains(
        chunkPosition
      );

      if (!foundPosition) {
        GameObject.Destroy(chunk.gameObject);
        m_chunks.Remove(chunk);
        m_chunkDictionary.Remove(chunk.worldPosition);
      }
    }

    // Set the resolutions of the chunks based on distance
    foreach (ChunkData chunk in m_chunks) {
      // Get the distance to the camera
      float distanceToCamera = Vector3.Distance(m_lastCameraPosition, chunk.worldPosition);

      // Calculate the target resolution
      float newResolution;
      if (distanceToCamera < lodDistance * 1f) {
        newResolution = 1f;
      } else if (distanceToCamera < lodDistance * 2f) {
        newResolution = 0.5f;
      } else if (distanceToCamera < lodDistance * 4f) {
        newResolution = 0.25f;
      } else if (distanceToCamera < lodDistance * 8f) {
        newResolution = 0.125f;
      } else {
        newResolution = 0.0625f;
      }

      if (chunk.resolution != newResolution) {
        // Update the resolution and noise size
        chunk.resolution = newResolution;
        chunk.component.resolution = new Vector3Int(
          Mathf.Max(Mathf.RoundToInt((float)chunkResolution.x * newResolution), 2),
          Mathf.Max(Mathf.RoundToInt((float)chunkResolution.y * newResolution), 2),
          Mathf.Max(Mathf.RoundToInt((float)chunkResolution.z * newResolution), 2)
        );
        // Request an update
        chunk.needsUpdate = true;
      }
    }
  }

  private void RequestChunksGeneration() {
    // Group the number of chunks being generated
    Dictionary<float, int> resolutionGroups = new Dictionary<float, int>();
    int totalInProgress = 0;
    for (int index = 0; index < m_chunks.Count; index++) {
      ChunkData chunk = m_chunks[index];

      if (chunk.component.isGenerating) {
        // totalInProgress++;
        totalInProgress += Mathf.RoundToInt((chunk.resolution / 0.0625f));

        if (resolutionGroups.ContainsKey(chunk.resolution)) {
          resolutionGroups[chunk.resolution]++;
        } else {
          resolutionGroups[chunk.resolution] = 1;
        }
      } else if (!resolutionGroups.ContainsKey(chunk.resolution)) {
        resolutionGroups[chunk.resolution] = 0;
      }
    }

    // Tell chunks to generate their meshes
    // Check if the chunks are already there
    for (int index = 0; index < m_visibleChunkPositions.Count; index++) {
      Vector3 position = m_visibleChunkPositions[index];

      if (m_chunkDictionary.ContainsKey(position)) {
        ChunkData chunk = m_chunkDictionary[position];

        // Stop generating if we are out of budget
        if (totalInProgress >= maxNumberOfChunksToGenerate) {
          break;
        }

        // Tell the chunk to start generating if the budget is available
        if (
          chunk.needsUpdate
          && resolutionGroups[chunk.resolution] < (1f / chunk.resolution)
        ) {
          chunk.component.GenerateOnNextFrame();
          chunk.needsUpdate = false;

          // Update the groups
          resolutionGroups[chunk.resolution]++;
          totalInProgress += Mathf.RoundToInt((chunk.resolution / 0.0625f));
        }
      }
    }
  }

  private void Update() {
    if (m_isSortingReady) {
      m_generateTimer += Time.deltaTime;
      if (m_generateTimer > generatePeriod) {
        m_generateTimer = 0f;
        RequestChunksGeneration();
      }
    }

    // Handle the job used to sort the chunks
    if (m_sortingHandle != null && m_sortingHandle.Value.IsCompleted) {
      // Complete the job
      m_sortingHandle.Value.Complete();

      // Get the results
      this.m_visibleChunkPositions = new List<Vector3>(m_sortingJobChunks.ToArray());

      // Dispose memory
      m_sortingJobChunks.Dispose();
      m_sortingHandle = null;
      m_isSortingReady = true;

      UpdateFollowingVisibleChunks();
      return;
    }

    Camera camera = Camera.main;
    if (camera && m_isSortingReady) {
      m_updateTimer += Time.deltaTime;
      if (m_updateTimer > updatePeriod) {
        m_updateTimer = 0f;

        Vector3 cameraPosition = FlatY(camera.transform.position);

        UpdateVisibleChunkPositions(cameraPosition);

        if (!multithreadedSorting) {
          UpdateFollowingVisibleChunks();
        }
      }
    }
  }
}
