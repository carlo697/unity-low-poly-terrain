using UnityEngine;

public delegate bool GetDetailResult(RaycastHit hit, out DetailInstance instance);

public struct TempDetailInstance {
  public RaycastCommand raycastCommand;
  public GetDetailResult GetFinalInstance;
}
