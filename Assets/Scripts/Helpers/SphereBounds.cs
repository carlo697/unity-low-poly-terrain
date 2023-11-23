using UnityEngine;
using Unity.Collections;

public struct SphereBounds {
  public Vector3 center;
  public float radius;

  public SphereBounds(Vector4 sphere) {
    this.center = new Vector3(sphere.x, sphere.y, sphere.z);
    this.radius = sphere.z;
  }

  public SphereBounds(Vector3 center, float radius) {
    this.center = center;
    this.radius = radius;
  }

  public SphereBounds(Bounds bounds) {
    this.center = bounds.center;
    this.radius = Vector3.Distance(bounds.center, bounds.min);
  }

  public bool Intersects(NativeArray<Plane> planes) {
    for (int i = 0; i < 6; i++) {
      float distance = planes[i].GetDistanceToPoint(center);
      if (distance + radius < 0.0) {
        return false;
      }
    }

    return true;
  }

  public bool IntersectsExcludingFarPlane(NativeArray<Plane> planes) {
    for (int i = 0; i < 5; i++) {
      float distance = planes[i].GetDistanceToPoint(center);
      if (distance + radius < 0.0) {
        return false;
      }
    }

    return true;
  }
}