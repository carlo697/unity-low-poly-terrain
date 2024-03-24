using UnityEngine;

public class GaussianKernelViewer : MonoBehaviour {
  public bool log;

  void OnDrawGizmos() {
    int size = 2;
    int axisLength = 1 + size * 2;
    float[] weights = TextureUtils.Generate3dGaussianWeights(size);

    if (log) {
      for (int i = 0; i < 3; i++) {
        string output = "";

        for (int y = 0; y < axisLength; y++) {
          for (int x = 0; x < axisLength; x++) {
            int index = TextureUtils.GetIndexFrom3d(x, y, i, axisLength, axisLength);
            output += $" {weights[index]} ";
          }

          output += "\n";
        }

        Debug.Log(output);
      }
    }

    for (int z = 0; z < axisLength; z++) {
      for (int y = 0; y < axisLength; y++) {
        for (int x = 0; x < axisLength; x++) {
          int index = TextureUtils.GetIndexFrom3d(x, y, z, axisLength, axisLength);
          float weight = weights[index];
          Gizmos.color = new Color(weight, weight, weight);
          Gizmos.DrawCube(transform.position + new Vector3(x, y, z), Vector3.one);
        }
      }
    }
  }
}
