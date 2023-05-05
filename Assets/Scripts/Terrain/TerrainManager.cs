using UnityEngine;
using System.Collections.Generic;

public class TerrainManager : MonoBehaviour {
  public class ChunkData {
    public Vector3 worldPosition;
    public Vector3 coords;
    public TerrainChunk component;
    public GameObject gameObject;

    public ChunkData(Vector3 worldPosition, Vector3 coords, TerrainChunk component) {
      this.worldPosition = worldPosition;
      this.coords = coords;
      this.component = component;
      this.gameObject = component.gameObject;
    }
  }

  public float viewDistance = 100f;
  public Vector3 chunkSize = new Vector3(32f, 64f, 32f);
  public Vector3Int chunkResolution = new Vector3Int(32, 64, 32);
  public Material chunkMaterial;

  public float noiseSize = 32f;
  public int noiseOctaves = 3;

  private List<ChunkData> m_chunks = new List<ChunkData>();
  private Dictionary<Vector3, ChunkData> m_chunkDictionary =
    new Dictionary<Vector3, ChunkData>();

  private List<Vector3> m_visibleChunkPositions = new List<Vector3>();
  private HashSet<Vector3> m_visibleChunkPositionsHashSet =
    new HashSet<Vector3>();

  public float m_updatePeriod = 0.1f;
  private float m_timeSinceLastUpdate = 0.0f;

  private void CreateChunk(Vector3 worldPosition) {
    // Create empty GameObject
    GameObject gameObject = new GameObject(string.Format(
      "{0}, {1}", worldPosition.x, worldPosition.z
    ));

    // Set position and parent
    gameObject.transform.position = worldPosition;
    gameObject.transform.localScale = chunkSize;
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
    chunk.resolution = chunkResolution;
    chunk.noiseSize = noiseSize;
    chunk.noiseOctaves = noiseOctaves;
    chunk.noiseOffset = coords;
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
            position.x - chunkSize.x / 2f,
            position.y - chunkSize.y / 2f,
            position.z - chunkSize.z / 2f
          ),
          chunkSize
        );

        // Check if a sphere of radius 'distance' is touching the chunk
        float distanceToChunk = Mathf.Sqrt(bounds.SqrDistance(worldPosition));
        if (distanceToChunk < viewDistance) {
          m_visibleChunkPositions.Add(position);
          m_visibleChunkPositionsHashSet.Add(position);
        }
      }
    }
  }

  private void Update() {
    m_timeSinceLastUpdate += Time.deltaTime;

    if (m_timeSinceLastUpdate < m_updatePeriod) {
      return;
    }

    m_timeSinceLastUpdate = 0f;

    Camera camera = Camera.main;
    if (camera) {
      Vector3 cameraPosition = camera.transform.position;

      UpdateVisibleChunkPositions(cameraPosition);

      // Check if the chunks are already there
      foreach (Vector3 position in m_visibleChunkPositions) {
        bool foundChunk = m_chunkDictionary.ContainsKey(position);

        if (!foundChunk) {
          CreateChunk(position);
        }
      }

      // Delete chunks that are out of view
      foreach (ChunkData chunk in m_chunks.ToArray()) {
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
    }
  }
}
