using UnityEngine;
using System.Runtime.CompilerServices;

public class SimpleGrid2<T> where T : new() {
  public Vector2 center { get { return m_center; } set { m_center = value; } }
  private Vector2 m_center;

  public Vector2 size { get { return m_size; } }
  private Vector2 m_size;

  public Vector2 halfSize { get { return m_halfSize; } }
  private Vector2 m_halfSize;

  public Vector2Int resolution { get { return m_resolution; } }
  private Vector2Int m_resolution;

  public T[] cells { get { return m_cells; } }
  private T[] m_cells;

  public SimpleGrid2(Vector2 center, Vector2 size, Vector2Int resolution) {
    this.m_center = center;
    this.m_size = size;
    this.m_halfSize = size / 2f;
    this.m_resolution = resolution;
    this.m_cells = new T[m_resolution.x * m_resolution.y];
    for (int i = 0; i < m_cells.Length; i++) {
      m_cells[i] = new T();
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public T GetCellAt(float x, float y) {
    float relativeX = (x - center.x + m_halfSize.x);
    float relativeY = (y - center.y + m_halfSize.y);

    // Convert position to coords
    int coordX = Mathf.FloorToInt((relativeX * resolution.x) / m_size.x);
    int coordY = Mathf.FloorToInt((relativeY * resolution.y) / m_size.y);

    // Convert coords to index
    return m_cells[coordX + coordY * m_resolution.x];
  }
}