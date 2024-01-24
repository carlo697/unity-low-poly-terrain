using UnityEngine;
using System.Collections.Generic;

public enum DistanceShape {
  Square,
  Circle
}

public struct QuadtreeChunkNode {
  public int firstChild;
  public Bounds bounds;

  public bool hasChildren { get { return firstChild != -1; } }

  public QuadtreeChunkNode(int firstChild, Bounds bounds) {
    this.firstChild = firstChild;
    this.bounds = bounds;
  }
}

public class QuadtreeChunk {
  public Bounds bounds;
  public List<QuadtreeChunkNode> nodes;

  public QuadtreeChunk(Bounds bounds, int capacity = 0) {
    this.bounds = bounds;
    this.nodes = new List<QuadtreeChunkNode>(capacity);
  }

  public void Clear() {
    this.nodes.Clear();
  }

  public void Build(
    List<float> levelDistances,
    DistanceShape distanceShape,
    Vector3 cameraPosition,
    bool drawGizmos = false
  ) {
    // Add the root node
    this.nodes.Add(new QuadtreeChunkNode(-1, bounds));

    // Build the grid starting from the root node
    BuildInternal(0, 0, levelDistances, distanceShape, cameraPosition, drawGizmos);
  }

  private void BuildInternal(
    int nodeIndex,
    int level,
    List<float> levelDistances,
    DistanceShape distanceShape,
    Vector3 cameraPosition,
    bool drawGizmos = false
  ) {
    // Get the current node
    QuadtreeChunkNode currentNode = nodes[nodeIndex];
    Bounds currentBounds = currentNode.bounds;

    // The maximun level has been reached
    if (level > levelDistances.Count - 1) {
      return;
    }

    // Get the distance to the camera
    Vector3 closestPoint = currentBounds.ClosestPoint(cameraPosition);
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
        Vector3.one * (levelDistance * 2f)
      );
      split = cameraBounds.Contains(closestPoint);
    } else {
      split = Vector3.Distance(cameraPosition, closestPoint) <= levelDistance;
    }

    // Split the node
    if (split) {
      // Set the index of the first child
      int firstChildIndex = this.nodes.Count;
      currentNode.firstChild = firstChildIndex;
      nodes[nodeIndex] = currentNode;

      // Calculate the sizes needed for the child nodes
      int childLevel = level + 1;
      Vector3 halfExtents = new Vector3(
        currentBounds.extents.x / 2f,
        currentBounds.extents.y,
        currentBounds.extents.z / 2f
      );
      Vector3 halfSize = new Vector3(
        currentBounds.size.x / 2f,
        currentBounds.size.y,
        currentBounds.size.z / 2f
      );

      // North east
      this.nodes.Add(
        new QuadtreeChunkNode(
          -1,
          new Bounds(currentBounds.center + new Vector3(halfExtents.x, 0f, halfExtents.z), halfSize)
        )
      );

      // South east
      this.nodes.Add(
        new QuadtreeChunkNode(
          -1,
          new Bounds(currentBounds.center + new Vector3(halfExtents.x, 0f, -halfExtents.z), halfSize)
        )
      );

      // South west
      this.nodes.Add(
        new QuadtreeChunkNode(
          -1,
          new Bounds(currentBounds.center + new Vector3(-halfExtents.x, 0f, -halfExtents.z), halfSize)
        )
      );

      // North west
      this.nodes.Add(
        new QuadtreeChunkNode(
          -1,
          new Bounds(currentBounds.center + new Vector3(-halfExtents.x, 0f, halfExtents.z), halfSize)
        )
      );

      BuildInternal(
        firstChildIndex,
        childLevel,
        levelDistances,
        distanceShape,
        cameraPosition,
        drawGizmos
      );
      BuildInternal(
        firstChildIndex + 1,
        childLevel,
        levelDistances,
        distanceShape,
        cameraPosition,
        drawGizmos
      );
      BuildInternal(
        firstChildIndex + 2,
        childLevel,
        levelDistances,
        distanceShape,
        cameraPosition,
        drawGizmos
      );
      BuildInternal(
        firstChildIndex + 3,
        childLevel,
        levelDistances,
        distanceShape,
        cameraPosition,
        drawGizmos
      );
    }
  }

  public (bool, QuadtreeChunkNode) GetChunkAt(Vector3 position, int parentIndex = 0) {
    // for (int i = 0; i < nodes.Count; i++) {
    //   QuadtreeChunkNode node = nodes[i];

    //   if (!node.hasChildren && node.bounds.Contains(position)) {
    //     return (true, node);
    //   }
    // }

    // return (false, default);

    QuadtreeChunkNode parent = nodes[parentIndex];

    if (parent.hasChildren) {
      Vector3 center = parent.bounds.center;

      if (position.x >= center.x) {
        if (position.z >= center.z) {
          return GetChunkAt(position, parent.firstChild);
        } else {
          return GetChunkAt(position, parent.firstChild + 1);
        }
      } else {
        if (position.z >= center.z) {
          return GetChunkAt(position, parent.firstChild + 3);
        } else {
          return GetChunkAt(position, parent.firstChild + 2);
        }
      }
    }

    return (true, parent);
  }

  public static (bool, QuadtreeChunkNode) GetChunkAt(List<QuadtreeChunk> chunks, Vector3 position) {
    for (int i = 0; i < chunks.Count; i++) {
      QuadtreeChunk chunk = chunks[i];

      if (chunk.bounds.Contains(position)) {
        return chunk.GetChunkAt(position);
      }
    }

    return (false, default);
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
        QuadtreeChunk tree = new QuadtreeChunk(bounds);
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
    List<Bounds> results,
    List<QuadtreeChunk> quadtrees,
    Vector3 cameraPosition,
    float viewDistance
  ) {
    results.Clear();

    // Iterate the quadtrees
    for (int i = 0; i < quadtrees.Count; i++) {
      QuadtreeChunk tree = quadtrees[i];

      // Iterate the nodes
      for (int j = 0; j < tree.nodes.Count; j++) {
        QuadtreeChunkNode node = tree.nodes[j];

        // Ignore if the node has children
        if (node.hasChildren) {
          continue;
        }

        // Ignore if the node is too far from the camera
        Vector3 closestPoint = node.bounds.ClosestPoint(cameraPosition);
        if (Vector3.Distance(closestPoint, cameraPosition) > viewDistance) {
          continue;
        }

        results.Add(node.bounds);
      }
    }
  }
}