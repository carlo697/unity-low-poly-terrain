using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public enum DetailsBatchRenderMode {
  Normal,
  Solid,
  Shadows
}

public class DetailsInstancingBatch {
  public DetailSubmesh[] submeshes;
  public Bounds bounds;

  public DetailsBatchRenderMode renderMode;

  public bool isConcurrent;
  public List<Matrix4x4> matrixList = new();
  public ConcurrentList<Matrix4x4> matrixConcurrentList = new();

  public List<Matrix4x4> matrices { get { return isConcurrent ? matrixConcurrentList.list : matrixList; } }

  public int submeshCount { get { return submeshes.Length; } }

  public bool hasShadows { get { return m_hasShadows; } }
  private bool m_hasShadows;

  private ComputeBuffer argsBuffer;
  private uint[] args;
  private ComputeBuffer matrixBuffer;
  private MaterialPropertyBlock materialPropertyBlock;

  public DetailsInstancingBatch(
    DetailSubmesh[] submeshes,
    Bounds bounds = new Bounds(),
    DetailsBatchRenderMode renderMode = DetailsBatchRenderMode.Normal,
    bool concurrent = false
  ) {
    this.submeshes = submeshes;
    this.bounds = bounds;
    this.materialPropertyBlock = new MaterialPropertyBlock();
    this.renderMode = renderMode;
    this.isConcurrent = concurrent;

    args = new uint[submeshCount * 5];
    for (int i = 0; i < submeshCount; i++) {
      DetailSubmesh submesh = submeshes[i];
      int offset = i * 5;

      // 0 == Submesh index count
      args[offset] = (uint)submesh.mesh.GetIndexCount(submesh.submeshIndex);
      // 1 == Instance count (unkown at the start)
      args[offset + 1] = 0;
      // 2 = Submesh start index location
      args[offset + 2] = (uint)submesh.mesh.GetIndexStart(submesh.submeshIndex);
      // 3 = Submesh base vertex location
      args[offset + 3] = (uint)submesh.mesh.GetBaseVertex(submesh.submeshIndex);
      // 4 = Start instance location
      args[offset + 4] = 0;

      if (submesh.castShadows != ShadowCastingMode.Off) {
        m_hasShadows = true;
      }
    }
  }

  public void Render() {
    if (matrices.Count > 0) {
      for (int i = 0; i < submeshes.Length; i++) {
        DetailSubmesh submesh = submeshes[i];

        if (renderMode == DetailsBatchRenderMode.Shadows
          && submesh.castShadows == ShadowCastingMode.Off
        ) {
          return;
        }

        ShadowCastingMode shadowCastingMode;
        switch (renderMode) {
          case DetailsBatchRenderMode.Shadows:
            shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            break;
          case DetailsBatchRenderMode.Solid:
            shadowCastingMode = ShadowCastingMode.Off;
            break;
          default:
            shadowCastingMode = submesh.castShadows;
            break;
        }

        int argsOffset = i * 5 * sizeof(uint);

        Graphics.DrawMeshInstancedIndirect(
          submesh.mesh,
          submesh.submeshIndex,
          submesh.material,
          bounds,
          argsBuffer,
          argsOffset,
          materialPropertyBlock,
          shadowCastingMode
        );
      }
    }
  }

  public void Clear() {
    matrices.Clear();
  }

  public void Destroy() {
    ReleaseBuffers();
  }

  private void ReleaseBuffers() {
    if (matrixBuffer != null) {
      matrixBuffer.Release();
      matrixBuffer = null;
    }

    if (argsBuffer != null) {
      argsBuffer.Release();
      argsBuffer = null;
    }
  }

  public void UploadBuffers() {
    ReleaseBuffers();

    if (matrices.Count == 0) {
      return;
    }

    // Prepare the matrices buffer and send it to the property block
    matrixBuffer = new ComputeBuffer(matrices.Count, Marshal.SizeOf<Matrix4x4>());
    matrixBuffer.SetData(matrices);
    materialPropertyBlock.SetBuffer("matrixBuffer", matrixBuffer);

    // Prepare indirect arguments
    argsBuffer = new ComputeBuffer(submeshCount * 5, sizeof(uint), ComputeBufferType.IndirectArguments);
    for (int i = 0; i < submeshCount; i++) {
      int offset = i * 5;
      // Set the instance count
      args[offset + 1] = (uint)matrices.Count;
    }

    // Send arguments buffer to the property block
    argsBuffer.SetData(args);
  }
}