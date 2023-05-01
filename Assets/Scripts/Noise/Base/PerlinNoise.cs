using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Source: https://www.youtube.com/watch?v=SoakEoUQ7Rg
// Source: https://github.com/motudev/3dperlinNoiseTut
// Author: https://github.com/motudev
public static class PerlinNoise {
  private static readonly int[] permutation = {
    151,160,137, 91, 90, 15,131, 13,201, 95, 96, 53,194,233,  7,225,
    140, 36,103, 30, 69,142,  8, 99, 37,240, 21, 10, 23,190,  6,148,
    247,120,234, 75,  0, 26,197, 62, 94,252,219,203,117, 35, 11, 32,
    57,177, 33, 88,237,149, 56, 87,174, 20,125,136,171,168, 68,175,
    74,165, 71,134,139, 48, 27,166, 77,146,158,231, 83,111,229,122,
    60,211,133,230,220,105, 92, 41, 55, 46,245, 40,244,102,143, 54,
    65, 25, 63,161,  1,216, 80, 73,209, 76,132,187,208, 89, 18,169,
    200,196,135,130,116,188,159, 86,164,100,109,198,173,186,  3, 64,
    52,217,226,250,124,123,  5,202, 38,147,118,126,255, 82, 85,212,
    207,206, 59,227, 47, 16, 58, 17,182,189, 28, 42,223,183,170,213,
    119,248,152,  2, 44,154,163, 70,221,153,101,155,167, 43,172,  9,
    129, 22, 39,253, 19, 98,108,110, 79,113,224,232,178,185,112,104,
    218,246, 97,228,251, 34,242,193,238,210,144, 12,191,179,162,241,
    81, 51,145,235,249, 14,239,107, 49,192,214, 31,181,199,106,157,
    184, 84,204,176,115,121, 50, 45,127,  4,150,254,138,236,205, 93,
    222,114, 67, 29, 24, 72,243,141,128,195, 78, 66,215, 61,156,180,
    151,160,137, 91, 90, 15,131, 13,201, 95, 96, 53,194,233,  7,225,
    140, 36,103, 30, 69,142,  8, 99, 37,240, 21, 10, 23,190,  6,148,
    247,120,234, 75,  0, 26,197, 62, 94,252,219,203,117, 35, 11, 32,
    57,177, 33, 88,237,149, 56, 87,174, 20,125,136,171,168, 68,175,
    74,165, 71,134,139, 48, 27,166, 77,146,158,231, 83,111,229,122,
    60,211,133,230,220,105, 92, 41, 55, 46,245, 40,244,102,143, 54,
    65, 25, 63,161,  1,216, 80, 73,209, 76,132,187,208, 89, 18,169,
    200,196,135,130,116,188,159, 86,164,100,109,198,173,186,  3, 64,
    52,217,226,250,124,123,  5,202, 38,147,118,126,255, 82, 85,212,
    207,206, 59,227, 47, 16, 58, 17,182,189, 28, 42,223,183,170,213,
    119,248,152,  2, 44,154,163, 70,221,153,101,155,167, 43,172,  9,
    129, 22, 39,253, 19, 98,108,110, 79,113,224,232,178,185,112,104,
    218,246, 97,228,251, 34,242,193,238,210,144, 12,191,179,162,241,
    81, 51,145,235,249, 14,239,107, 49,192,214, 31,181,199,106,157,
    184, 84,204,176,115,121, 50, 45,127,  4,150,254,138,236,205, 93,
    222,114, 67, 29, 24, 72,243,141,128,195, 78, 66,215, 61,156,180
  };

  private static readonly int permutationCount = 255;

  private static Vector3[] directions = {
    new Vector3( 1f, 1f, 0f),
    new Vector3(-1f, 1f, 0f),
    new Vector3( 1f,-1f, 0f),
    new Vector3(-1f,-1f, 0f),
    new Vector3( 1f, 0f, 1f),
    new Vector3(-1f, 0f, 1f),
    new Vector3( 1f, 0f,-1f),
    new Vector3(-1f, 0f,-1f),
    new Vector3( 0f, 1f, 1f),
    new Vector3( 0f,-1f, 1f),
    new Vector3( 0f, 1f,-1f),
    new Vector3( 0f,-1f,-1f),

    new Vector3( 1f, 1f, 0f),
    new Vector3(-1f, 1f, 0f),
    new Vector3( 0f,-1f, 1f),
    new Vector3( 0f,-1f,-1f)
  };

  private static readonly int directionCount = 15;

  // private static float scalar(Vector3 a, Vector3 b) {
  //   return a.x * b.x + a.y * b.y + a.z * b.z;
  // }

  private static float smoothDistance(float d) {
    return d * d * d * (d * (d * 6f - 15f) + 10f);
  }

