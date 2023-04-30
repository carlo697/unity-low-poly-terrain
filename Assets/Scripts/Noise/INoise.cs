using UnityEngine;

public interface INoise {
  void SetSeed(int seed);
  float Sample(float x, float y, float z);
}