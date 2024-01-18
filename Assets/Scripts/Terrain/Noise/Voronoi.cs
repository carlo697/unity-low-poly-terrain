using Unity.Mathematics;
using System.Runtime.CompilerServices;

// Thanks to:
// https://www.ronja-tutorials.com/post/028-voronoi-noise/
// https://cyangamedev.wordpress.com/2019/07/16/voronoi/
public static class Voronoi {
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static float Random2dTo1d(float2 value) {
    return new XorshiftStar((ulong)value.x.GetHashCode() ^ ((ulong)value.y.GetHashCode() << 2)).NextFloat();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static float2 Random2dTo2d(float2 value) {
    var rng = new XorshiftStar((ulong)value.x.GetHashCode() ^ ((ulong)value.y.GetHashCode() << 2));
    return new float2(rng.NextFloat(), rng.NextFloat());
  }

  public struct Values {
    public float distance;
    public float id;
    public float distanceToEdge;

    public Values(float distance, float id, float distanceToEdge) {
      this.distance = distance;
      this.id = id;
      this.distanceToEdge = distanceToEdge;
    }
  }

  public static Values VoronoiWithEdgeDistanceAt(float2 position, float randomness = 1f) {
    float2 baseCell = math.floor(position);

    float minimumDistance = float.MaxValue;
    float2 toClosestCell = default;
    float2 closestCell = default;

    for (int y = -1; y <= 1; y++) {
      for (int x = -1; x <= 1; x++) {
        float2 cell = baseCell + new float2(x, y);
        float2 cellPosition = cell + Random2dTo2d(cell) * randomness;

        float2 toCell = cellPosition - position;
        float distanceToCell = math.length(toCell);

        if (distanceToCell < minimumDistance) {
          minimumDistance = distanceToCell;
          closestCell = cell;
          toClosestCell = toCell;
        }
      }
    }

    float minimumEdgeDistance = float.MaxValue;
    for (int y = -1; y <= 1; y++) {
      for (int x = -1; x <= 1; x++) {
        float2 cell = baseCell + new float2(x, y);
        float2 cellPosition = cell + Random2dTo2d(cell) * randomness;

        float2 toCell = cellPosition - position;

        if (!cell.Equals(closestCell)) {
          float2 toCenter = (toClosestCell + toCell) * 0.5f;
          float2 cellDifference = math.normalize(toCell - toClosestCell);

          float edgeDistance = math.dot(toCenter, cellDifference);
          minimumEdgeDistance = math.min(minimumEdgeDistance, edgeDistance);
        }
      }
    }

    float cellId = Random2dTo1d(closestCell);
    return new Values(minimumDistance, cellId, minimumEdgeDistance);
  }

  public struct EdgeDistance {
    public float2 cell;
    public float id;
    public float distanceToEdge;

    public EdgeDistance(float2 cell, float id, float distanceToEdge) {
      this.cell = cell;
      this.id = id;
      this.distanceToEdge = distanceToEdge;
    }
  }

  public static void VoronoiEdgeDistancesAt(EdgeDistance[] result, float2 position, float randomness = 1f) {
    float2 initialCell = math.floor(position);
    int cellIndex = 0;

    for (int baseY = -1; baseY <= 1; baseY++) {
      for (int baseX = -1; baseX <= 1; baseX++) {
        float2 baseCell = initialCell + new float2(baseX, baseY);
        float2 baseCellPosition = baseCell + Random2dTo2d(baseCell) * randomness;
        float2 toBaseCell = baseCellPosition - position;

        float minimumEdgeDistance = float.MaxValue;
        for (int y = -1; y <= 1; y++) {
          for (int x = -1; x <= 1; x++) {
            float2 cell = baseCell + new float2(x, y);
            float2 cellPosition = cell + Random2dTo2d(cell) * randomness;
            float2 toCell = cellPosition - position;

            if (!cell.Equals(baseCell)) {
              float2 toCenter = (toBaseCell + toCell) * 0.5f;
              float2 cellDifference = math.normalize(toCell - toBaseCell);

              float edgeDistance = math.dot(toCenter, cellDifference);
              minimumEdgeDistance = math.min(minimumEdgeDistance, edgeDistance);
            }
          }
        }

        result[cellIndex] = new EdgeDistance(baseCell, Random2dTo1d(baseCell), minimumEdgeDistance);
        cellIndex++;
      }
    }
  }
}