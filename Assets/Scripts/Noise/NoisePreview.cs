using UnityEngine;

public class NoisePreview : MonoBehaviour {
  public Vector3 offset = Vector3.zero;
  public int resolution = 256;

  public int seed = 0;
  public float frequency = 0.05f;
  public float amplitude = 1f;
  public float persistence = 0.5f;
  public int octaves = 3;

  public enum NoiseType { BuiltIn, Simple3D, Advanced3D };
  public NoiseType type = NoiseType.BuiltIn;

  private MeshRenderer m_meshRenderer;

  private void Start() {
    Generate();
  }

  public void Generate() {
    BasicNoise basicNoise = new BasicNoise(
      frequency,
      amplitude,
      seed
    );

    FractalNoise fractalNoise = new FractalNoise(
      frequency,
      amplitude,
      persistence,
      octaves,
      seed
    );

    // Generate heightmap
    float[,] heightmap = new float[resolution, resolution];
    for (int y = 0; y < resolution; y++) {
      for (int x = 0; x < resolution; x++) {
        if (type == NoiseType.BuiltIn) {
          heightmap[x, y] = Mathf.PerlinNoise(
            (float)x * frequency + offset.x,
            (float)y * frequency + offset.y
          );
        } else if (type == NoiseType.Simple3D) {
          float value = basicNoise.Sample(
            (float)x + offset.x,
            (float)y + offset.y,
            offset.z
          );

          heightmap[x, y] = (value + 1f) / 2f;
        } else {
          float value = fractalNoise.Sample(
            (float)x + offset.x,
            (float)y + offset.y,
            offset.z
          );

          heightmap[x, y] = (value + 1f) / 2f;
        }
      }
    }

    // Generate material and assign texture
    Material newMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
    newMaterial.SetTexture("_BaseMap", TextureGenerator.GetTextureFromHeightmap(heightmap));

    // Add a mesh renderer and assign material
    m_meshRenderer = GetComponent<MeshRenderer>();
    if (!m_meshRenderer) {
      m_meshRenderer = gameObject.AddComponent<MeshRenderer>();
    }
    m_meshRenderer.sharedMaterial = newMaterial;
  }

  private void OnValidate() {
    Generate();
  }
}
