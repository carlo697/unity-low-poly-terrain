using UnityEngine;
using System.Collections.Generic;

public class GrassManager : MonoBehaviour {
  public TerrainChunkManager terrainManager { get { return m_terrainManager; } }
  [SerializeField] private TerrainChunkManager m_terrainManager;

  public float maxDistance = 100f;

  [Header("Debug")]
  public bool logGenerationInfo;
  public bool logInstancingInfo;
  public bool logCountOfRenderedInstances;
  public bool skipRendering;

  public Dictionary<int, Grass> grassesById { get { return m_grassesById; } }
  private Dictionary<int, Grass> m_grassesById = new();

  private void Start() {
    // Register event in the terrain manager
    m_terrainManager.ChunkSpawned += ChunkSpawnedEventHandler;

    InitializeGrassesDatabase();
  }

  private void Update() {
    if (logCountOfRenderedInstances) {
      int total = 0;

      GrassChunk[] chunks = FindObjectsOfType<GrassChunk>();
      foreach (var chunk in chunks) {
        if (chunk.isUsingGrass && chunk.isWithinMaxDistance && chunk.groups != null) {
          foreach (var (submesh, batch) in chunk.groups) {
            total += batch.matrices.Count;
          }
        }
      }

      Debug.Log($"Count of rendered grass instances: {total}");
    }
  }

  private void OnDestroy() {
    // Unregister event
    m_terrainManager.ChunkSpawned -= ChunkSpawnedEventHandler;
  }

  private void InitializeGrassesDatabase() {
    foreach (var grassSpawner in terrainManager.terrainShape.grassSpawners) {
      m_grassesById[grassSpawner.grass.id] = grassSpawner.grass;
    }
  }

  private void ChunkSpawnedEventHandler(TerrainChunk chunk) {
    // Add grass
    if (chunk.levelOfDetail == 1 && chunk.terrainShape.useGrass) {
      GrassChunk grassChunk = chunk.gameObject.AddComponent<GrassChunk>();
      grassChunk.manager = this;
      grassChunk.maxDistance = maxDistance;
      grassChunk.logGenerationInfo = logGenerationInfo;
      grassChunk.logInstancingInfo = logInstancingInfo;
      grassChunk.skipRendering = skipRendering;
    }
  }
}