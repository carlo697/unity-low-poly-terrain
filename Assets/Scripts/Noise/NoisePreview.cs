using UnityEngine;

public class NoisePreview : MonoBehaviour {
  public Vector3 offset = Vector3.zero;
  public int resolution = 256;

  public int seed = 0;
  public float frequency = 0.05f;
  public float persistence = 0.5f;
  public int octaves = 3;
  public bool middle;
  public AnimationCurve curve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);

  public enum NoiseType { BuiltIn, FastNoise2D, FastNoise3D };
  public NoiseType type = NoiseType.BuiltIn;
  public bool debugTime;

  private MeshRenderer m_meshRenderer;

  private FastNoise m_fastNoise2;

  private void Start() {
    Generate();
  }

  private void InitializeNoises() {
    m_fastNoise2 = new FastNoise("FractalFBm");
    m_fastNoise2.Set("Source", new FastNoise("Simplex"));
    m_fastNoise2.Set("Gain", persistence);
    m_fastNoise2.Set("Lacunarity", 2f);
    m_fastNoise2.Set("Octaves", octaves);
  }

  public void Generate() {
    System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    watch.Start();

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
        } else if (type == NoiseType.FastNoise2D) {
          float value = curve.Evaluate(m_fastNoise2.GenSingle2D(
            (float)x * frequency + offset.x,
            (float)y * frequency + offset.y,
            seed
          ));

          heightmap[x, y] = (value + 1f) / 2f;
        } else if (type == NoiseType.FastNoise3D) {
          float value = curve.Evaluate(m_fastNoise2.GenSingle3D(
            (float)x * frequency + offset.x,
            (float)y * frequency + offset.y,
            offset.z,
            seed
          ));

          heightmap[x, y] = (value + 1f) / 2f;
        }

        if (middle) {
          heightmap[x, y] = heightmap[x, y] >= 0.5f ? 1f : 0f;
        }
      }
    }

    watch.Stop();
    if (debugTime)
      Debug.Log(string.Format("Time: {0} ms", watch.ElapsedMilliseconds));

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
      } else if (type == NoiseType.FastNoise2D) {
        finalValue = m_fastNoise2.GenSingle2D(
          coordX,
          coordY,
          0
        );
      } else if (type == NoiseType.FastNoise3D) {
        finalValue = m_fastNoise2.GenSingle3D(
          coordX,
          coordY,
          coordZ,
          0
        );
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
