using UnityEngine;
using System.Collections.Generic;

public enum DistanceShape {
  Square,
  Circle
}

class QuadtreeChunk {
  public List<float> levelDistances;
  public int level;
  public Vector2 position;
  public Vector2 extents;
  public QuadtreeChunk[] children;
  public Bounds bounds;
  public DistanceShape distanceShape;

  public QuadtreeChunk(
    List<float> levelDistances,
    int level,
    Vector2 position,
    Vector2 extents,
    DistanceShape distanceShape = DistanceShape.Square
  ) {
    this.levelDistances = levelDistances;
    this.level = level;
    this.position = position;
    this.extents = extents;
    this.distanceShape = distanceShape;

    this.children = null;
    this.bounds = new Bounds(
      new Vector3(position.x, 0f, position.y),
      new Vector3(extents.x * 2f, 1f, extents.y * 2f)
    );
  }

  public void Build(Vector3 cameraPosition, bool drawGizmos = false) {
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
      Vector2 halfExtents = extents / 2f;
      children = new QuadtreeChunk[4] {
        // North east
        new QuadtreeChunk(
          levelDistances,
          level + 1,
          position + halfExtents,
          halfExtents,
          distanceShape
        ),
        // South east
        new QuadtreeChunk(
          levelDistances,
          level + 1,
          position + new Vector2(halfExtents.x, -halfExtents.y),
          halfExtents,
          distanceShape
        ),
        // South west
        new QuadtreeChunk(
          levelDistances,
          level + 1,
          position - halfExtents,
          halfExtents,
          distanceShape
        ),
        // North west
        new QuadtreeChunk(
          levelDistances,
          level + 1,
          position + new Vector2(-halfExtents.x, halfExtents.y),
          halfExtents,
          distanceShape
        )
      };

      for (int i = 0; i < children.Length; i++) {
        children[i].Build(cameraPosition, drawGizmos);
      }
    }
  }

  public List<QuadtreeChunk> GetChunksRecursively(List<QuadtreeChunk> list = null) {
    list = list ?? new List<QuadtreeChunk>();
    list.Add(this);

    if (children != null) {
      for (int i = 0; i < children.Length; i++) {
        children[i].GetChunksRecursively(list);
      }
    }

    return list;
  }

  public static List<float> CalculateLevelDistances(
    float baseChunkSize,
    int levelsOfDetail = 8,
    float detailDistanceBase = 2f,
    float detailDistanceMultiplier = 1f,
    int detailDistanceDecreaseAtLevel = 1,
    float detailDistanceConstantDecrease = 0f
  ) {
    List<float> levelDistances = new List<float>();

    // Calculate the whole size of the tree so that the minimun size of a chunk
    // is equal to chunkSize.x
    float minimunChunkSize = baseChunkSize;
    float areaExtents = Mathf.Pow(2, levelsOfDetail - 1) * minimunChunkSize;
    float areaSize = areaExtents * 2f;
    Vector2 extents = new Vector2(areaExtents, areaExtents);

    // Calculate the distances for the levels of detail
    for (int i = 0; i < levelsOfDetail; i++) {
      int decreaseLevel = Mathf.Max(0, i - detailDistanceDecreaseAtLevel);
      levelDistances.Add(
        (
          (Mathf.Pow(detailDistanceBase, i + 1f) * minimunChunkSize)
          / (1f + (float)decreaseLevel * detailDistanceConstantDecrease)
        ) * detailDistanceMultiplier
      );
    }
    levelDistances.Reverse();

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

    return levelDistances;
  }

  public static List<QuadtreeChunk> CreateQuadtree(
    Vector3 cameraPosition,
    Vector2 chunkSize,
    List<float> levelDistances,
    float viewDistance,
    DistanceShape distanceShape = DistanceShape.Square,
    List<QuadtreeChunk> list = null,
    bool drawGizmos = false
  ) {
    // Calculate the whole size of the tree so that the minimun size of a chunk
    // is equal to chunkSize.x
    float minimunChunkSize = chunkSize.x;
    float areaExtents = Mathf.Pow(2f, levelDistances.Count - 1f) * minimunChunkSize;
    float areaSize = areaExtents * 2f;
    Vector2 extents = new Vector2(areaExtents, areaExtents);

    // Create the tree
    if (list == null) {
      list = new List<QuadtreeChunk>();
    } else {
      list.Clear();
    }

    // Get the area the player is standing right now
    Vector2 mainAreaCoords = new Vector2(
      Mathf.Floor(cameraPosition.x / areaSize),
      Mathf.Floor(cameraPosition.z / areaSize)
    );
    Vector2 mainAreaPosition = new Vector2(
      mainAreaCoords.x * areaSize,
      mainAreaCoords.y * areaSize
    );

    int visibleX = Mathf.CeilToInt(viewDistance / areaSize);
    int visibleY = Mathf.CeilToInt(viewDistance / areaSize);

    // Build a list of the coords of the visible chunks
    for (
      int y = (int)mainAreaCoords.y - visibleY;
      y <= mainAreaCoords.y + visibleY;
      y++) {
      for (
        int x = (int)mainAreaCoords.x - visibleX;
        x <= mainAreaCoords.x + visibleX;
        x++
      ) {
        Vector2 coords = new Vector3(x, y);
        Vector2 position = new Vector2(
          coords.x * areaSize + areaExtents,
          coords.y * areaSize + areaExtents
        );
        Bounds bounds = new Bounds(
          new Vector3(position.x, 0, position.y),
          new Vector3(extents.x * 2f, chunkSize.y, extents.y * 2f)
        );

        // Check if a sphere of radius 'distance' is touching the chunk
        float distanceToChunk = Mathf.Sqrt(bounds.SqrDistance(cameraPosition));
        if (distanceToChunk > viewDistance) {
          continue;
        }

        if (drawGizmos) {
          Gizmos.color = Color.blue;
          Gizmos.DrawWireCube(
            new Vector3(position.x, 0, position.y),
            new Vector3(extents.x * 2f, chunkSize.y, extents.y * 2f)
          );
        }

        QuadtreeChunk area = new QuadtreeChunk(
          levelDistances,
          0,
          position,
          new Vector2(areaExtents, areaExtents),
          distanceShape
        );
        area.Build(cameraPosition, drawGizmos);
        list.Add(area);
      }
    }

    return list;
  }

  public static List<QuadtreeChunk> RetrieveVisibleChunks(
    List<QuadtreeChunk> chunks,
    Vector3 cameraPosition,
    float viewDistance
  ) {
    List<QuadtreeChunk> list = new List<QuadtreeChunk>();

    for (int i = 0; i < chunks.Count; i++) {
      chunks[i].GetChunksRecursively(list);
    }

    List<QuadtreeChunk> visibleChunks = new List<QuadtreeChunk>();
    for (int i = 0; i < list.Count; i++) {
      QuadtreeChunk chunk = list[i];
      if (chunk.children == null) {
        Vector3 closestPoint = chunk.bounds.ClosestPoint(cameraPosition);

        if (Vector3.Distance(closestPoint, cameraPosition) <= viewDistance) {
          visibleChunks.Add(chunk);
        }
      }
    }

    return visibleChunks;
  }
}