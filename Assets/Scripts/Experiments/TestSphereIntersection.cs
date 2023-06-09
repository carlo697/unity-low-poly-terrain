
using UnityEngine;


public class TestSphereIntersection : MonoBehaviour {
  public float radius = 0.5f;
  public Transform rayObject;

  void OnDrawGizmos() {
    float dstToSphere;
    float dstThroughSphere;
    raySphere(transform.position, radius, rayObject.position, rayObject.forward, out dstToSphere, out dstThroughSphere);

    Gizmos.color = Color.red;
    Gizmos.DrawSphere(rayObject.position + rayObject.forward * dstToSphere, 0.1f);
    Gizmos.DrawSphere(rayObject.position + rayObject.forward * (dstToSphere + dstThroughSphere), 0.1f);
  }

  void raySphere(Vector3 sphereCenter, float sphereRadius, Vector3 rayOrigin, Vector3 rayDir, out float dstToSphere, out float dstThroughSphere) {
    Vector3 offset = rayOrigin - sphereCenter;
    // float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
    float a = Vector3.Dot(rayDir, rayDir);
    float b = 2 * Vector3.Dot(offset, rayDir);
    float c = Vector3.Dot(offset, offset) - sphereRadius * sphereRadius;
    float d = b * b - 4 * a * c; // Discriminant from quadratic formula

    // Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
    if (d > 0) {
      float s = Mathf.Sqrt(d);
      float dstToSphereNear = Mathf.Max(0, (-b - s) / (2 * a));
      float dstToSphereFar = (-b + s) / (2 * a);

      // Ignore intersections that occur behind the ray
      if (dstToSphereFar >= 0) {
        // result = float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
        dstToSphere = dstToSphereNear;
        dstThroughSphere = dstToSphereFar - dstToSphereNear;
        return;
      }
    }

    // Ray did not intersect sphere
    dstToSphere = float.MaxValue;
    dstThroughSphere = 0;
  }
}
