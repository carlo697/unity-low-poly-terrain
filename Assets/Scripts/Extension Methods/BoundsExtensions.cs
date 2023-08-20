using UnityEngine;
using System.Runtime.CompilerServices;

public static class BoundsExtensions {
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool IsInside(this Bounds boundsA, Bounds boundsB) {
    return (boundsA.min.x < boundsB.max.x) && (boundsA.max.x > boundsB.min.x) &&
        (boundsA.min.y < boundsB.max.y) && (boundsA.max.y > boundsB.min.y) &&
        (boundsA.min.z < boundsB.max.z) && (boundsA.max.z > boundsB.min.z);
  }
}