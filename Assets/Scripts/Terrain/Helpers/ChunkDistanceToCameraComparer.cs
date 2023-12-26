using UnityEngine;
using System.Collections.Generic;

public struct ChunkDistanceToCameraComparer : IComparer<Bounds> {
  public Vector3 cameraPosition;
  public Plane[] cameraPlanes;

  public ChunkDistanceToCameraComparer(Camera camera) {
    this.cameraPosition = camera.transform.position;
    this.cameraPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
  }

  public int Compare(Bounds a, Bounds b) {
    bool isAInside = GeometryUtility.TestPlanesAABB(cameraPlanes, a);
    bool isBInside = GeometryUtility.TestPlanesAABB(cameraPlanes, b);

    if (isAInside != isBInside) {
      return isBInside.CompareTo(isAInside);
    }

    float distanceA =
      (a.center.x - cameraPosition.x) * (a.center.x - cameraPosition.x)
      + (a.center.z - cameraPosition.z) * (a.center.z - cameraPosition.z);

    float distanceB =
      (b.center.x - cameraPosition.x) * (b.center.x - cameraPosition.x)
      + (b.center.z - cameraPosition.z) * (b.center.z - cameraPosition.z);

    return distanceA.CompareTo(distanceB);
  }
}