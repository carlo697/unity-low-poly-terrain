using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[UnityEditor.InitializeOnLoad]
static class PreventSavingNoisePreviewTextures {
  private static Dictionary<MeshRenderer, Material> cachedMaterials =
    new Dictionary<MeshRenderer, Material>();

  static PreventSavingNoisePreviewTextures() {
    EditorSceneManager.sceneSaved += OnSceneSaved;
    EditorSceneManager.sceneSaving += OnSceneSaving;
  }

  static void OnSceneSaving(Scene scene, string path) {
    cachedMaterials.Clear();

    NoisePreview[] components = Resources.FindObjectsOfTypeAll<NoisePreview>();
    foreach (NoisePreview chunk in components) {
      MeshRenderer meshRenderer = chunk.GetComponent<MeshRenderer>();

      if (meshRenderer) {
        cachedMaterials.Add(meshRenderer, meshRenderer.sharedMaterial);
        meshRenderer.sharedMaterial = null;
      }
    }
  }

  static void OnSceneSaved(Scene scene) {
    foreach (var item in cachedMaterials) {
      item.Key.sharedMaterial = item.Value;
    }
  }
}
