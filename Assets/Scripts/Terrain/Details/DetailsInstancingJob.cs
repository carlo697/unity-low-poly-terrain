using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public struct DetailsInstancingJob : IJobParallelForBatch {
  public float shadowDistance;
  public float maxDistance;

  public Vector3 cameraPosition;
  [ReadOnly] public NativeArray<Plane> cameraPlanes;

  [ReadOnly] public GCHandle details;
  [ReadOnly] public GCHandle chunks;
  [ReadOnly] public GCHandle instancingBatches;
  [ReadOnly] public GCHandle instancingShadowBatches;

  public void Execute(int startIndex, int count) {
    var details = (Dictionary<int, Detail>)this.details.Target;
    var chunks = (List<DetailsChunk>)this.chunks.Target;
    var instancingBatches =
      (Dictionary<DetailSubmesh[], DetailsInstancingBatch>)this.instancingBatches.Target;
    var instancingShadowBatches =
      (Dictionary<DetailSubmesh[], DetailsInstancingBatch>)this.instancingShadowBatches.Target;

    // Iterate the instances in the chunk
    for (int i = startIndex; i < startIndex + count; i++) {
      DetailsChunk chunk = chunks[i];

      // Skip if the chunk don't have instances
      if (chunk.instances.Count == 0) {
        continue;
      }

      // Skip if the chunk is not visible
      bool isChunkVisible = chunk.bounds.Intersects(cameraPlanes);
      Vector3 nearestPoint = chunk.bounds.ClosestPoint(cameraPosition);
      bool isChunkShadowVisible = Vector3.Distance(nearestPoint, cameraPosition) < shadowDistance;
      if (!isChunkVisible && !isChunkShadowVisible) {
        continue;
      }

      for (int j = 0; j < chunk.instances.Count; j++) {
        DetailInstance instance = chunk.instances[j];
        Detail detail = details[instance.detailId];

        // Get the distance to the instance
        float distance = Vector3.Distance(instance.sphereBounds.center, cameraPosition);

        // Distance culling
        float maxAbsoluteDistance = detail.maxDistance * maxDistance;
        if (distance > maxAbsoluteDistance) {
          continue;
        }

        // Select the correct mesh for this detail
        DetailMeshSet meshSet = detail.meshes[instance.meshIndex];

        // Get LOD
        float normalizedDistance = distance / maxAbsoluteDistance;
        DetailSubmesh[] submeshes = null;
        for (int lod = meshSet.levelOfDetails.Length - 1; lod >= 0; lod--) {
          DetailMeshWithLOD meshWithLod = meshSet.levelOfDetails[lod];
          if (normalizedDistance > meshWithLod.distance) {
            submeshes = meshWithLod.submeshes;
            break;
          }
        }

        // Use frustum culling to add the matrix for the solid pass
        if (isChunkVisible) {
          bool isVisible = instance.sphereBounds.IntersectsExcludingFarPlane(cameraPlanes);
          if (isVisible) {
            DetailsInstancingBatch batch = instancingBatches[submeshes];
            batch.matrixConcurrentList.Add(instance.matrix);
          }
        }

        // Use distance culling to add the matrix for the shadow pass
        if (isChunkShadowVisible) {
          bool isShadowVisible = distance + instance.sphereBounds.radius < shadowDistance;
          if (isShadowVisible) {
            DetailsInstancingBatch batch = instancingShadowBatches[submeshes];
            if (batch.hasShadows) {
              batch.matrixConcurrentList.Add(instance.matrix);
            }
          }
        }
      }
    }
  }
}