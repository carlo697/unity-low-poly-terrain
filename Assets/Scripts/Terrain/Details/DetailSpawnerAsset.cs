
using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Terrain/Detail Spawner", order = 2)]
public class DetailSpawnerAsset : DetailSpawner {
  public uint seed;
  public float populationDensity = 5f;
  public AnimationCurve scaleCurve = new AnimationCurve(
    new Keyframe[] { new Keyframe(0f, 0.75f), new Keyframe(1f, 1f) }
  );

  public override Detail detail { get { return m_detail; } }
  [SerializeField] private Detail m_detail;

  public float minimumDistance;

  public override List<TempDetailInstance> Spawn(ulong seed, Bounds bounds, float levelOfDetail) {
    // Random number generators
    XorshiftStar positionRng = new XorshiftStar(seed + this.seed);
    XorshiftStar lodRng = new XorshiftStar(seed + this.seed + 1);

    // Calculate how many details are inside the chunk
    float populationX = bounds.size.x / populationDensity + 1;
    float populationZ = bounds.size.z / populationDensity + 1;
    int totalPopulation = Mathf.RoundToInt(populationX * populationZ);

    // Get the layer mask of the terrain
    int groundLayer = LayerMask.NameToLayer("Ground");
    int layerMask = 1 << groundLayer;

    // Create a list of temporal instances
    List<TempDetailInstance> results = new List<TempDetailInstance>(totalPopulation);
    Vector3 start = bounds.center - bounds.extents;
    for (int i = 0; i < totalPopulation; i++) {
      // To maintain a stable sequence of random numbers, we need to always generate
      // these variables even if the chunk won't have all the details
      Vector3 position = new Vector3(
        start.x + (float)positionRng.NextDouble() * bounds.size.x,
        start.y + bounds.size.y,
        start.z + (float)positionRng.NextDouble() * bounds.size.z
      );
      ulong instanceSeed = lodRng.Sample();

      // See if we need to skip this instance
      if ((float)lodRng.NextDouble() >= levelOfDetail) continue;

      // Create a raycast command
      QueryParameters parameters = QueryParameters.Default;
      parameters.hitBackfaces = true;
      parameters.layerMask = layerMask;
      RaycastCommand raycastCommand = new RaycastCommand(
        position,
        Vector3.down,
        parameters,
        1024
      );

      // Function to instance the final detail when the raycast is done
      Func<RaycastHit, DetailInstance?> GetFinalInstance = (RaycastHit hit) => {
        XorshiftStar instanceRng = new XorshiftStar(instanceSeed);

        Vector3 position = hit.point;

        // Generate rotation using the normal of the raycast hit and applying a random Y rotation
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        rotation *= Quaternion.Euler(0f, (float)instanceRng.NextDouble() * 360f, 0f);

        // Generate a scale vector
        float scaleResult = scaleCurve.Evaluate((float)instanceRng.NextDouble());
        Vector3 scale = new Vector3(scaleResult, scaleResult, scaleResult);

        // Select the prefab to use
        GameObject prefab = detail.prefabs[instanceRng.Next(detail.prefabs.Length)];

        return new DetailInstance {
          position = position,
          rotation = rotation,
          scale = scale,
          prefab = prefab,
          detail = detail,
          matrix = Matrix4x4.TRS(position, rotation, scale)
        };
      };

      // Add a temporal instance to the list
      results.Add(new TempDetailInstance {
        raycastCommand = raycastCommand,
        GetFinalInstance = GetFinalInstance
      });
    }

    return results;
  }

  // public override (Vector3, Quaternion) GetFinalTransform(
  //   TempDetailInstance tempInstance,
  //   RaycastHit hit,
  //   TerrainChunk chunk
  // ) {
  //   return (tempInstance.raycastCommand);
  // }
}
