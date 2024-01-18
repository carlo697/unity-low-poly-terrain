using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class TextureUtils {
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int GetIndexFrom2d(Vector2Int coords, Vector2Int size) {
    return GetIndexFrom2d(coords.x, coords.y, size.x);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int GetIndexFrom2d(int x, int y, int sizeX) {
    return y * sizeX + x;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int GetIndexFrom3d(Vector3Int coords, Vector3Int size) {
    return GetIndexFrom3d(coords.x, coords.y, coords.z, size.y, size.z);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int GetIndexFrom3d(int x, int y, int z, int sizeY, int sizeZ) {
    return z + y * sizeZ + x * sizeZ * sizeY;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static Vector3Int Get3dFromIndex(int index, Vector3Int size) {
    return Get3dFromIndex(index, size.x, size.y);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static Vector3Int Get3dFromIndex(int index, int sizeX, int sizeY) {
    int z = index % sizeX;
    int y = (index / sizeX) % sizeY;
    int x = index / (sizeY * sizeX);
    return new Vector3Int(x, y, z);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static Vector2Int Get2dFromIndex(int index, int sizeX) {
    int x = index % sizeX;
    int y = index / sizeX;
    return new Vector2Int(x, y);
  }

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

  public static float[] BoxBlur(float[] source, Vector2Int resolution, int blur) {
    float[] output = new float[resolution.x * resolution.y];

    for (int y = 0; y < resolution.y; y++) {
      for (int x = 0; x < resolution.x; x++) {
        int index2D = y * resolution.x + x;

        float value = 0f;
        int sampleCount = 0;

        for (int offsetZ = -blur; offsetZ < blur; offsetZ++) {
          for (int offsetX = -blur; offsetX < blur; offsetX++) {
            int sampleX = (x + offsetX);
            int sampleZ = (y + offsetZ);

            if (sampleX >= 0 && sampleX < resolution.x && sampleZ >= 0 && sampleZ < resolution.y) {
              int offsetIndex2D = sampleZ * resolution.x + sampleX;
              value += source[offsetIndex2D];
              sampleCount++;
            }
          }
        }

        output[index2D] = value / sampleCount;
      }
    }

    return output;
  }

  public static float[] ExtractSection(
    float[] sourceArray,
    Vector2Int offset,
    Vector2Int sourceResolution,
    Vector2Int outputResolution
  ) {
    float[] output = new float[outputResolution.x * outputResolution.y];

    for (int y = 0; y < outputResolution.y; y++) {
      for (int x = 0; x < outputResolution.x; x++) {
        int targetX = x + offset.x;
        int targetY = y + offset.y;

        int sourceIndex2D = targetY * sourceResolution.x + targetX;
        float source = sourceArray[sourceIndex2D];

        int outputIndex2D = y * outputResolution.x + x;
        output[outputIndex2D] = source;
      }
    }

    return output;
  }

  private static Vector2Int[] neighborOffsets = new Vector2Int[] {
    new Vector2Int(0, 1),
    new Vector2Int(0, -1),
    new Vector2Int(1, 0),
    new Vector2Int(-1, 0),
    //
    // new Vector2Int(-1, 1),
    // new Vector2Int(0, 1),
    // new Vector2Int(1, 1),
    // new Vector2Int(1, 0),
    // new Vector2Int(1, -1),
    // new Vector2Int(0, -1),
    // new Vector2Int(-1, -1),
    // new Vector2Int(-1, 0),
  };

  private static float[] neighborDistances = new float[] {
    1f,
    1f,
    1f,
    1f
    //
    // 1.414f,
    // 1f,
    // 1.414f,
    // 1f,
    // 1.414f,
    // 1f,
    // 1.414f,
    // 1f,
  };

  public static float[] FindDistances(
    float[] source,
    Vector2Int resolution,
    float distanceMultiplier = 1f
  ) {
    Queue<Vector2Int> frontier = new();
    float[] distances = new float[resolution.x * resolution.y];
    bool[] visited = new bool[resolution.x * resolution.y];

    // Find the pixels that belong to a biome
    for (int y = 0; y < resolution.y; y++) {
      for (int x = 0; x < resolution.x; x++) {
        int sourceIndex = y * resolution.x + x;
        float sourceValue = source[sourceIndex];

        if (sourceValue == 1f) {
          frontier.Enqueue(new Vector2Int(x, y));
          distances[sourceIndex] = 1f;
          visited[sourceIndex] = true;
        }
      }
    }

    // Expand outwards from existing points
    while (frontier.Count > 0) {
      Vector2Int currentCoords = frontier.Dequeue();
      int currentIndex = currentCoords.y * resolution.x + currentCoords.x;
      float currentDistance = Mathf.Max(distances[currentIndex], 0f);

      // Iterate over offsets to get neighbor
      for (int i = 0; i < neighborOffsets.Length; i++) {
        Vector2Int offset = neighborOffsets[i];

        // Get the coordinates and index of the neighbor
        Vector2Int neighborCoords = new Vector2Int(
          currentCoords.x + offset.x,
          currentCoords.y + offset.y
        );

        if (!(neighborCoords.x >= 0
          && neighborCoords.x < resolution.x
          && neighborCoords.y >= 0
          && neighborCoords.y < resolution.y)
        ) {
          continue;
        }

        int neighborIndex = neighborCoords.y * resolution.x + neighborCoords.x;

        // If the neighbor has not been visited
        if (visited[neighborIndex] == false) {
          float distance = neighborDistances[i] * distanceMultiplier;

          distances[neighborIndex] = currentDistance - distanceMultiplier;
          visited[neighborIndex] = true;
          frontier.Enqueue(new Vector2Int(neighborCoords.x, neighborCoords.y));
        }
      }
    }

    return distances;
  }
}
