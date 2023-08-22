
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections;
using System.Collections.Generic;

public class DetailsChunk : MonoBehaviour {
  private static int updatesThisFrame;
  private static int lastFrameCount;

  public Bounds bounds;
  public TerrainShape terrainShape;

  private float m_levelOfDetail = 1f;
  private bool m_updateFlag;
  private bool m_isGenerating;
  private List<DetailInstance> m_instances = new List<DetailInstance>();

  private void Update() {
    if (lastFrameCount != Time.frameCount) {
      updatesThisFrame = 0;
      lastFrameCount = Time.frameCount;
    }

    if (m_updateFlag && !m_isGenerating && updatesThisFrame < 2) {
      m_isGenerating = true;
      m_updateFlag = false;
      StartCoroutine(PlaceDetails());

      updatesThisFrame++;
    }
  }

  public void RequestUpdate(float levelOfDetail) {
    m_updateFlag = true;
    m_levelOfDetail = levelOfDetail;
  }

  public IEnumerator PlaceDetails() {
    yield return null;

    // Record time taken to generate and spawn the details
    System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    // Generate a seed for this chunk
    ulong seed = (ulong)(terrainShape.terrainSeed + bounds.center.GetHashCode());

    // Create a list of temporal instances using the spawners
    List<TempDetailInstance> tempInstances = new List<TempDetailInstance>();
    for (int i = 0; i < terrainShape.detailSpawners.Length; i++) {
      DetailSpawner spawner = terrainShape.detailSpawners[i];

      // Call the "Spawn" method to create the temporal instances
      List<TempDetailInstance> spawnResults = spawner.Spawn(seed, bounds, m_levelOfDetail);

      // Add them to the main array
      tempInstances.AddRange(spawnResults);
      // for (int resultIndex = 0; resultIndex < spawnResults.Count; resultIndex++) {
      //   tempInstances.Add(spawnResults[resultIndex]);
      // }
    }

    // if (bounds.center.x == -304f && bounds.center.z == -112f) {
    //   Debug.LogFormat("{0}, {1}, {2}", m_levelOfDetail, seed, tempInstances.Count);
    // }

    // Create the arrays needed to schedule the job for the raycasts
    var results = new NativeArray<RaycastHit>(tempInstances.Count, Allocator.Persistent);
    var commands = new NativeArray<RaycastCommand>(tempInstances.Count, Allocator.Persistent);

    QueryParameters parameters = QueryParameters.Default;
    parameters.hitBackfaces = true;

    // Transfer the raycast commands from the temporal instances to the "commands" array
    for (int i = 0; i < tempInstances.Count; i++) {
      commands[i] = tempInstances[i].raycastCommand;
    }

    // if (bounds.center.x == -304f && bounds.center.z == -112f) {
    //   Debug.LogFormat("instances: {0}", m_instances.Count);
    //   Debug.Break();
    // }

    // Schedule the raycast commands
    JobHandle handle = RaycastCommand.ScheduleBatch(
      commands,
      results,
      1000,
      1
    );

    // Wait for the job to be completed
    while (!handle.IsCompleted) {
      timer.Stop();
      yield return new WaitForSeconds(.1f);
    }

    timer.Start();

    // Complete the job
    handle.Complete();

    // Get the final instances and register them
    m_instances.Clear();
    for (int i = 0; i < results.Length; i++) {
      if (results[i].collider != null) {
        DetailInstance? instance = tempInstances[i].GetFinalInstance(results[i]);

        if (instance.HasValue)
          m_instances.Add(instance.Value);
      }
    }

    // if (bounds.center.x == -304f && bounds.center.z == -112f) {
    //   Debug.LogFormat("instances: {0}", m_instances.Count);
    //   Debug.Break();
    // }

    // Dispose the buffers
    results.Dispose();
    commands.Dispose();

    // Delete the old game objects
    int childCount = transform.childCount;
    for (int i = childCount - 1; i >= 0; i--) {
      // GameObject.Destroy(transform.GetChild(i).gameObject);

      PrefabPool.Release(transform.GetChild(i).gameObject);
    }

    // Instantiate new game objects
    for (int i = 0; i < m_instances.Count; i++) {
      DetailInstance instance = m_instances[i];

      // GameObject obj = GameObject.Instantiate(
      //   instance.prefab,
      //   instance.position,
      //   instance.rotation,
      //   transform
      // );

      GameObject obj = PrefabPool.Get(instance.prefab);
      obj.transform.SetParent(transform);
      obj.transform.position = instance.position;
      obj.transform.rotation = instance.rotation;
      obj.transform.localScale = instance.scale;
    }

    timer.Stop();
    Debug.Log(
      string.Format(
        "Time: {0} ms, instances: {1}",
        timer.ElapsedMilliseconds,
        m_instances.Count
      )
    );

    m_isGenerating = false;
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

  // [ContextMenu("Test Looping Points")]
  // public void TestLoopingPoints() {
  //   // Start recording time
  //   System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
  //   timer.Start();

  //   float total = 0;
  //   for (int i = 0; i < 20; i++) {
  //     for (int j = 0; j < m_chunk.points.Length; j++) {
  //       CubeGridPoint point = m_chunk.points[j];
  //       total += point.value;
  //     }
  //   }

  //   // Stop recording time
  //   timer.Stop();
  //   Debug.LogFormat("{0} ms", timer.ElapsedMilliseconds);
  //   Debug.LogFormat("Total value: {0}", total);
  // }

  private void OnDrawGizmosSelected() {
    Gizmos.color = Color.white;
    Gizmos.DrawWireCube(bounds.center, bounds.size);
  }
}
