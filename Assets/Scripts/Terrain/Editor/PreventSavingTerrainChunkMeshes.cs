using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[UnityEditor.InitializeOnLoad]
static class PreventSavingTerrainChunkMeshes {
  private static Dictionary<MeshFilter, Mesh> cachedMeshes = new Dictionary<MeshFilter, Mesh>();

  static PreventSavingTerrainChunkMeshes() {
    EditorSceneManager.sceneSaved += OnSceneSaved;
    EditorSceneManager.sceneSaving += OnSceneSaving;
  }

  static void OnSceneSaving(Scene scene, string path) {
    cachedMeshes.Clear();

    TerrainChunk[] components = Resources.FindObjectsOfTypeAll<TerrainChunk>();
    foreach (TerrainChunk chunk in components) {
      MeshFilter meshFilter = chunk.GetComponent<MeshFilter>();

      if (meshFilter) {
        cachedMeshes.Add(meshFilter, meshFilter.sharedMesh);
        meshFilter.sharedMesh = null;
      }
    }
  }

  static void OnSceneSaved(Scene scene) {
    foreach (var item in cachedMeshes) {
      item.Key.sharedMesh = item.Value;
    }
  }
}
