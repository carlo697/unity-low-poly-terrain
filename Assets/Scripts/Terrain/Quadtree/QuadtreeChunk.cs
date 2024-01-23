using UnityEngine;
using System.Collections.Generic;

public enum DistanceShape {
  Square,
  Circle
}

public class QuadtreeChunk {
  public int level;
  public Bounds bounds;
  public QuadtreeChunk[] children;

  public QuadtreeChunk(
    int level,
    Bounds bounds
  ) {
    this.level = level;
    this.bounds = bounds;
    this.children = null;
  }

  public void Build(
    List<float> levelDistances,
    DistanceShape distanceShape,
    Vector3 cameraPosition,
    bool drawGizmos = false
  ) {
    if (level > levelDistances.Count - 1) {
      return;
    }

    // Get the distance to the camera
    Vector3 closestPoint = bounds.ClosestPoint(cameraPosition);
    float levelDistance = levelDistances[level];

    // Debug gizmos
    if (drawGizmos) {
      Gizmos.color = Color.red;
      Gizmos.DrawWireSphere(cameraPosition, levelDistance);
    }

    // Determine if we need to split the node
    bool split = false;
    if (distanceShape == DistanceShape.Square) {
      Bounds cameraBounds = new Bounds(
        cameraPosition,
        Vector3.one * levelDistance * 2f
      );
      split = cameraBounds.Contains(closestPoint);
    } else {
      split = Vector3.Distance(cameraPosition, closestPoint) <= levelDistance;
    }

    // Split the node
    if (split) {
      Vector3 halfExtents = new Vector3(
        bounds.extents.x / 2f,
        bounds.extents.y,
        bounds.extents.z / 2f
      );

      Vector3 halfSize = new Vector3(
        bounds.size.x / 2f,
        bounds.size.y,
        bounds.size.z / 2f
      );

      children = new QuadtreeChunk[4];

      // North east
      children[0] = new QuadtreeChunk(
        level + 1,
        new Bounds(bounds.center + new Vector3(halfExtents.x, 0f, halfExtents.z), halfSize)
      );

      // South east
      children[1] = new QuadtreeChunk(
        level + 1,
        new Bounds(bounds.center + new Vector3(halfExtents.x, 0f, -halfExtents.z), halfSize)
      );

      // South west
      children[2] = new QuadtreeChunk(
        level + 1,
        new Bounds(bounds.center + new Vector3(-halfExtents.x, 0f, -halfExtents.z), halfSize)
      );

      // North west
      children[3] = new QuadtreeChunk(
        level + 1,
        new Bounds(bounds.center + new Vector3(-halfExtents.x, 0f, halfExtents.z), halfSize)
      );

      for (int i = 0; i < children.Length; i++) {
        children[i].Build(levelDistances, distanceShape, cameraPosition, drawGizmos);
      }
    }
  }

  public List<QuadtreeChunk> GetChunksRecursively(List<QuadtreeChunk> list) {
    list.Add(this);

    if (children != null) {
      for (int i = 0; i < children.Length; i++) {
        children[i].GetChunksRecursively(list);
      }
    }

    return list;
  }

  public QuadtreeChunk GetChunkAt(Vector3 position) {
    if (children != null) {
      for (int i = 0; i < children.Length; i++) {
        QuadtreeChunk chunk = children[i];

        if (chunk.bounds.Contains(position)) {
          return chunk.GetChunkAt(position);
        }
      }
    }

    return this;
  }

  public static QuadtreeChunk GetChunkAt(List<QuadtreeChunk> chunks, Vector3 position) {
    for (int i = 0; i < chunks.Count; i++) {
      QuadtreeChunk chunk = chunks[i];

      if (chunk.bounds.Contains(position)) {
        return chunk.GetChunkAt(position);
      }
    }

    return null;
  }

