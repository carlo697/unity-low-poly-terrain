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
    return bounds.ApplyTransform(transformation, new Vector3[8]);
  }

  public static Bounds ApplyTransform(this Bounds bounds, Matrix4x4 transformation, Vector3[] corners) {
    corners[0] = bounds.min;
    corners[1] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
    corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
    corners[3] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
    corners[4] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
    corners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
    corners[6] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
    corners[7] = bounds.max;

    Vector3 firstPoint = transformation.MultiplyPoint(bounds.center);
    Bounds newBounds = new Bounds(firstPoint, Vector3.zero);

    for (int i = 1; i < 8; i++) {
      Vector3 point = transformation.MultiplyPoint(corners[i]);
      newBounds.Encapsulate(point);
    }

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