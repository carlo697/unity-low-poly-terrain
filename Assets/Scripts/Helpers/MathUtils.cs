using System.Runtime.CompilerServices;

public static class MathUtils {
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static unsafe float FastInvSqrt(float x) {
    float xhalf = 0.5f * x;
    int i = *(int*)&x;
    i = 0x5f3759df - (i >> 1);
    x = *(float*)&i;
    x = x * (1.5f - xhalf * x * x);
    return x;
  }
}
