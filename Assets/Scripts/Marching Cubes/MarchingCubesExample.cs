using UnityEngine;
using System;

public class MarchingCubesExample : MonoBehaviour {
  public Vector3Int resolution = Vector3Int.one * 10;
  public float noiseSize = 1f;
  public int noiseOctaves = 3;
  public Vector3 noiseOffset = Vector3.zero;
  public bool multithreaded;

  public float threshold = 0.5f;
  public bool useMiddlePoint = false;

  public bool drawGizmos = true;
  public float gizmosSize = 0.1f;

  private CubeGrid m_grid;

  [SerializeField] private bool m_rebuildFlag;
  private MeshFilter m_meshFilter;
  private MeshRenderer m_meshRenderer;

  void Start() {
    RegenerateIfNeeded();
  }

  void Update() {
    RegenerateIfNeeded();
  }

  [ContextMenu("InstantRegenerate")]
  public void InstantRegenerate() {
    var totalTimer = new System.Diagnostics.Stopwatch();
    totalTimer.Start();

    // Generate noise
    FractalNoise noise = new FractalNoise(1 / noiseSize, 1f, 0.5f, noiseOctaves);
    Func<float, float, float, float> samplerFunc = (float x, float y, float z) => {
      return noise.Sample(
        x + noiseOffset.x,
        y + noiseOffset.y,
        z + noiseOffset.z
      );
    };

    // Create a new grid
    m_grid = new CubeGrid(
      samplerFunc,
      resolution,
      threshold,
      useMiddlePoint,
      multithreaded
    );

    // Generate the mesh from the grid
    Mesh mesh = m_grid.Render();
    mesh.name = gameObject.name;

    var stepTimer = new System.Diagnostics.Stopwatch();
    stepTimer.Start();

    // Add a mesh filter
    m_meshFilter = GetComponent<MeshFilter>();
    if (!m_meshFilter) {
      m_meshFilter = gameObject.AddComponent<MeshFilter>();
    }
    m_meshFilter.sharedMesh = mesh;

    // Add a mesh renderer
    m_meshRenderer = GetComponent<MeshRenderer>();
    if (!m_meshRenderer) {
      m_meshRenderer = gameObject.AddComponent<MeshRenderer>();
    }

    // Check if it has a mesh collider
    MeshCollider collider = GetComponent<MeshCollider>();
    if (collider) {
      collider.sharedMesh = mesh;
    }

    stepTimer.Stop();
    Debug.Log(
      string.Format(
        "Apply to components: {0} ms",
        stepTimer.ElapsedMilliseconds
      )
    );

    totalTimer.Stop();
    Debug.Log(string.Format("Total: {0} ms", totalTimer.ElapsedMilliseconds));
  }

  public void FlagToRegenerate() {
    m_rebuildFlag = true;
  }

  private void RegenerateIfNeeded() {
    if (m_rebuildFlag || m_grid == null) {
      InstantRegenerate();
      m_rebuildFlag = false;
    }
  }

  private void OnValidate() {
    FlagToRegenerate();
  }

  private void OnDrawGizmos() {
    RegenerateIfNeeded();

    if (!drawGizmos) return;

    for (int z = 0; z < m_grid.resolution.z; z++) {
      for (int y = 0; y < m_grid.resolution.y; y++) {
        for (int x = 0; x < m_grid.resolution.x; x++) {
          Vector3 pointPosition = m_grid.GetPointPosition(x, y, z);
          Vector3 globalPointPosition = transform.TransformPoint(pointPosition);

          float value = m_grid.GetPoint(x, y, z).value;
          Gizmos.color = new Color(value, value, value);
          Gizmos.DrawCube(globalPointPosition, Vector3.one * gizmosSize);
        }
      }
    }
  }
}
