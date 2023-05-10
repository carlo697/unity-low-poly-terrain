using UnityEngine;

public class NoisePreview : MonoBehaviour {
  public Vector3 offset = Vector3.zero;
  public int resolution = 256;

  public int seed = 0;
  public float frequency = 0.05f;
  public float amplitude = 1f;
  public float persistence = 0.5f;
  public int octaves = 3;
  public AnimationCurve curve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);

  public enum NoiseType { BuiltIn, Simple3D, Advanced3D };
  public NoiseType type = NoiseType.BuiltIn;

  private MeshRenderer m_meshRenderer;

  private BasicNoise m_basicNoise;
  private FractalNoise m_fractalNoise;

  private void Start() {
    Generate();
  }

  private void InitializeNoises() {
    m_basicNoise = new BasicNoise(
      frequency,
      amplitude,
      seed
    );

    m_fractalNoise = new FractalNoise(
      frequency,
      amplitude,
      persistence,
      octaves,
      seed
    );
  }

  public void Generate() {
    InitializeNoises();

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
          float value = curve.Evaluate(m_basicNoise.Sample(
            (float)x + offset.x,
            (float)y + offset.y,
            offset.z
          ));

          heightmap[x, y] = (value + 1f) / 2f;
        } else {
          float value = curve.Evaluate(m_fractalNoise.Sample(
            (float)x + offset.x,
            (float)y + offset.y,
            offset.z
          ));

          heightmap[x, y] = (value + 1f) / 2f;
        }
      }
    }

    // Add a mesh renderer and assign material
    m_meshRenderer = GetComponent<MeshRenderer>();
    if (!m_meshRenderer) {
      m_meshRenderer = gameObject.AddComponent<MeshRenderer>();
    }

    // Generate material and assign texture
    Material material = m_meshRenderer.sharedMaterial;
    if (!material) {
      material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
    }
    material.SetTexture("_BaseMap", TextureGenerator.GetTextureFromHeightmap(heightmap));
    m_meshRenderer.sharedMaterial = material;
  }

  private void OnValidate() {
    Generate();
  }

  [ContextMenu("Print Min And Max")]
  private void PrintMinAndMax() {
    InitializeNoises();

    // Min and max values
    float min = float.MaxValue;
    float max = float.MinValue;
    float median = 0;

    // Generate heightmap
    int samples = 50000000;
    for (int i = 0; i < samples; i++) {
      float finalValue = 0f;

      float coordX = Random.Range(-10000000f, 10000000f);
      float coordY = Random.Range(-10000000f, 10000000f);
      float coordZ = Random.Range(-10000000f, 10000000f);

      if (type == NoiseType.BuiltIn) {
        finalValue = Mathf.PerlinNoise(coordX, coordY);
      } else if (type == NoiseType.Simple3D) {
        finalValue = m_basicNoise.Sample(coordX, coordY, coordZ);
      } else {
        finalValue = m_fractalNoise.Sample(coordX, coordY, coordZ);
      }

      median += finalValue / samples;
      if (finalValue > max) {
        max = finalValue;
      } else if (finalValue < min) {
        min = finalValue;
      }
    }

    // Debug the min and max values
    Debug.Log(string.Format("min: {0}, max: {1}, median: {2}", min, max, median));
  }
}
