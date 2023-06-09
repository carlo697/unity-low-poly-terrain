static const float maxFloat = 3.402823466e+38;

// Returns vector (dstToSphere, dstThroughSphere)
// If ray origin is inside sphere, dstToSphere = 0
// If ray misses sphere, dstToSphere = maxValue; dstThroughSphere = 0
void raySphere_float(float3 sphereCenter, float sphereRadius, float3 rayOrigin, float3 rayDir, out float dstToSphere, out float dstThroughSphere) {
  float3 offset = rayOrigin - sphereCenter;
  // float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
  float a = dot(rayDir, rayDir);
  float b = 2 * dot(offset, rayDir);
  float c = dot (offset, offset) - sphereRadius * sphereRadius;
  float d = b * b - 4 * a * c; // Discriminant from quadratic formula

  // Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
  if (d > 0) {
    float s = sqrt(d);
    float dstToSphereNear = max(0, (-b - s) / (2 * a));
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
  // result = float2(3.402823466e+38, 0);
  dstToSphere = maxFloat;
  dstThroughSphere = 0;
}

// void rayPlane_float(float3 planeCenter, float3 planeNormal, float3 rayOrigin, float3 rayDir, out float distance) {
//   float denom = dot(planeNormal, rayDir);
//   if (abs(denom) > 1e-6) {
//       float3 p0l0 = planeCenter - rayOrigin;
//       distance = dot(p0l0, planeNormal) / denom; 
//       return;
//   }

//   distance = maxFloat;
// }

void rayPlane_float(float3 planeCenter, float3 planeNormal, float3 rayOrigin, float3 rayDir, out float distance) {
  if (rayOrigin.y <= planeCenter.y) {
    distance = 0;
    return;
  }

  float denom = dot(planeNormal, rayDir);
  if (abs(denom) > 1e-6) {
      float t = dot(planeCenter - rayOrigin, planeNormal) / denom;
      if (t > 1e-6) {
        distance = t;
        return;
      }
  }

  distance = maxFloat;
}
