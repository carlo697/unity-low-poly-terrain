// Source: https://en.wikipedia.org/wiki/Xorshift#xorshift*
public struct XorshiftStar {
  private ulong x;

  public XorshiftStar(ulong seed) {
    this.x = seed;
  }

  public ulong Sample() {
    x ^= x >> 12;
    x ^= x << 25;
    x ^= x >> 27;
    return x * 0x2545F4914F6CDD1D;
  }

  public int Next(int maxValue) {
    return (int)(NextDouble() * maxValue);
  }

  public double NextDouble() {
    const double multiplier = (1.0 / ulong.MaxValue);
    return Sample() * multiplier;
  }
}
