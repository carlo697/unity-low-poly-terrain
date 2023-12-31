using UnityEngine;
using System.Runtime.CompilerServices;

// Source: https://en.wikipedia.org/wiki/Xorshift#xorshift*
public struct XorshiftStar {
  private ulong x;
  private const double doubleNormalizer = (1.0d / ulong.MaxValue);
  private const float floatNormalizer = (1.0f / ulong.MaxValue);

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
    return Sample() * doubleNormalizer;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public float NextFloat() {
    return Sample() * floatNormalizer;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public float NextFloat(float max) {
    return Sample() * floatNormalizer * max;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public float NextFloat(float min, float max) {
    float value = Sample() * floatNormalizer;
    return min + value * (max - min);
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
