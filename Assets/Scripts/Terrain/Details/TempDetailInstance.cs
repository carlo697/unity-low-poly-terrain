using UnityEngine;
using System;

public struct TempDetailInstance {
  public RaycastCommand raycastCommand;
  public Func<RaycastHit, DetailInstance?> GetFinalInstance;
}
