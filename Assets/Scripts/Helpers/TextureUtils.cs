using UnityEngine;
using System.Runtime.CompilerServices;

public static class TextureUtils {
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static float Normalize(float value) {
    return (value + 1f) * 0.5f;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static float Denormalize(float value) {
    return (value * 2f) - 1f;
  }

  public static Texture2D GetTextureFromColorMap(Color[] colorMap, int width, int height) {
    Texture2D texture = new Texture2D(width, height);

    texture.wrapMode = TextureWrapMode.Clamp;
    texture.SetPixels(colorMap);
    texture.Apply();

    return texture;
  }

  public static Texture2D GetTextureFromHeightmap(float[,] heightMap) {
    int width = heightMap.GetLength(0);
    int height = heightMap.GetLength(1);

    Color[] colorMap = new Color[width * height];
    for (int y = 0; y < height; y++) {
      for (int x = 0; x < width; x++) {
        colorMap[y * width + x] = Color.Lerp(Color.black, Color.white, heightMap[x, y]);
      }
    }

    return GetTextureFromColorMap(colorMap, width, height);
  }
}
