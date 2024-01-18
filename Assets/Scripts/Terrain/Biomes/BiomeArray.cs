using System;

[Serializable]
public class BiomeArray {
  [BiomeArray]
  public Biome[] array = new Biome[0];

  public static implicit operator Biome[](BiomeArray obj) {
    return obj.array;
  }
}