using Unity.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Terrain/Grass Spawner", order = 4)]
public class GrassSpawnerAsset : GrassSpawner {
  [Header("Distribution")]
  public uint seed;
  public float populationDensity = 0.1f;

  public override Grass grass { get { return m_grass; } }
  [Header("Grass")]
  [SerializeField] private Grass m_grass;

  [Header("Scale")]
  public bool useScaleCurve = true;
  public AnimationCurve scaleCurve = new AnimationCurve(
    new Keyframe[] { new Keyframe(0f, 0.75f), new Keyframe(1f, 1f) }
  );

  [Header("Material")]
  public uint[] materials = new uint[] { 1 };

  [Header("Noise")]
  public bool useHeightNoise;
  public DetailSpawnerNoise heightNoiseSettings;
  public float minHeightNoise = 0.5f;
  public float maxHeightNoise = 1f;
  private DetailSpawnerNoise.Generator m_instancedHeightNoise;

  public override void InitializeForGeneration() {
    if (useHeightNoise && m_instancedHeightNoise == null) {
      m_instancedHeightNoise = heightNoiseSettings.GetGenerator();
    }
  }

  public override int Spawn(
    NativeList<GrassInstance> instances,
    Vector3 chunkPosition,
    ulong seed,
    int vertexIndex,
    Vector3 a,
    Vector3 b,
    Vector3 c,
    float area,
    Vector3 unnormalizedNormal,
    uint materialId
  ) {
    // Check materials
    bool wasMaterialFound = false;
    for (int i = 0; i < materials.Length; i++) {
      if (materials[i] == materialId) {
        wasMaterialFound = true;
        break;
      }
    }

    // Skip this instance if the material is not valid
    if (!wasMaterialFound) {
      return 0;
    }

    XorshiftStar rng = new XorshiftStar(seed);
    float baseCount = (1f / populationDensity) * (1f / populationDensity);
    // float distance = 1f - (Mathf.Clamp(normalizedDistance, 0f, maxDistance) / maxDistance);
    float count = baseCount * area;

    int finalCount;
    if (count < 1f) {
      // If count is less than 1, we want to give it a chance to spawn
      finalCount = rng.NextDouble() < count ? 1 : 0;
    } else {
      finalCount = Mathf.FloorToInt(count);
    }

    // Create for loop
    for (int bladeIndex = 0; bladeIndex < finalCount; bladeIndex++) {
      // Get random point in triangle to spawn the blade
      float r1 = (float)rng.NextDouble();
      float r1Sqrt = Mathf.Sqrt(r1);
      float r2 = (float)rng.NextDouble();
      Vector3 triangleCenter = a * (1f - r1Sqrt) + b * r1Sqrt * (1f - r2) + c * (r1Sqrt * r2);

      // Get world position
      Vector3 position = chunkPosition + triangleCenter;

      // Generate random scale
      float scaleResult = useScaleCurve ? scaleCurve.Evaluate((float)rng.NextDouble()) : 1f;
      Vector3 scale = new Vector3(scaleResult, scaleResult, scaleResult);

      if (useHeightNoise) {
        float randomHeight = m_instancedHeightNoise.Generate(
          position.x,
          position.z,
          (int)this.seed
        );
        // To range 0-1
        randomHeight = (randomHeight + 1f) / 2f;
        // To range minHeightNoise-maxHeightNoise
        randomHeight = minHeightNoise + randomHeight * (maxHeightNoise - minHeightNoise);
        // Apply it to the scale
        scale.y *= randomHeight;
      }

      // Generate a random rotation
      Quaternion rotation = Quaternion.FromToRotation(Vector3.up, unnormalizedNormal);
      rotation *= Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

      // Add matrix
      Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
      // for (int submeshIndex = 0; submeshIndex < m_grass.submeshes.Length; submeshIndex++) {
      //   DetailSubmesh submesh = m_grass.submeshes[submeshIndex];

      //   // Get the list of matrices or create it if necessary
      //   List<Matrix4x4> lists;
      //   if (!groups.TryGetValue(submesh, out lists)) {
      //     lists = groups[submesh] = new List<Matrix4x4>();
      //   }

      //   // Add the matrix
      //   lists.Add(matrix);
      // }
      instances.Add(new GrassInstance {
        grassId = grass.id,
        meshIndex = rng.Next(grass.meshes.Length),
        matrix = matrix
      });
    }

    return finalCount;
  }
}
