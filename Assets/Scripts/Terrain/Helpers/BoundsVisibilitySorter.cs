using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;

public enum VisibilitySortMode {
  Farthest,
  Nearest,
  NearestAndInsideFrustum
}

public static class BoundsVisibilitySorter {
  private static BoundsVisibilityComparer comparer = new BoundsVisibilityComparer();

  public static void Sort(List<Bounds> list, Camera camera, VisibilitySortMode mode) {
    Vector3 cameraPosition = camera.transform.position;
    Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
    comparer.mode = mode;

    // Request a temporary list of BoundsVisibility to fill it and sort it
    using (var obj = ListPool<BoundsVisibility>.Get(out var tempList)) {
      // Fill the temporary list with the bounds in the original list
      tempList.Clear();
      for (int i = 0; i < list.Count; i++) {
        tempList.Add(
          new BoundsVisibility(list[i], cameraPosition, cameraPlanes)
        );
      }

      // Sort the temporary list
      tempList.Sort(comparer);

      // Get the final list
      list.Clear();
      for (int i = 0; i < tempList.Count; i++) {
        list.Add(tempList[i].bounds);
      }
    }
  }
}

public struct BoundsVisibility {
  public Bounds bounds;
  public float sqrDistanceToCamera;
  public bool isInsideFrustum;

  public BoundsVisibility(
    Bounds bounds,
    Vector3 cameraPosition,
    Plane[] frustumPlanes
  ) {
    this.bounds = bounds;
    this.sqrDistanceToCamera =
      (bounds.center.x - cameraPosition.x) * (bounds.center.x - cameraPosition.x)
      + (bounds.center.z - cameraPosition.z) * (bounds.center.z - cameraPosition.z);
    this.isInsideFrustum = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
  }
}

public class BoundsVisibilityComparer : Comparer<BoundsVisibility> {
  public VisibilitySortMode mode;

  public override int Compare(BoundsVisibility a, BoundsVisibility b) {
    switch (mode) {
      case VisibilitySortMode.NearestAndInsideFrustum:
        if (a.isInsideFrustum != b.isInsideFrustum) {
          return b.isInsideFrustum.CompareTo(a.isInsideFrustum);
        }

        return a.sqrDistanceToCamera.CompareTo(b.sqrDistanceToCamera);
      case VisibilitySortMode.Nearest:
        return a.sqrDistanceToCamera.CompareTo(b.sqrDistanceToCamera);
      default:
        return b.sqrDistanceToCamera.CompareTo(a.sqrDistanceToCamera);
    }
  }
}