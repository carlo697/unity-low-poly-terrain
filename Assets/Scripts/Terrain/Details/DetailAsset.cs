
using UnityEngine;

[CreateAssetMenu(menuName = "Terrain/Detail", order = 1)]
public class DetailAsset : Detail {
  public override int id { get { return m_id; } }
  [SerializeField] private int m_id;

  public override GameObject[] prefabs { get { return m_prefabs; } }
  [SerializeField] private GameObject[] m_prefabs;

  public override int preAllocateCount { get { return m_preAllocateCount; } }
  [SerializeField] private int m_preAllocateCount = 100;

  public override DetailSubmesh[] submeshes { get { return m_submeshes; } }
  [SerializeField] private DetailSubmesh[] m_submeshes = new DetailSubmesh[0];
}
