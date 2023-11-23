
using UnityEngine;

[CreateAssetMenu(menuName = "Terrain/Detail", order = 1)]
public class DetailAsset : Detail {
  public override int id { get { return m_id; } }
  [SerializeField] private int m_id;

  public override GameObject[] prefabs { get { return m_prefabs; } }
  [SerializeField] private GameObject[] m_prefabs;

  public override DetailMeshSet[] meshes { get { return m_meshes; } }
  [SerializeField] private DetailMeshSet[] m_meshes = new DetailMeshSet[0];

  public override float maxDistance { get { return m_maxDistance; } }
  [Range(0f, 1f)]
  [SerializeField] private float m_maxDistance = 1f;
}
