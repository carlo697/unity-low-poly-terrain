
using UnityEngine;

public struct FastNoiseChunk {
  public Vector3Int resolution;
  public Vector3 position;
  public Vector3 size;
  public float scale;

  public FastNoiseChunk(
    Vector3Int resolution,
    Vector3 position,
    Vector3 size,
    float scale = 1f
  ) {
    this.resolution = resolution;
    this.position = position;
    this.size = size;
    this.scale = scale;
  }

  public FastNoiseChunk(TerrainChunk chunk) {
    this.resolution = chunk.gridSize;
    this.position = chunk.noisePosition;
    this.size = chunk.size;
    this.scale = chunk.noiseSize;
  }

  public float[] GenerateGrid(
    bool is3D,
    FastNoise noise,
    int seed,
    float scale = 1f
  ) {
    // Variables needed to sample the point in world space
    float totalNoiseScale = this.scale * scale;
    Vector3 noiseFrequency = new Vector3(
      (32f / totalNoiseScale) * (1f / (resolution.x - 1)) * (size.x / 32f),
      (32f / totalNoiseScale) * (1f / (resolution.y - 1)) * (size.y / 32f),
      (32f / totalNoiseScale) * (1f / (resolution.z - 1)) * (size.z / 32f)
    );
    Vector3 offset = new Vector3(
      (position.x / size.x) * noiseFrequency.x * (resolution.x - 1),
      (position.y / size.y) * noiseFrequency.y * (resolution.y - 1),
      (position.z / size.z) * noiseFrequency.z * (resolution.z - 1)
    );

    // Apply offset to noise
    FastNoise offsetNode = new FastNoise("Domain Offset");
    offsetNode.Set("Source", noise);
    if (is3D) {
      offsetNode.Set("OffsetX", offset.z);
      offsetNode.Set("OffsetY", offset.y);
      offsetNode.Set("OffsetZ", offset.x);
    } else {
      offsetNode.Set("OffsetX", offset.x);
      offsetNode.Set("OffsetY", offset.z);
    }

    // Apply scale to noise
    FastNoise scaleNode = new FastNoise("Domain Axis Scale");
    scaleNode.Set("Source", offsetNode);
    if (is3D) {
      scaleNode.Set("ScaleX", noiseFrequency.z);
      scaleNode.Set("ScaleY", noiseFrequency.y);
      scaleNode.Set("ScaleZ", noiseFrequency.x);
    } else {
      scaleNode.Set("ScaleX", noiseFrequency.x);
      scaleNode.Set("ScaleY", noiseFrequency.z);
    }

    // Sample the pixels
    float[] pixels;
    if (is3D) {
      pixels = new float[resolution.x * resolution.y * resolution.z];
      scaleNode.GenUniformGrid3D(pixels, 0, 0, 0, resolution.x, resolution.y, resolution.z, 1f, seed);
    } else {
      pixels = new float[resolution.x * resolution.z];
      scaleNode.GenUniformGrid2D(pixels, 0, 0, resolution.x, resolution.z, 1f, seed);
    }

    return pixels;
  }
}