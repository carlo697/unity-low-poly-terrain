using UnityEngine;
using System.Runtime.CompilerServices;
using Unity.Collections;

public static class BoundsExtensions {
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool IsInside(this Bounds boundsA, Bounds boundsB) {
    return (boundsA.min.x < boundsB.max.x) && (boundsA.max.x > boundsB.min.x) &&
        (boundsA.min.y < boundsB.max.y) && (boundsA.max.y > boundsB.min.y) &&
        (boundsA.min.z < boundsB.max.z) && (boundsA.max.z > boundsB.min.z);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static Bounds ApplyTransform(this Bounds bounds, Matrix4x4 transformation) {
    Vector3 originalMin = bounds.min;
    Vector3 originalMax = bounds.max;

    Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
    Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

    // 0
    Vector3 transformed = transformation.MultiplyPoint(originalMin);
    min = Vector3.Min(min, transformed);
    max = Vector3.Max(max, transformed);

    // 1
    transformed = transformation.MultiplyPoint(new Vector3(originalMax.x, originalMin.y, originalMin.z));
    min = Vector3.Min(min, transformed);
    max = Vector3.Max(max, transformed);

    // 2
    transformed = transformation.MultiplyPoint(new Vector3(originalMax.x, originalMax.y, originalMin.z));
    min = Vector3.Min(min, transformed);
    max = Vector3.Max(max, transformed);

    // 3
    transformed = transformation.MultiplyPoint(new Vector3(originalMin.x, originalMax.y, originalMin.z));
    min = Vector3.Min(min, transformed);
    max = Vector3.Max(max, transformed);

    // 4
    transformed = transformation.MultiplyPoint(new Vector3(originalMin.x, originalMin.y, originalMax.z));
    min = Vector3.Min(min, transformed);
    max = Vector3.Max(max, transformed);

    // 5
    transformed = transformation.MultiplyPoint(new Vector3(originalMax.x, originalMin.y, originalMax.z));
    min = Vector3.Min(min, transformed);
    max = Vector3.Max(max, transformed);

    // 6
    transformed = transformation.MultiplyPoint(originalMax);
    min = Vector3.Min(min, transformed);
    max = Vector3.Max(max, transformed);

    // 7
    transformed = transformation.MultiplyPoint(new Vector3(originalMin.x, originalMax.y, originalMax.z));
    min = Vector3.Min(min, transformed);
    max = Vector3.Max(max, transformed);

    Bounds newBounds = new Bounds(bounds.center, Vector3.zero);
    newBounds.SetMinMax(min, max);

    return newBounds;
  }

  // Thanks to jaszunio15
  // Source: https://forum.unity.com/threads/managed-version-of-geometryutility-testplanesaabb.473575/#post-7838973
  public static bool Intersects(this Bounds bounds, NativeArray<Plane> planes) {
    for (int i = 0; i < planes.Length; i++) {
      Plane plane = planes[i];

      Vector3 normalSign = new Vector3(
        Mathf.Sign(plane.normal.x),
        Mathf.Sign(plane.normal.y),
        Mathf.Sign(plane.normal.z)
      );
      Vector3 testPoint = bounds.center + Vector3.Scale(bounds.extents, normalSign);

      float dot = Vector3.Dot(testPoint, plane.normal);
      if (dot + plane.distance < 0) {
        return false;
      }
    }

    return true;
  }
}