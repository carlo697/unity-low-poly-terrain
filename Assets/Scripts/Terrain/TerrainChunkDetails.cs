
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

[RequireComponent(typeof(TerrainChunk))]
public class TerrainChunkDetails : MonoBehaviour {
  private TerrainChunk m_chunk;

  private void Awake() {
    m_chunk = GetComponent<TerrainChunk>();
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
      // Start recording time
      System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
      timer.Start();

      Bounds bounds = collider.bounds;
      RaycastHit[] hits = new RaycastHit[10];
      float distance = collider.bounds.size.y;
      int totalHits = 0;

      if (collider) {
        for (int i = 0; i < 20000; i++) {
          Ray ray = new Ray(RandomPointInBounds(bounds), Vector3.down);
          int hitCount = Physics.RaycastNonAlloc(ray, hits, distance);

          for (int j = 0; j < hitCount; j++) {
            RaycastHit hit = hits[j];
            totalHits++;
          }
        }
      }

      // Stop recording time
      timer.Stop();
      Debug.LogFormat("{0} ms", timer.ElapsedMilliseconds);
      Debug.LogFormat("Total hits: {0}", totalHits);
    }
  }

  [ContextMenu("Test Raycasts With Job")]
  public void TestRaycastWithJob() {
    // Check if it has a mesh collider
    MeshCollider collider = GetComponent<MeshCollider>();

    if (collider) {
      // Start recording time
      System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
      timer.Start();

      // Create the arrays needed to schedule the job
      int maxHits = 5;
      int rayCount = 20000;
      var results = new NativeArray<RaycastHit>(rayCount * maxHits, Allocator.TempJob);
      var commands = new NativeArray<RaycastCommand>(rayCount, Allocator.TempJob);

      QueryParameters parameters = QueryParameters.Default;
      parameters.hitBackfaces = true;

      // Create the raycast commands
      Bounds bounds = collider.bounds;
      for (int i = 0; i < rayCount; i++) {
        commands[i] = new RaycastCommand(
          RandomPointInBounds(bounds),
          Vector3.down,
          parameters
        );
      }

      // Schedule the raycasts and complete the job
      JobHandle handle = RaycastCommand.ScheduleBatch(
        commands,
        results,
        1000,
        maxHits
      );
      handle.Complete();

      // Read the results
      int totalHits = 0;
      for (int i = 0; i < results.Length; i++) {
        if (results[i].collider != null) {
          totalHits++;
        }
      }

      // Dispose the buffers
      results.Dispose();
      commands.Dispose();

      // Stop recording time
      timer.Stop();
      Debug.LogFormat("{0} ms", timer.ElapsedMilliseconds);
      Debug.LogFormat("Total hits: {0}", totalHits);
    }
  }

  [ContextMenu("Test Looping Points")]
  public void TestLoopingPoints() {
    // Start recording time
    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    float total = 0;
    for (int i = 0; i < 20; i++) {
      for (int j = 0; j < m_chunk.points.Length; j++) {
        CubeGridPoint point = m_chunk.points[j];
        total += point.value;
      }
    }

    // Stop recording time
    timer.Stop();
    Debug.LogFormat("{0} ms", timer.ElapsedMilliseconds);
    Debug.LogFormat("Total value: {0}", total);
  }
}
