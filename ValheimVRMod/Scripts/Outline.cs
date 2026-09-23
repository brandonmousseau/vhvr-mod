//
//  Outline.cs
//  QuickOutline
//
//  Created by Chris Nolet on 3/30/18.
//  Copyright © 2018 Chris Nolet. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimVRMod.Utilities;

[DisallowMultipleComponent]

public class Outline : MonoBehaviour {
  private static HashSet<Mesh> registeredMeshes = new HashSet<Mesh>();

  public enum Mode {
    OutlineAll,
    OutlineVisible,
    OutlineHidden,
    OutlineAndSilhouette,
    SilhouetteOnly
  }

  public Mode OutlineMode {
    get { return outlineMode; }
    set {
      outlineMode = value;
      needsUpdate = true;
    }
  }

  public Color OutlineColor {
    get { return outlineColor; }
    set {
      outlineColor = value;
      needsUpdate = true;
    }
  }

  public float OutlineWidth {
    get { return outlineWidth; }
    set {
      outlineWidth = value;
      needsUpdate = true;
    }
  }

  [Serializable]
  private class ListVector3 {
    public List<Vector3> data;
  }

  [SerializeField]
  private Mode outlineMode;

  [SerializeField]
  private Color outlineColor = Color.white;

  [SerializeField, Range(0f, 10f)]
  private float outlineWidth = 2f;

  [Header("Optional")]

  [SerializeField, Tooltip("Precompute enabled: Per-vertex calculations are performed in the editor and serialized with the object. "
  + "Precompute disabled: Per-vertex calculations are performed at runtime in Awake(). This may cause a pause for large meshes.")]
  private bool precomputeOutline;

  [SerializeField, HideInInspector]
  private List<Mesh> bakeKeys = new List<Mesh>();

  [SerializeField, HideInInspector]
  private List<ListVector3> bakeValues = new List<ListVector3>();

  // How often an enabled outline checks whether its materials were replaced by copies, see ReattachReplacedMaterials().
  private const float REATTACH_CHECK_INTERVAL = 0.5f;

  private Renderer[] renderers;
  private static Material sharedOutlineMaskMaterial;
  private static Material sharedOutlineFillMaterial;
  private Material outlineMaskMaterial;
  private Material outlineFillMaterial;

  private bool needsUpdate;
  private float nextReattachCheckTime;

  void Awake() {

    // Cache renderers
    renderers = GetComponentsInChildren<Renderer>();

    tryInitMaterials();

    // Retrieve or generate smooth normals
    LoadSmoothNormals();

    // Apply material properties immediately
    needsUpdate = true;
  }

  // Instantiate outline materials
  private void tryInitMaterials() {

    if (outlineMaskMaterial != null && outlineFillMaterial != null) {
      return;
    }
    
    if (sharedOutlineMaskMaterial == null) {
      sharedOutlineMaskMaterial = Instantiate(VRAssetManager.GetAsset<Material>("OutlineMask"));
      sharedOutlineMaskMaterial.name = "OutlineMask (Instance)";
    }
    if (sharedOutlineFillMaterial == null) {
      sharedOutlineFillMaterial = Instantiate(VRAssetManager.GetAsset<Material>("OutlineFill"));
      sharedOutlineFillMaterial.name = "OutlineFill (Instance)";
    }

    outlineMaskMaterial = new Material(sharedOutlineMaskMaterial);
    outlineFillMaterial = new Material(sharedOutlineFillMaterial);
  }

  private bool IsPlayerHairMaterials(List<Material> materials) {
    foreach (Material material in materials) {
      if (material.name.StartsWith("PlayerHair")) {
        return true;
      }
    }
    return false;
  }

  // Whether the material is an outline material, including copies of ours: reading Renderer.material(s), which the
  // game does e.g. in VisEquipment and MaterialVariation, replaces every material of that renderer with a copy, the
  // outline materials we appended included. Copies keep the shader, so that is what identifies them.
  private static bool IsOutlineMaterial(Material material) {
    return material != null && sharedOutlineMaskMaterial != null && sharedOutlineFillMaterial != null &&
      (material.shader == sharedOutlineMaskMaterial.shader || material.shader == sharedOutlineFillMaterial.shader);
  }

  private static bool ShouldSkipRenderer(Renderer renderer) {
    return renderer == null || renderer.GetType() == typeof(ParticleSystemRenderer) || renderer.sharedMaterials == null;
  }

  // Replaces any outline materials on the renderer, stale copies included, with this outline's own.
  private void AttachOutlineMaterials(Renderer renderer) {
    var materials = renderer.sharedMaterials.Where(material => !IsOutlineMaterial(material)).ToList();

    if (IsPlayerHairMaterials(materials)) {
      // Not adding outlines to player hairs: the hair, espcially eyebrows, is too close to the camera and their
      // outline may become visible even with a moderate near clip distance.
      return;
    }

    materials.Add(outlineMaskMaterial);
    materials.Add(outlineFillMaterial);

    renderer.sharedMaterials = materials.ToArray();
  }

  void OnEnable() {
    foreach (var renderer in renderers) {
      if (ShouldSkipRenderer(renderer)) {
        continue;
      }
      AttachOutlineMaterials(renderer);
    }
    nextReattachCheckTime = Time.unscaledTime + REATTACH_CHECK_INTERVAL;
  }