  // Vector3 = 221 ms
  // Without scalar = 121 ms
  // Wihthout Mathf.Lerp = 75 ms
  public static float Sample(float x, float y, float z) {
    int flooredPointX0 = Mathf.FloorToInt(x);
    int flooredPointY0 = Mathf.FloorToInt(y);
    int flooredPointZ0 = Mathf.FloorToInt(z);

    float distanceX0 = x - flooredPointX0;
    float distanceY0 = y - flooredPointY0;
    float distanceZ0 = z - flooredPointZ0;

    float distanceX1 = distanceX0 - 1f;
    float distanceY1 = distanceY0 - 1f;
    float distanceZ1 = distanceZ0 - 1f;

    flooredPointX0 &= permutationCount;
    flooredPointY0 &= permutationCount;
    flooredPointZ0 &= permutationCount;

    int flooredPointX1 = flooredPointX0 + 1;
    int flooredPointY1 = flooredPointY0 + 1;
    int flooredPointZ1 = flooredPointZ0 + 1;

    int permutationX0 = permutation[flooredPointX0];
    int permutationX1 = permutation[flooredPointX1];

    int permutationY00 = permutation[permutationX0 + flooredPointY0];
    int permutationY10 = permutation[permutationX1 + flooredPointY0];
    int permutationY01 = permutation[permutationX0 + flooredPointY1];
    int permutationY11 = permutation[permutationX1 + flooredPointY1];

    Vector3 direction000 = directions[permutation[permutationY00 + flooredPointZ0] & directionCount];
    Vector3 direction100 = directions[permutation[permutationY10 + flooredPointZ0] & directionCount];
    Vector3 direction010 = directions[permutation[permutationY01 + flooredPointZ0] & directionCount];
    Vector3 direction110 = directions[permutation[permutationY11 + flooredPointZ0] & directionCount];
    Vector3 direction001 = directions[permutation[permutationY00 + flooredPointZ1] & directionCount];
    Vector3 direction101 = directions[permutation[permutationY10 + flooredPointZ1] & directionCount];
    Vector3 direction011 = directions[permutation[permutationY01 + flooredPointZ1] & directionCount];
    Vector3 direction111 = directions[permutation[permutationY11 + flooredPointZ1] & directionCount];

    // float value000 = scalar(direction000, new Vector3(distanceX0, distanceY0, distanceZ0));
    // float value100 = scalar(direction100, new Vector3(distanceX1, distanceY0, distanceZ0));
    // float value010 = scalar(direction010, new Vector3(distanceX0, distanceY1, distanceZ0));
    // float value110 = scalar(direction110, new Vector3(distanceX1, distanceY1, distanceZ0));
    // float value001 = scalar(direction001, new Vector3(distanceX0, distanceY0, distanceZ1));
    // float value101 = scalar(direction101, new Vector3(distanceX1, distanceY0, distanceZ1));
    // float value011 = scalar(direction011, new Vector3(distanceX0, distanceY1, distanceZ1));
    // float value111 = scalar(direction111, new Vector3(distanceX1, distanceY1, distanceZ1));

    float value000 = direction000.x * distanceX0 + direction000.y * distanceY0 + direction000.z * distanceZ0;
    float value100 = direction100.x * distanceX1 + direction100.y * distanceY0 + direction100.z * distanceZ0;
    float value010 = direction010.x * distanceX0 + direction010.y * distanceY1 + direction010.z * distanceZ0;
    float value110 = direction110.x * distanceX1 + direction110.y * distanceY1 + direction110.z * distanceZ0;
    float value001 = direction001.x * distanceX0 + direction001.y * distanceY0 + direction001.z * distanceZ1;
    float value101 = direction101.x * distanceX1 + direction101.y * distanceY0 + direction101.z * distanceZ1;
    float value011 = direction011.x * distanceX0 + direction011.y * distanceY1 + direction011.z * distanceZ1;
    float value111 = direction111.x * distanceX1 + direction111.y * distanceY1 + direction111.z * distanceZ1;


    float smoothDistanceX = smoothDistance(distanceX0);
    float smoothDistanceY = smoothDistance(distanceY0);
    float smoothDistanceZ = smoothDistance(distanceZ0);

    // return Mathf.Lerp(
    //   Mathf.Lerp(Mathf.Lerp(value000, value100, smoothDistanceX), Mathf.Lerp(value010, value110, smoothDistanceX), smoothDistanceY),
    //   Mathf.Lerp(Mathf.Lerp(value001, value101, smoothDistanceX), Mathf.Lerp(value011, value111, smoothDistanceX), smoothDistanceY),
    //   smoothDistanceZ
    // );

    float lerp1X = value000 + (value100 - value000) * smoothDistanceX;
    float lerp1Y = value010 + (value110 - value010) * smoothDistanceX;
    float lerpX = lerp1X + (lerp1Y - lerp1X) * smoothDistanceY;

    float lerp2X = value001 + (value101 - value001) * smoothDistanceX;
    float lerp2Y = value011 + (value111 - value011) * smoothDistanceX;
    float lerpY = lerp2X + (lerp2Y - lerp2X) * smoothDistanceY;

    return lerpX + (lerpY - lerpX) * smoothDistanceZ;
  }
}
