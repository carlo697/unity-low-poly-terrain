using UnityEngine;
using System;
using System.Collections.Generic;

public class OldGrassChunk : MonoBehaviour {
  public static DateTime lastUpdatedAt = DateTime.Now;

  public Material material;

  private TerrainChunk m_terrainChunk;
  private Mesh m_mesh;
  private int m_levelOfDetail;
  // private Vector3 m_lastPositionAtUpdate;
  private Vector3 m_cameraPosition;
  private float m_chunkDistanceToCamera;
  private float m_lastChunkDistanceToCamera;

  public const float maxDistance = 100f;

  private void Awake() {
    m_terrainChunk = GetComponent<TerrainChunk>();

    // Calculate level of detail
    m_levelOfDetail = 1;
    if (m_terrainChunk.terrainManager) {
      m_levelOfDetail = 1 + (int)Mathf.Log(
        m_terrainChunk.size.x / m_terrainChunk.terrainManager.chunkSize.x,
        2
      );
    }
  }

  private void Start() {
    if (m_levelOfDetail > 1) return;

    // Create mesh
    m_mesh = new Mesh();
    m_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

    // Create object and add mesh
    GameObject obj = new GameObject("grass");
    obj.transform.SetParent(transform, false);
    MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
    meshFilter.sharedMesh = m_mesh;

    MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
    meshRenderer.sharedMaterial = material;
    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
  }

  private void Update() {
    // Don't update if the chunk has not been generated
    if (m_levelOfDetail > 1 || m_terrainChunk.status != TerrainChunkStatus.Generated) return;

    // Calculate position to camera
    Camera camera = Camera.main;
    m_cameraPosition = camera.transform.position;
    m_chunkDistanceToCamera = Vector3.Distance(m_cameraPosition, transform.position);

    // Don't update if the chunk is far away
    if (m_chunkDistanceToCamera > maxDistance + m_terrainChunk.size.x * 2f) return;

    // Don't update if the distance to the camera hasn't changed enough
    float distanceDifference = Mathf.Abs(m_chunkDistanceToCamera - m_lastChunkDistanceToCamera);
    if (distanceDifference < 8f) return;

    // Don't update
    bool shouldUpdate = DateTime.Now > lastUpdatedAt.AddSeconds(1d / 32f);
    if (!shouldUpdate) return;

    // Generate the grass
    m_lastChunkDistanceToCamera = m_chunkDistanceToCamera;
    lastUpdatedAt = DateTime.Now;
    Generate(m_levelOfDetail);
  }

  private void Generate(int levelOfDetail) {

    var timer = new System.Diagnostics.Stopwatch();
    timer.Start();

    List<Vector3> vertices = new List<Vector3>(524288);
    List<int> triangles = new List<int>(524288);

    Vector3 chunkWorldCenter = transform.position;

    for (int i = 0; i < m_terrainChunk.meshTriangles.Length; i += 3) {
      Vector3 worldCenter = m_terrainChunk.meshVertices[i] + chunkWorldCenter;

      // Calculate distance to camera
      float distance = Vector3.Distance(worldCenter, m_cameraPosition);
      if (distance > maxDistance) {
        continue;
      }

      Vector3 a = m_terrainChunk.meshVertices[i];
      Vector3 b = m_terrainChunk.meshVertices[i + 1];
      Vector3 c = m_terrainChunk.meshVertices[i + 2];
      Vector3 ab = b - a;
      Vector3 ac = c - a;

      // Calculate normal
      Vector3 unnormalizedNormal = Vector3.Cross(ab, ac);

      // Calculate area of triangle
      float area = unnormalizedNormal.magnitude / 2f;

      // Calculate number of blades for the triangle
      float normalizedLevelOfDetail = (maxDistance - Mathf.Clamp(distance, 0f, maxDistance)) / maxDistance;
      int bladeCount = Mathf.FloorToInt((float)80 * area * normalizedLevelOfDetail);

      XorshiftStar rng = new XorshiftStar((ulong)i);

      // Create for loop
      for (int bladeIndex = 0; bladeIndex < bladeCount; bladeIndex++) {
        // Get random point in triangle to spawn the blade
        float r1 = (float)rng.NextDouble();
        float r1Sqrt = Mathf.Sqrt(r1);
        float r2 = (float)rng.NextDouble();
        Vector3 center = a * (1 - r1Sqrt) + b * r1Sqrt * (1f - r2) + c * (r1Sqrt * r2);

        // Generate random scale
        float scaleMagnitude = 1f + (float)rng.NextDouble() * 0.5f;
        Vector3 scale = new Vector3(scaleMagnitude, scaleMagnitude, scaleMagnitude);

        // Generate a random rotation
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, unnormalizedNormal);
        rotation *= Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

        // Generate the blade
        // int vertexCount = GenerateBlade(center, vertices, triangles);

        // // Apply transform
        // Matrix4x4 localToWorld = Matrix4x4.TRS(center, rotation, scale);
        // for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++) {
        //   int realIndex = vertices.Count - 1 - vertexIndex;
        //   vertices[realIndex] = localToWorld.MultiplyPoint3x4(vertices[realIndex]);
        // }
      }
    }

    // Create mesh
    m_mesh.Clear();
    m_mesh.SetVertices(vertices);
    m_mesh.SetIndices(triangles, MeshTopology.Triangles, 0);
    m_mesh.RecalculateNormals();

    timer.Stop();
    Debug.LogFormat("Grass: {0} ms, Vertices: {1}", timer.ElapsedMilliseconds, vertices.Count);
  }

  private int GenerateBlade(Vector3 position, List<Vector3> vertices, List<int> triangles) {
    int currentTriangle = vertices.Count;

    // Generate quad
    vertices.Add(Vector3.zero);
    vertices.Add(new Vector3(0f, 0f, 0.05f));
    vertices.Add(new Vector3(0f, 0.3f, 0.05f));

    triangles.Add(currentTriangle);
    triangles.Add(currentTriangle + 1);
    triangles.Add(currentTriangle + 2);

    // Generate the inverted quad
    vertices.Add(new Vector3(0f, 0.3f, 0.05f));
    vertices.Add(new Vector3(0f, 0f, 0.05f));
    vertices.Add(Vector3.zero);

    triangles.Add(currentTriangle);
    triangles.Add(currentTriangle + 1);
    triangles.Add(currentTriangle + 2);

    return 6;
  }

  private void OnDestroy() {
    Destroy(m_mesh);
  }
}