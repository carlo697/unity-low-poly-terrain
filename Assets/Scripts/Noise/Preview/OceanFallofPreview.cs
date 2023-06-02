using UnityEngine;

public class OceanFallofPreview : NoisePreview {
  public Vector3 noiseOffset = Vector3.zero;
  public bool useOutputCurve;
  public AnimationCurve outputCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

  public override void Generate() {
    System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    watch.Start();

    InitializeNoises();

    // Generate heightmap
    float[,] heightmap = new float[resolution, resolution];
    for (int y = 0; y < resolution; y++) {
      for (int x = 0; x < resolution; x++) {
        float scale = frequency;

        float normalizedX = (float)x / resolution + offset.x;
        float normalizedY = (float)y / resolution + offset.y;

        float posX = Mathf.Clamp01(normalizedX) * 2f - 1f;
        float posY = Mathf.Clamp01(normalizedY) * 2f - 1f;

        float falloff = 1f - (1f - posX * posX) * (1f - posY * posY);
        float curvedFalloff = curve.Evaluate(falloff);

        float noise = (m_fastNoise2.GenSingle3D(
          normalizedX * scale + noiseOffset.x,
          normalizedY * scale + noiseOffset.y,
          0,
          seed
        ) + 1f) / 2f;

        // float finalFalloff = noise - curvedFalloff;
        float finalFalloff = noise * (1f - curvedFalloff);
        if (useOutputCurve)
          finalFalloff = outputCurve.Evaluate(finalFalloff);

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
