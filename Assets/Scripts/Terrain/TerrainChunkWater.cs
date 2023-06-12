
using UnityEngine;

[RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshRenderer))]
public class TerrainChunkWater : MonoBehaviour {
  public Vector2Int resolution = new Vector2Int(32, 32);
  public Vector2 size = new Vector2(32f, 32f);
  public float seaLevel;

  private MeshFilter m_meshFilter;

  void Awake() {
    m_meshFilter = GetComponent<MeshFilter>();
  }

  void Start() {
    Mesh mesh = new Mesh();
    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    m_meshFilter.sharedMesh = mesh;

    int numVertices = (resolution.x + 1) * (resolution.y + 1);
    Vector3[] vertices = new Vector3[numVertices];
    Vector2[] uv = new Vector2[numVertices];
    int[] triangles = new int[resolution.x * resolution.y * 6];

    for (int y = 0; y <= resolution.y; y++) {
      for (int x = 0; x <= resolution.x; x++) {
        int index = y * (resolution.x + 1) + x;
        float xPos = (float)x / resolution.x;
        float yPos = (float)y / resolution.y;
        vertices[index] = new Vector3(xPos * size.x, 0f, yPos * size.y);
        uv[index] = new Vector2(xPos * size.x, yPos * size.y);
      }
    }

    int triangleIndex = 0;
    for (int y = 0; y < resolution.y; y++) {
      for (int x = 0; x < resolution.x; x++) {
        int vertexIndex = y * (resolution.x + 1) + x;
        triangles[triangleIndex + 2] = vertexIndex;
        triangles[triangleIndex + 1] = vertexIndex + 1;
        triangles[triangleIndex] = vertexIndex + resolution.x + 2;
        triangles[triangleIndex + 5] = vertexIndex;
        triangles[triangleIndex + 4] = vertexIndex + resolution.x + 2;
        triangles[triangleIndex + 3] = vertexIndex + resolution.x + 1;
        triangleIndex += 6;
      }
    }

    mesh.vertices = vertices;
    mesh.uv = uv;
    mesh.triangles = triangles;
    mesh.RecalculateNormals();
  }

  void Update() {

  }
}
