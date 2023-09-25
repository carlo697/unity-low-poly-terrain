using System.Runtime.CompilerServices;

public static class MaterialBitConverter {
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static uint FloatToMaterialId(float value) {
    return UIntBitConverter.FloatToUIntBits(value) / 10u;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static float MaterialIdToFloat(uint value) {
    return UIntBitConverter.UIntBitsToFloat(value * 10u + 1u);
  }
}