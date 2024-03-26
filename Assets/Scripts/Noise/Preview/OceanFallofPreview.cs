using UnityEngine;

public class OceanFallofPreview : NoisePreview {
  [Header("Falloff")]
  public AnimationCurve falloffCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

  public Vector2 falloffOffset = Vector2.zero;
  public Vector2 falloffScale = Vector2.one;
  public bool useFalloffOnly;

  [Header("Ocean Border")]
  public bool displayLandGradient;
  public bool displayOceanGradient;
  public GradientSteepMode gradientSteepMode;
  public float fixedGradientSteep = 0.15f;
  public NoiseTreeGenerator gradientSteepNoise;

  public enum GradientSteepMode {
    Fixed,
    Noise
  }

  public override float[,] GenerateNoise() {
    var generator = noise.GetGenerator();
    var steepGenerator = gradientSteepNoise.GetGenerator();

    // Generate heightmap
    float[,] heightmap = new float[resolution, resolution];
    for (int y = 0; y < resolution; y++) {
      for (int x = 0; x < resolution; x++) {
        // Normalized coordinates used to sample the noise
        float normalizedX = ((float)x / resolution + falloffOffset.x) * falloffScale.x;
        float normalizedY = ((float)y / resolution + falloffOffset.y) * falloffScale.y;

        // Sample and normalize the noise
        float noise = generator.Generate3d(
          normalizedX + offset.x,
          normalizedY + offset.y,
          offset.z,
          1f,
          0
        );

        // Clamped coordinates for creating the falloff map
        float posX = Mathf.Clamp01(normalizedX) * 2f - 1f;
        float posY = Mathf.Clamp01(normalizedY) * 2f - 1f;

        // Create the falloff map
        float falloff = 1f - (1f - posX * posX) * (1f - posY * posY);
        falloff = 1f - falloffCurve.Evaluate(falloff);

        // Combine the falloff map and the noise
        // float finalFalloff = noise - curvedFalloff;
        float finalOutput = noise * falloff;
        if (useFalloffOnly) {
          finalOutput = falloff;
        }

        if (displayLandGradient || displayOceanGradient) {
          float seaLevel = threshold;

          float steepness;
          if (gradientSteepMode == GradientSteepMode.Noise && steepGenerator != null) {
            steepness = steepGenerator.Generate3d(
              normalizedX + offset.x,
              normalizedY + offset.y,
              offset.z,
              1f,
              0
            );
          } else {
            steepness = fixedGradientSteep;
          }

          float gradient = 0f;
          if (displayLandGradient) {
            float landGradient;
            if (finalOutput <= seaLevel) {
              landGradient = 0f;
            } else {
              landGradient = Mathf.SmoothStep(0f, 1f, (finalOutput - seaLevel) / steepness);
            }

            gradient = landGradient;
          }

          if (displayOceanGradient) {
            float oceanGradient;
            if (finalOutput > seaLevel) {
              oceanGradient = 0f;
            } else {
              oceanGradient = Mathf.SmoothStep(0f, 1f, ((seaLevel - finalOutput) / steepness));
            }

            gradient += oceanGradient;
          }

          finalOutput = gradient;
        }

        if (useThreshold) {
          heightmap[x, y] = finalOutput >= threshold ? 1f : 0f;
        } else {
          heightmap[x, y] = finalOutput;
        }
      }
    }

    return heightmap;
  }
}
