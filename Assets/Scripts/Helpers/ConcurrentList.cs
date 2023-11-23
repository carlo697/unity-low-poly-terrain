
using System;
using System.Collections.Generic;

public class ConcurrentList<T> {
  public object syncObject;
  public List<T> list;

  public ConcurrentList() {
    this.syncObject = new Object();
    this.list = new List<T>();
  }

  public T this[int index] {
    get {
      lock (syncObject) {
        return list[index];
      }
    }
    set {
      lock (syncObject) {
        list[index] = value;
      }
    }
  }

  public int count {
    get {
      lock (syncObject) {
        return list.Count;
      }
    }
  }

  public void Add(T item) {
    lock (syncObject) {
      list.Add(item);
    }
  }

  public void Clear() {
    lock (syncObject) {
      list.Clear();
    }
  }
}
