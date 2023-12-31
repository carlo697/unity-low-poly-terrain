
using UnityEngine;
using System;
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
  public uint[] materials = new uint[] { 0 };

  [Header("Noise")]
  public bool useNoise;
  public DetailSpawnerNoise noiseSettings;

  public override void Spawn(
    List<DetailInstance> instances,
    ulong seed,
    Bounds bounds,
    int integerLevelOfDetail,
    float normalizedLevelOfDetail
  ) {
    if (integerLevelOfDetail > maximumLevelOfDetail) {
      return;
    }

    // Random number generators
    XorshiftStar positionRng = new XorshiftStar(seed + this.seed);
    XorshiftStar lodRng = new XorshiftStar(seed + this.seed + 1);
    XorshiftStar noiseRng = new XorshiftStar(seed + this.seed + 3);

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
      Vector3 position = new Vector3(
        start.x + (float)positionRng.NextDouble() * bounds.size.x,
        start.y + bounds.size.y,
        start.z + (float)positionRng.NextDouble() * bounds.size.z
      );
      ulong instanceSeed = lodRng.Sample();

      // Generate noise
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
        1024f,
        layerMask,
        QueryTriggerInteraction.Ignore
      )) {
        continue;
      }

      // Get the material on the hit point
      uint materialId = MaterialBitConverter.FloatToMaterialId(hit.textureCoord.x);
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

      // Generate a bounds
      Bounds baseBounds = detail.meshes[meshIndex].levelOfDetails[0].submeshes[0].mesh.bounds;
      baseBounds = baseBounds.ApplyTransform(matrix);
      SphereBounds sphereBounds = new SphereBounds(baseBounds);

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
  }

  // public override (Vector3, Quaternion) GetFinalTransform(
  //   TempDetailInstance tempInstance,
  //   RaycastHit hit,
  //   TerrainChunk chunk
  // ) {
  //   return (tempInstance.raycastCommand);
  // }
}
