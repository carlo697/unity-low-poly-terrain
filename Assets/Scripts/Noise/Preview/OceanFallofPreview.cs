using UnityEngine;

public class OceanFallofPreview : NoisePreview {
  public Vector3 noiseOffset = Vector3.zero;

  public override void Generate() {
    System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    watch.Start();

    InitializeNoises();

    // Generate heightmap
    float[,] heightmap = new float[resolution, resolution];
    for (int y = 0; y < resolution; y++) {
      for (int x = 0; x < resolution; x++) {
        float scale = frequency;

        float normalizedX = Mathf.Clamp01((float)x / resolution + offset.x);
        float normalizedY = Mathf.Clamp01((float)y / resolution + offset.y);

        float posX = normalizedX * 2f - 1f;
        float posY = normalizedY * 2f - 1f;

        float falloff = 1f - (1f - posX * posX) * (1f - posY * posY);
        float curvedFalloff = curve.Evaluate(falloff);

        float noise = (m_fastNoise2.GenSingle3D(
          normalizedX * scale + noiseOffset.x,
          normalizedY * scale + noiseOffset.y,
          0,
          seed
        ) + 1f) / 2f;

        float finalFalloff = noise - curvedFalloff;

        if (middle) {
          heightmap[x, y] = finalFalloff >= 0.5f ? 1f : 0f;
        } else {
          heightmap[x, y] = finalFalloff;

        }
      }
    }

    watch.Stop();
    if (debugTime)
      Debug.Log(string.Format("Time: {0} ms", watch.ElapsedMilliseconds));

    // Add a mesh renderer and assign material
    AssignHeightmap(heightmap);
  }
}
