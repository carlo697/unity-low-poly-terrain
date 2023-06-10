using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterReflectionProbe : MonoBehaviour {
  [SerializeField] private Transform targetCamera;

  // Update is called once per frame
  void Update() {
    if (targetCamera) {
      transform.position = targetCamera.position;
    }
  }
}