  public static void CalculateLevelDistances(
    List<float> results,
    int levels,
    float baseChunkSize,
    float detailDistanceBase = 2f,
    float detailDistanceMultiplier = 1f
  ) {
    results.Clear();

    // Calculate the whole size of the tree so that the minimun size of a chunk
    // is equal to chunkSize.x
    float minimunChunkSize = baseChunkSize;
    float areaExtents = Mathf.Pow(2, levels - 1) * minimunChunkSize;
    float areaSize = areaExtents * 2f;
    Vector2 extents = new Vector2(areaExtents, areaExtents);

    // Calculate the distances for the levels of detail
    for (int i = levels - 1; i >= 0; i--) {
      float distance = Mathf.Pow(detailDistanceBase, i + 1f) * minimunChunkSize * detailDistanceMultiplier;
      results.Add(distance);
    }

    // Harcoded distances for the levels of detail
    // m_levelDistances = new List<float> {
    //   8192f,
    //   4096f,
    //   2048f,
    //   1024f,
    //   512f,
    //   256f,
    //   128f,
    //   64f
    // };
  }

  public static void CreateQuadtrees(
    List<QuadtreeChunk> results,
    Vector3 cameraPosition,
    Vector3 chunkSize,
    Vector3 chunkOffset,
    List<float> levelDistances,
    float viewDistance,
    DistanceShape distanceShape = DistanceShape.Square,
    bool drawGizmos = false
  ) {
    results.Clear();

    // Calculate the whole size of a tree so that the minimun size of a node inside it
    // is equal to chunkSize.x
    float treeExtents = Mathf.Pow(2f, levelDistances.Count - 1f) * chunkSize.x;
    float treeSize = treeExtents * 2f;

    // Get the position of the tree the player is standing right now
    Vector2 mainAreaCoords = new Vector2(
      Mathf.Floor(cameraPosition.x / treeSize),
      Mathf.Floor(cameraPosition.z / treeSize)
    );
    Vector2 mainAreaPosition = new Vector2(
      mainAreaCoords.x * treeSize,
      mainAreaCoords.y * treeSize
    );

    // Calculate how many trees are visible
    int visibleX = Mathf.CeilToInt(viewDistance / treeSize);
    int visibleY = Mathf.CeilToInt(viewDistance / treeSize);

    // Iterate over the visible trees
    for (
      int y = (int)mainAreaCoords.y - visibleY;
      y <= mainAreaCoords.y + visibleY;
      y++
    ) {
      for (
        int x = (int)mainAreaCoords.x - visibleX;
        x <= mainAreaCoords.x + visibleX;
        x++
      ) {
        // Build a bounds
        Vector3 position3d = new Vector3(x * treeSize + treeExtents, 0f, y * treeSize + treeExtents) + chunkOffset;
        Vector3 size3d = new Vector3(treeSize, chunkSize.y, treeSize);
        Bounds bounds = new Bounds(position3d, size3d);

        // Check if a sphere of radius 'distance' is touching the bounds
        float distanceToChunk = Mathf.Sqrt(bounds.SqrDistance(cameraPosition));
        if (distanceToChunk > viewDistance) {
          continue;
        }

        // Instantiate the tree
        QuadtreeChunk tree = new QuadtreeChunk(0, bounds);
        results.Add(tree);

        // Debug gizmos
        if (drawGizmos) {
          Gizmos.color = Color.blue;
          Gizmos.DrawWireCube(tree.bounds.center, tree.bounds.size);
        }

        // Build the tree
        tree.Build(levelDistances, distanceShape, cameraPosition, drawGizmos);
      }
    }
  }

  public static void RetrieveVisibleChunks(
    List<QuadtreeChunk> results,
    List<QuadtreeChunk> chunks,
    Vector3 cameraPosition,
    float viewDistance
  ) {
    results.Clear();

    // Get all the quadrants
    for (int i = 0; i < chunks.Count; i++) {
      chunks[i].GetChunksRecursively(results);
    }

    // Filter the list to leave only the quadrants with no children
    // and that are inside the view distance
    for (int i = results.Count - 1; i >= 0; i--) {
      QuadtreeChunk chunk = results[i];
      if (chunk.children != null) {
        results.RemoveAt(i);
        continue;
      }

      Vector3 closestPoint = chunk.bounds.ClosestPoint(cameraPosition);
      if (Vector3.Distance(closestPoint, cameraPosition) > viewDistance) {
        results.RemoveAt(i);
        continue;
      }
    }
  }
}