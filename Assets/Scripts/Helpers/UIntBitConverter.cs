using System.Runtime.CompilerServices;

public static class UIntBitConverter {
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static unsafe uint FloatToUIntBits(float value) {
    return *((uint*)&value);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static unsafe float UIntBitsToFloat(uint value) {
    return *((float*)&value);
  }
}