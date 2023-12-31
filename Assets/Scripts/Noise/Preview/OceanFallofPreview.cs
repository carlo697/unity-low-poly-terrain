using UnityEngine;

public class OceanFallofPreview : NoisePreview {
  [Header("Falloff")]
  public AnimationCurve falloffCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

  public Vector2 falloffOffset = Vector2.zero;
  public Vector2 falloffScale = Vector2.one;
  public bool useFalloffOnly;
  public bool useOutputCurve;
  public AnimationCurve outputCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

  [Header("Ocean Border")]
  public bool useSeaBorder;
  public float seaBorderBeforeThreshold = 0.05f;
  public float seaBorderAfterThreshold = 0.1f;

  public override float[,] GenerateNoise() {
    var generator = noise.GetGenerator();

    // Generate heightmap
    float[,] heightmap = new float[resolution, resolution];
    for (int y = 0; y < resolution; y++) {
      for (int x = 0; x < resolution; x++) {
        // Normalized coordinates used to sample the noise
        float normalizedX = ((float)x / resolution + falloffOffset.x) * falloffScale.x;
        float normalizedY = ((float)y / resolution + falloffOffset.y) * falloffScale.y;

        // Clamped coordinates for creating the falloff map
        float posX = Mathf.Clamp01(normalizedX) * 2f - 1f;
        float posY = Mathf.Clamp01(normalizedY) * 2f - 1f;

        // Create the falloff map
        float falloff = 1f - (1f - posX * posX) * (1f - posY * posY);
        float curvedFalloff = 1f - falloffCurve.Evaluate(falloff);

        // Sample and normalize the noise
        float noise = generator.Generate3d(
          normalizedX + offset.x,
          normalizedY + offset.y,
          offset.z,
          0
        );
        noise = (noise + 1f) / 2f;

        // Combine the falloff map and the noise
        // float finalFalloff = noise - curvedFalloff;
        float finalFalloff = useFalloffOnly ? curvedFalloff : noise * curvedFalloff;
        if (useOutputCurve) {
          finalFalloff = outputCurve.Evaluate(finalFalloff);
        }

        // Draw sea border
        if (useSeaBorder) {
          float borderGradient;
          float start = threshold - seaBorderBeforeThreshold;
          float middle = threshold;
          if (finalFalloff <= start) {
            borderGradient = 0f;
          } else if (finalFalloff <= middle) {
            borderGradient = Mathf.SmoothStep(
              0f,
              1f,
              (finalFalloff - start) / (seaBorderBeforeThreshold)
            );
          } else {
            borderGradient = Mathf.SmoothStep(
              1f,
              0f,
              (finalFalloff - middle) / (seaBorderAfterThreshold)
            );
          }

          finalFalloff = borderGradient;
        }

        if (useThreshold) {
          heightmap[x, y] = finalFalloff >= threshold ? 1f : 0f;
        } else {
          heightmap[x, y] = finalFalloff;
        }
      }
    }

    return heightmap;
  }
}
