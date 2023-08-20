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

  public static void Allocate(GameObject prefab, int count = 1000) {
    string type = prefab.name;

    // Get the stack
    Stack<GameObject> stack;
    pool.TryGetValue(type, out stack);

    // Register a new stack for the prefab if necessary
    if (stack == null) {
      stack = pool[type] = new(count * 2);
    }

    for (int i = 0; i < count; i++) {
      // Instantiate the prefab
      GameObject gameObject = GameObject.Instantiate(prefab);
      gameObject.name = type;

      // Disable the object and parent it
      gameObject.SetActive(false);
      gameObject.transform.SetParent(parent);

      // Add the object to the stack
      stack.Push(gameObject);
    }
  }

  public static GameObject Get(GameObject prefab, int capacity = 1000) {
    string type = prefab.name;

    // Get the stack
    Stack<GameObject> stack;
    pool.TryGetValue(type, out stack);

    // Allocate more objects if the stack doesn't exist or is getting short
    if (stack == null || stack.Count < 25) {
      Allocate(prefab, 100);
      stack = pool[type];
    }

    // Return an instantiated prefab from the stack
    GameObject gameObject = gameObject = stack.Pop();

    // Enable the instantiated prefab and return it
    gameObject.SetActive(true);
    return gameObject;
  }

  public static void Release(GameObject gameObj) {
    // Get the type name
    string type = gameObj.name;

    // Disable the object and parent it
    gameObj.SetActive(false);
    gameObj.transform.SetParent(parent);

    // Add the object to the pool
    pool[type].Push(gameObj);
  }
}