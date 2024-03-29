
using UnityEngine;
using System.Buffers;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Terrain/Detail Spawner", order = 2)]
public class DetailSpawnerAsset : DetailSpawner {
  [Header("Distribution")]
  public uint seed;
  public float populationDensity = 5f;
  public int maximumLevelOfDetail = 8;

  public override Detail detail { get { return m_detail; } }
  [Header("Detail")]
  [SerializeField] private Detail m_detail;

  [Header("Scale")]
  public AnimationCurve scaleCurve = new AnimationCurve(
    new Keyframe[] { new Keyframe(0f, 0.75f), new Keyframe(1f, 1f) }
  );

  [Header("Rotation")]
  public bool applyNormalRotation;
  public Vector3 randomRotation = new Vector3(0f, 360f, 0);

  [Header("Slope")]
  public bool useSlopeAngle;
  public float minAngle = -90f;
  public float maxAngle = 90f;

  [Header("Material")]
  public uint[] materials = new uint[] { 1 };
  public int[] biomes = new int[0];
  public bool useBiomes { get { return biomes.Length > 0; } }

  [Header("Noise")]
  public bool useNoise;
  public DetailSpawnerNoise noiseSettings;

  public override void Spawn(
    List<DetailInstance> instances,
    ulong seed,
    TerrainChunk terrainChunk,
    Bounds bounds,
    int integerLevelOfDetail,
    float normalizedLevelOfDetail
  ) {
    // Skip if the level of detail is over the limit for this detail
    if (integerLevelOfDetail > maximumLevelOfDetail) {
      return;
    }

    // Check if the chunk contains a biome for this detail
    if (useBiomes) {
      bool hasBiome = false;

      // Iterate the biomes to see if any of them is present in the chunk
      for (int biomeIndex = 0; biomeIndex < biomes.Length; biomeIndex++) {
        int biomeId = biomes[biomeIndex];

        if (terrainChunk.biomeIds.Contains(biomeId)) {
          hasBiome = true;
          break;
        }
      }

      if (!hasBiome) {
        return;
      }
    }

    // Build an array of the biomes present in this chunk that are compatible
    // with this detail
    int[] presentBiomeIds = null;
    int presentBiomesCount = 0;
    if (useBiomes) {
      presentBiomeIds = ArrayPool<int>.Shared.Rent(biomes.Length);

      // Iterate the compatible biomes
      for (int biomeIndex = 0; biomeIndex < biomes.Length; biomeIndex++) {
        int biomeId = biomes[biomeIndex];

        // Check if the biome is in the chunk to save it
        if (terrainChunk.biomeIds.Contains(biomeId)) {
          presentBiomeIds[presentBiomesCount] = biomeId;
          presentBiomesCount++;
        }
      }
    }

    // Random number generators
    XorshiftStar positionRng = new XorshiftStar(seed + this.seed);
    XorshiftStar lodRng = new XorshiftStar(seed + this.seed + 1);
    XorshiftStar noiseRng = new XorshiftStar(seed + this.seed + 3);
    XorshiftStar biomeMaskRng = new XorshiftStar(seed + this.seed + 4);

    // Noise generator
    DetailSpawnerNoise.Generator noise = useNoise ? noiseSettings.GetGenerator() : null;

    // Calculate how many details are inside the chunk
    float populationX = bounds.size.x / populationDensity + 1;
    float populationZ = bounds.size.z / populationDensity + 1;
    int totalPopulation = Mathf.RoundToInt(populationX * populationZ * normalizedLevelOfDetail);

    // Get the layer mask of the terrain
    int groundLayer = LayerMask.NameToLayer("Ground");
    int layerMask = 1 << groundLayer;

    Vector3 start = bounds.center - bounds.extents;

    for (int i = 0; i < totalPopulation; i++) {
      // To maintain a stable sequence of random position, we need to always generate
      // these variables even if the chunk won't have all the details
      Vector3 normalizedPosition = new Vector3(
        (float)positionRng.NextDouble(),
        1f,
        (float)positionRng.NextDouble()
      );
      Vector3 position = new Vector3(
        start.x + normalizedPosition.x * bounds.size.x,
        start.y + normalizedPosition.y * bounds.size.y,
        start.z + normalizedPosition.z * bounds.size.z
      );
      ulong instanceSeed = lodRng.Sample();

      // Check biomes at the position to decide if we skip it
      if (useBiomes) {
        // Calculate the sum of masks at the position
        float maskSum = 0;
        for (int biomeIndex = 0; biomeIndex < presentBiomesCount; biomeIndex++) {
          int biomeId = presentBiomeIds[biomeIndex];

          float maskValue = terrainChunk.GetValue2dAtNormalized2d(
            terrainChunk.biomeMasksById[biomeId],
            normalizedPosition.x,
            normalizedPosition.z
          );

          maskSum += maskValue;
        }

        // Skip if necessary
        maskSum = Mathf.Clamp01(maskSum);
        if (maskSum == 0f || biomeMaskRng.NextFloat() > maskSum) {
          continue;
        }
      }

      // Generate noise to decide if we skip the position
      if (useNoise) {
        float value = noise.Generate(
          position.x,
          position.z,
          (int)this.seed
        );

        if (noiseRng.NextDouble() > value) {
          continue;
        }
      }

      // Raycast
      RaycastHit hit;
      if (!Physics.Raycast(
        position,
        Vector3.down,
        out hit,
        bounds.size.y,
        layerMask,
        QueryTriggerInteraction.Ignore
      )) {
        continue;
      }

      TerrainChunk _otherChunk = hit.collider.GetComponent<TerrainChunk>();
      if (_otherChunk.gameObject != terrainChunk.gameObject) {
        Debug.LogWarning("Inconsistent chunk while placing details");
        Debug.Log(normalizedPosition.ToStringGeneral());
        Debug.Log(position.ToStringGeneral());

        Debug.Log(terrainChunk);
        Debug.Log(_otherChunk);

        Debug.Log(terrainChunk.transform.position.ToStringGeneral());
        Debug.Log(_otherChunk.transform.position.ToStringGeneral());

        Debug.Log(terrainChunk.transform.localScale.ToStringGeneral());
        Debug.Log(_otherChunk.transform.localScale.ToStringGeneral());

        Debug.Log(terrainChunk.gameObject);
        Debug.Log(_otherChunk.gameObject);

        Debug.Log(bounds);

        continue;
      }

      // Get the material on the hit point
      uint materialId = terrainChunk.triangleMaterials[hit.triangleIndex];
      bool wasMaterialFound = false;
      for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++) {
        if (materials[materialIndex] == materialId) {
          wasMaterialFound = true;
          break;
        }
      }

      // Skip this instance if the material is not valid
      if (!wasMaterialFound) {
        continue;
      }

      if (useSlopeAngle) {
        float slopeAngle = 90f - Vector3Extensions.SimplifiedAngle(Vector3.up, hit.normal);
        if (slopeAngle < minAngle || slopeAngle > maxAngle) {
          continue;
        }
      }

      XorshiftStar instanceRng = new XorshiftStar(instanceSeed);

      // Get the final position
      position = hit.point;

      // Generate rotation using the normal of the raycast hit and applying a random Y rotation
      Quaternion rotation;
      if (applyNormalRotation) {
        rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
      } else {
        rotation = Quaternion.identity;
      }
      rotation *= Quaternion.Euler(
        randomRotation.x > 0 ? (float)instanceRng.NextDouble() * randomRotation.x : 0f,
        randomRotation.y > 0 ? (float)instanceRng.NextDouble() * randomRotation.y : 0f,
        randomRotation.z > 0 ? (float)instanceRng.NextDouble() * randomRotation.z : 0f
      );

      // Generate a scale vector
      float scaleResult = scaleCurve.Evaluate((float)instanceRng.NextDouble());
      Vector3 scale = new Vector3(scaleResult, scaleResult, scaleResult);

      // Transformation matrix
      Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);

