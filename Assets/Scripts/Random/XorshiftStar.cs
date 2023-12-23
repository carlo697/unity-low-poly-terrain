using UnityEngine;
using System.Runtime.CompilerServices;

// Source: https://en.wikipedia.org/wiki/Xorshift#xorshift*
public struct XorshiftStar {
  private ulong x;

  public XorshiftStar(ulong seed) {
    this.x = seed;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ulong Sample() {
    x ^= x >> 12;
    x ^= x << 25;
    x ^= x >> 27;
    return x * 0x2545F4914F6CDD1D;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public int Next(int maxValue) {
    return (int)(NextDouble() * maxValue);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public double NextDouble() {
    const double multiplier = (1.0 / ulong.MaxValue);
    return Sample() * multiplier;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public float NextFloat() {
    const float multiplier = (1.0f / ulong.MaxValue);
    return Sample() * multiplier;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Vector3 NextVector3() {
    return new Vector3(
      NextFloat() * 2f - 1f,
      NextFloat() * 2f - 1f,
      NextFloat() * 2f - 1f
    ).normalized;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Vector3 NextFlatVector3() {
    return new Vector3(
      NextFloat() * 2f - 1f,
      0f,
      NextFloat() * 2f - 1f
    ).normalized;
  }
}
