

using UnityEngine;
using System.Runtime.CompilerServices;

public static class Vector3Extensions {
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static float SimplifiedAngle(Vector3 from, Vector3 to) {
    float dot = Mathf.Clamp(from.x * to.x + from.y * to.y + from.z * to.z, -1f, 1f);
    return Mathf.Acos(dot) * Mathf.Rad2Deg;
  }

  public static string ToStringGeneral(this Vector3 vector) {
    return $"({vector.x.ToString("G")}, {vector.y.ToString("G")}, {vector.z.ToString("G")})";
  }
}