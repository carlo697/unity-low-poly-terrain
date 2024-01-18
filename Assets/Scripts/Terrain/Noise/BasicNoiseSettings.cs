using UnityEngine;

public class BasicNoiseSettings {
  public int seed = 0;
  public float scale = 1f;
  public float amplitude = 1f;
  public NoiseType noiseType = NoiseType.Simplex;

  [Header("Fractal")]
  public float gain = 0.5f;
  public float weightedStrength = 0f;
  public float lacunarity = 2f;
  public int octaves = 1;
  public FractalType fractalType = FractalType.FractalFBm;

  [Header("Curve")]
  public bool useCurve = false;
  public AnimationCurve curve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);

  [Header("Domain Warp")]
  public bool useDomainWarp = false;
  public float domainWarpAmplitude = 1f;
  public float domainWarpFrequency = 0.5f;
}
