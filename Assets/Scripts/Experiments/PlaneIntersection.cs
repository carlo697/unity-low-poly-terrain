
using UnityEngine;


public class PlaneIntersection : MonoBehaviour {
  public float radius = 0.5f;
  public Transform rayObject;

  void OnDrawGizmos() {
    float distance;
    rayPlane(transform.position, transform.up, rayObject.position, rayObject.forward, out distance);

    Gizmos.color = Color.red;
    Gizmos.DrawSphere(rayObject.position + rayObject.forward * distance, 0.1f);
  }

  void rayPlane(Vector3 planeCenter, Vector3 planeNormal, Vector3 rayOrigin, Vector3 rayDir, out float distance) {
    if (rayOrigin.y <= planeCenter.y) {
      distance = 0f;
      return;
    }

    float denom = Vector3.Dot(planeNormal, rayDir);
    if (Mathf.Abs(denom) > 1e-6) {
      float t = Vector3.Dot(planeCenter - rayOrigin, planeNormal) / denom;
      if (t > 1e-6) {
        distance = t;
        return;
      }
    }

    distance = float.MaxValue;
  }
}
