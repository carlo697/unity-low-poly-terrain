using UnityEngine;

public class NoisePreview : MonoBehaviour {
  [Header("Position")]
  public Vector3 offset;

  [Header("Noise")]
  public int resolution = 256;
  public FractalNoiseGenerator noise;

  [Header("Noise Output")]
  public bool useThreshold;
  public float threshold = 0.5f;

  [Header("Debug")]
  public bool debugTime;

  private void Start() {
    Generate();
  }

  public void AssignHeightmap(float[,] heightmap) {
    // Add a mesh renderer and assign material
    MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
    if (!meshRenderer) {
      meshRenderer = gameObject.AddComponent<MeshRenderer>();
    }

    // Generate material and assign texture
    Material material = meshRenderer.sharedMaterial;
    if (!material) {
      material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
    }
    material.SetTexture("_BaseMap", TextureUtils.GetTextureFromHeightmap(heightmap));
    meshRenderer.sharedMaterial = material;
  }

  public virtual float[,] GenerateNoise() {
    var generator = noise.GetGenerator();

    // Generate noise texture
    float[,] heightmap = new float[resolution, resolution];
    for (int y = 0; y < resolution; y++) {
      for (int x = 0; x < resolution; x++) {
        // Sample noise
        float value = generator.Generate3d(
          (float)x + offset.x,
          (float)y + offset.y,
          offset.z,
          0
        );

        // Normalize
        value = (value + 1f) / 2f;

        // Threshold
        if (useThreshold) {
          value = value >= threshold ? 1f : 0f;
        }

        // Save value
        heightmap[x, y] = value;
      }
    }

    return heightmap;
  }

  public void Generate() {
    System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    watch.Start();

    float[,] heightmap = GenerateNoise();

    watch.Stop();
    if (debugTime)
      Debug.Log(string.Format("Time: {0} ms", watch.ElapsedMilliseconds));

    AssignHeightmap(heightmap);
  }

  private void OnValidate() {
    Generate();
  }
}
