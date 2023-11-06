
using UnityEngine;

[CreateAssetMenu(menuName = "Terrain/Grass", order = 3)]
public class GrassAsset : Grass {
  public override int id { get { return m_id; } }
  [SerializeField] private int m_id;

  public override DetailMeshSet[] meshes { get { return m_meshes; } }
  [SerializeField] private DetailMeshSet[] m_meshes = new DetailMeshSet[0];

  public override float maxDistance { get { return m_maxDistance; } }
  [Range(0f, 1f)]
  [SerializeField] private float m_maxDistance = 1f;
}
