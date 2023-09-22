using UnityEngine;
using System;
using System.Collections.Generic;

public static class PrefabPool {
  private static Dictionary<string, Stack<GameObject>> pool = new();
  private static Transform parent;

  static PrefabPool() {
    GameObject parentObject = new GameObject("Prefab Pool");
    GameObject.DontDestroyOnLoad(parentObject);
    parent = parentObject.transform;
  }

  public static GameObject Get(GameObject prefab, int capacity = 1000) {
    string type = prefab.name;

    // Get the stack or create it if necessary
    Stack<GameObject> stack;
    if (!pool.TryGetValue(type, out stack)) {
      stack = pool[type] = new();
    }

    GameObject gameObject;
    if (stack.Count == 0) {
      // Instantiate the prefab
      gameObject = GameObject.Instantiate(prefab);
      gameObject.name = type;

      // Disable the object and parent it
      gameObject.SetActive(false);
      gameObject.transform.SetParent(parent);

      return gameObject;
    } else {
      // Return an instantiated prefab from the stack
      gameObject = stack.Pop();

      // Enable the instantiated prefab and return it
      // gameObject.SetActive(true);
    }

    return gameObject;
  }

  public static void Release(GameObject gameObj) {
    // Get the type name
    string type = gameObj.name;

    // Disable the object and parent it
    gameObj.SetActive(false);
    // gameObj.transform.SetParent(parent);

    // Add the object to the pool
    pool[type].Push(gameObj);
  }
}