      // Select a mesh/prefab
      int meshIndex = instanceRng.Next(detail.meshes.Length);

      // Get the bounds of the mesh
      Bounds baseBounds = detail.meshes[meshIndex].levelOfDetails[0].submeshes[0].mesh.bounds;

      // Get the maximun axis in the scale vector
      float radiusScale = scale.x;
      if (scale.y > radiusScale) {
        radiusScale = scale.y;
      }
      if (scale.z > radiusScale) {
        radiusScale = scale.z;
      }

      // Create the final sphere bounds
      SphereBounds sphereBounds = new SphereBounds(
        matrix.MultiplyPoint(baseBounds.center),
        Vector3.Distance(baseBounds.center, baseBounds.min) * radiusScale
      );

      // Add a temporal instance to the list
      instances.Add(new DetailInstance {
        detailId = detail.id,
        meshIndex = meshIndex,
        position = position,
        rotation = rotation,
        scale = scale,
        matrix = matrix,
        sphereBounds = sphereBounds
      });
    }

    if (presentBiomeIds != null) {
      ArrayPool<int>.Shared.Return(presentBiomeIds);
    }
  }

  // public override (Vector3, Quaternion) GetFinalTransform(
  //   TempDetailInstance tempInstance,
  //   RaycastHit hit,
  //   TerrainChunk chunk
  // ) {
  //   return (tempInstance.raycastCommand);
  // }
}
