using UnityEngine;
using System.Collections.Generic;

public enum DistanceShape {
  Square,
  Circle
}

public class QuadtreeChunk {
  public int level;
  public QuadtreeChunk[] children;
  public Bounds bounds;

  public QuadtreeChunk(
    int level,
    Vector3 position,
    Vector3 extents
  ) {
    this.level = level;
    this.children = null;
    this.bounds = new Bounds(position, extents * 2f);
  }

  public void Build(
    List<float> levelDistances,
    DistanceShape distanceShape,
    Vector3 cameraPosition,
    bool drawGizmos = false
  ) {
    if (level > levelDistances.Count - 1)
      return;

    Vector3 closestPoint = bounds.ClosestPoint(cameraPosition);
    float levelDistance = levelDistances[level];

    if (drawGizmos) {
      Gizmos.color = Color.red;
      Gizmos.DrawWireSphere(cameraPosition, levelDistance);
    }

    bool isInside = false;
    if (distanceShape == DistanceShape.Square) {
      Bounds cameraBounds = new Bounds(
        cameraPosition,
        Vector3.one * levelDistance * 2f
      );
      isInside = cameraBounds.Contains(closestPoint);
    } else {
      isInside = Vector3.Distance(cameraPosition, closestPoint) <= levelDistance;
    }

    if (isInside) {
      Vector3 halfExtents = new Vector3(
        bounds.extents.x / 2f,
        bounds.extents.y,
        bounds.extents.z / 2f
      );

      children = new QuadtreeChunk[4] {
        // North east
        new QuadtreeChunk(
          level + 1,
          bounds.center + new Vector3(halfExtents.x, 0f, halfExtents.z),
          halfExtents
        ),
        // South east
        new QuadtreeChunk(
          level + 1,
          bounds.center + new Vector3(halfExtents.x, 0f, -halfExtents.z),
          halfExtents
        ),
        // South west
        new QuadtreeChunk(
          level + 1,
          bounds.center + new Vector3(-halfExtents.x, 0f, -halfExtents.z),
          halfExtents
        ),
        // North west
        new QuadtreeChunk(
          level + 1,
          bounds.center + new Vector3(-halfExtents.x, 0f, halfExtents.z),
          halfExtents
        )
      };

      for (int i = 0; i < children.Length; i++) {
        children[i].Build(levelDistances, distanceShape, cameraPosition, drawGizmos);
      }
    }
  }

  public List<QuadtreeChunk> GetChunksRecursively() {
    return GetChunksRecursively(new List<QuadtreeChunk>());
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

  public static List<float> CalculateLevelDistances(
    int levels,
    float baseChunkSize,
    float detailDistanceBase = 2f,
    float detailDistanceMultiplier = 1f,
    int detailDistanceDecreaseAtLevel = 1,
    float detailDistanceConstantDecrease = 0f
  ) {
    return CalculateLevelDistances(
      new List<float>(),
      levels,
      baseChunkSize,
      detailDistanceBase,
      detailDistanceMultiplier,
      detailDistanceDecreaseAtLevel,
      detailDistanceConstantDecrease
    );
  }

  public static List<float> CalculateLevelDistances(
    List<float> results,
    int levels,
    float baseChunkSize,
    float detailDistanceBase = 2f,
    float detailDistanceMultiplier = 1f,
    int detailDistanceDecreaseAtLevel = 1,
    float detailDistanceConstantDecrease = 0f
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
      int decreaseLevel = Mathf.Max(0, i - detailDistanceDecreaseAtLevel);
      float distance = (
        (Mathf.Pow(detailDistanceBase, i + 1f) * minimunChunkSize)
        / (1f + (float)decreaseLevel * detailDistanceConstantDecrease)
        ) * detailDistanceMultiplier;

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

    return results;
  }

  public static List<QuadtreeChunk> CreateQuadtree(
    Vector3 cameraPosition,
    Vector3 chunkSize,
    Vector3 chunkOffset,
    List<float> levelDistances,
    float viewDistance,
    DistanceShape distanceShape = DistanceShape.Square,
    List<QuadtreeChunk> list = null,
    bool drawGizmos = false
  ) {
    // Calculate the whole size of the tree so that the minimun size of a chunk
    // is equal to chunkSize.x
    float quadMinimunSize = chunkSize.x;
    float treeExtents = Mathf.Pow(2f, levelDistances.Count - 1f) * quadMinimunSize;
    float treeSize = treeExtents * 2f;
    Vector2 treeExtents2d = new Vector2(treeExtents, treeExtents);

    // Create the tree
    if (list == null) {
      list = new List<QuadtreeChunk>();
    } else {
      list.Clear();
    }

    // Get the area the player is standing right now
    Vector2 mainAreaCoords = new Vector2(
      Mathf.Floor(cameraPosition.x / treeSize),
      Mathf.Floor(cameraPosition.z / treeSize)
    );
    Vector2 mainAreaPosition = new Vector2(
      mainAreaCoords.x * treeSize,
      mainAreaCoords.y * treeSize
    );

    int visibleX = Mathf.CeilToInt(viewDistance / treeSize);
    int visibleY = Mathf.CeilToInt(viewDistance / treeSize);

    // Build a list of the coords of the visible chunks
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
        Vector3 coords2d = new Vector3(x, y);
        Vector3 position2d = new Vector2(
          coords2d.x * treeSize + treeExtents,
          coords2d.y * treeSize + treeExtents
        );

        Vector3 position3d = new Vector3(position2d.x, 0, position2d.y) + chunkOffset;
        Vector3 size3d = new Vector3(treeExtents2d.x * 2f, chunkSize.y, treeExtents2d.y * 2f);

        Bounds bounds = new Bounds(position3d, size3d);

        // Check if a sphere of radius 'distance' is touching the chunk
        float distanceToChunk = Mathf.Sqrt(bounds.SqrDistance(cameraPosition));
        if (distanceToChunk > viewDistance) {
          continue;
        }

        QuadtreeChunk tree = new QuadtreeChunk(
          0,
          position3d,
          size3d / 2f
        );

        if (drawGizmos) {
          Gizmos.color = Color.blue;
          Gizmos.DrawWireCube(tree.bounds.center, tree.bounds.size);
        }

        tree.Build(levelDistances, distanceShape, cameraPosition, drawGizmos);
        list.Add(tree);
      }
    }

    return list;
  }

  public static List<QuadtreeChunk> RetrieveVisibleChunks(
    List<QuadtreeChunk> chunks,
    Vector3 cameraPosition,
    float viewDistance
  ) {
    return RetrieveVisibleChunks(new List<QuadtreeChunk>(), chunks, cameraPosition, viewDistance);
  }

  public static List<QuadtreeChunk> RetrieveVisibleChunks(
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

    return results;
  }
}