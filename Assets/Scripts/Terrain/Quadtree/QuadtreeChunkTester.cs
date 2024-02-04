using UnityEngine;
using System.Collections.Generic;

public class QuadtreeChunkTester : MonoBehaviour {
  public float viewDistance = 5000f;
  public DistanceShape distanceShape = DistanceShape.Circle;
  public Vector3 chunkSize = new Vector3(32f, 128f, 32f);

  public bool testPosition;
  public Transform testTransform;

  private List<QuadtreeChunk> m_quadtrees = new();
  private List<Bounds> m_visibleChunkBounds = new();

  public int levelsOfDetail = 8;
  private List<float> m_levelDistances = new();

  private void UpdateVisibleChunkPositions(Camera camera) {
    Vector3 cameraPosition = camera.transform.position;

    QuadtreeChunk.CalculateLevelDistances(
      m_levelDistances,
      levelsOfDetail,
      chunkSize.x,
      2f,
      2.5f
    );

    QuadtreeChunk.CreateQuadtrees(
      m_quadtrees,
      cameraPosition,
      chunkSize,
      Vector3.zero,
      m_levelDistances,
      viewDistance,
      distanceShape,
      true
    );

    QuadtreeChunk.RetrieveVisibleChunks(
      m_visibleChunkBounds,
      m_quadtrees,
      cameraPosition,
      viewDistance
    );
  }

  private (bool, QuadtreeChunkNode) TestPositon(Vector3 position, int times) {
    System.Diagnostics.Stopwatch timer = new();
    timer.Start();

    bool foundTestPosition = false;
    QuadtreeChunkNode testNode = default;

    for (int i = 0; i < times; i++) {
      (foundTestPosition, testNode) = QuadtreeChunk.GetChunkAt(m_quadtrees, position);
    }

    timer.Stop();
    Debug.Log($"Time to get the position {times} times: {timer.Elapsed.TotalMilliseconds}");

    return (foundTestPosition, testNode);
  }

  private void OnDrawGizmos() {
    Gizmos.color = new Color(1f, 1f, 1f, 0.1f);

    UpdateVisibleChunkPositions(Camera.main);

    bool foundTestPosition;
    QuadtreeChunkNode testNode = default;
    Vector3 testPosition = Vector3.zero;
    if (testTransform) {
      testPosition = testTransform.transform.position;

      (foundTestPosition, testNode) = TestPositon(testPosition, 1);
      TestPositon(testPosition, 100);
      TestPositon(testPosition, 1000);
      TestPositon(testPosition, 10000);
      TestPositon(testPosition, 20000);
    } else {
      foundTestPosition = false;
    }

    for (int i = 0; i < m_visibleChunkBounds.Count; i++) {
      Bounds bounds = m_visibleChunkBounds[i];

      if (foundTestPosition && testNode.bounds == bounds) {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(bounds.center, bounds.size * 1.1f);
      } else {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
      }
    }
  }
}