  // If the game replaced our outline materials with copies (see IsOutlineMaterial()), the copies no longer follow
  // the color and mode set on this outline, so an outline that was fading out would stay stuck at whatever it looked
  // like when copied. Swap our own materials back in. This also restores them after another outline covering the
  // same renderer (e.g. on a parent object) was disabled, which removes every outline material from it.
  private void ReattachReplacedMaterials() {
    foreach (var renderer in renderers) {
      if (ShouldSkipRenderer(renderer)) {
        continue;
      }
      var materials = renderer.sharedMaterials;
      if (!materials.Contains(outlineMaskMaterial) || !materials.Contains(outlineFillMaterial)) {
        AttachOutlineMaterials(renderer);
      }
    }
  }

  void OnValidate() {

    // Update material properties
    needsUpdate = true;

    // Clear cache when baking is disabled or corrupted
    if (!precomputeOutline && bakeKeys.Count != 0 || bakeKeys.Count != bakeValues.Count) {
      bakeKeys.Clear();
      bakeValues.Clear();
    }

    // Generate smooth normals when baking is enabled
    if (precomputeOutline && bakeKeys.Count == 0) {
      Bake();
    }
  }

  void Update() {
    if (needsUpdate) {
      needsUpdate = false;

      UpdateMaterialProperties();
    }

    if (Time.unscaledTime >= nextReattachCheckTime) {
      nextReattachCheckTime = Time.unscaledTime + REATTACH_CHECK_INTERVAL;
      ReattachReplacedMaterials();
    }
  }

  void OnDisable() {
    foreach (var renderer in renderers) {
      if (ShouldSkipRenderer(renderer)) {
        continue;
      }

      // Remove outline materials, including copies the game may have made of ours (see IsOutlineMaterial()).
      var materials = renderer.sharedMaterials;
      if (materials.Any(IsOutlineMaterial)) {
        renderer.sharedMaterials = materials.Where(material => !IsOutlineMaterial(material)).ToArray();
      }
    }
  }

  void Bake() {

    // Generate smooth normals for each mesh
    var bakedMeshes = new HashSet<Mesh>();

    foreach (var meshFilter in GetComponentsInChildren<MeshFilter>()) {

      // Skip duplicates
      if (!bakedMeshes.Add(meshFilter.sharedMesh)) {
        continue;
      }

      // Serialize smooth normals
      var smoothNormals = SmoothNormals(meshFilter.sharedMesh);

      bakeKeys.Add(meshFilter.sharedMesh);
      bakeValues.Add(new ListVector3() { data = smoothNormals });
    }
  }

  void LoadSmoothNormals() {

    // Retrieve or generate smooth normals
    foreach (var meshFilter in GetComponentsInChildren<MeshFilter>()) {
      if (!meshFilter.sharedMesh.isReadable)
      {
        continue;
      }
      // Skip if smooth normals have already been adopted
      if (!registeredMeshes.Add(meshFilter.sharedMesh)) {
        continue;
      }

      // Retrieve or generate smooth normals
      var index = bakeKeys.IndexOf(meshFilter.sharedMesh);
      var smoothNormals = (index >= 0) ? bakeValues[index].data : SmoothNormals(meshFilter.sharedMesh);

      // Store smooth normals in UV3
      meshFilter.sharedMesh.SetUVs(3, smoothNormals);
    }

    // Clear UV3 on skinned mesh renderers
    foreach (var skinnedMeshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>()) {
      if (!skinnedMeshRenderer.sharedMesh.isReadable)
      {
        continue;
      }
      if (registeredMeshes.Add(skinnedMeshRenderer.sharedMesh)) {
        skinnedMeshRenderer.sharedMesh.uv4 = new Vector2[skinnedMeshRenderer.sharedMesh.vertexCount];
      }
    }
  }

  List<Vector3> SmoothNormals(Mesh mesh) {

    // Group vertices by location
    var groups = mesh.vertices.Select((vertex, index) => new KeyValuePair<Vector3, int>(vertex, index)).GroupBy(pair => pair.Key);

    // Copy normals to a new list
    var smoothNormals = new List<Vector3>(mesh.normals);

    // Average normals for grouped vertices
    foreach (var group in groups) {

      // Skip single vertices
      if (group.Count() == 1) {
        continue;
      }

      // Calculate the average normal
      var smoothNormal = Vector3.zero;

      foreach (var pair in group) {
        smoothNormal += mesh.normals[pair.Value];
      }

      smoothNormal.Normalize();

      // Assign smooth normal to each vertex
      foreach (var pair in group) {
        smoothNormals[pair.Value] = smoothNormal;
      }
    }

    return smoothNormals;
  }

  void UpdateMaterialProperties() {

    // Apply properties according to mode
    outlineFillMaterial.SetColor("_OutlineColor", outlineColor);

    switch (outlineMode) {
      case Mode.OutlineAll:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;

      case Mode.OutlineVisible:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;

      case Mode.OutlineHidden:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;

      case Mode.OutlineAndSilhouette:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;

      case Mode.SilhouetteOnly:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
        outlineFillMaterial.SetFloat("_OutlineWidth", 0);
        break;
    }
  }
}
