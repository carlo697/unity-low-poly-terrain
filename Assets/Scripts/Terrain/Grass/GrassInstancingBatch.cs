using UnityEngine;
using System.Collections.Generic;

public class GrassInstancingBatch {
  public Grass grass;
  public List<Matrix4x4> matrices = new List<Matrix4x4>();

  public GrassInstancingBatch(Grass grass) {
    this.grass = grass;
  }
}