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
  // Meshes already warned about in WarnIfOnlyLastSubmeshOutlined(), so that each is reported once.
  private static HashSet<Mesh> multiSubmeshWarnedMeshes = new HashSet<Mesh>();

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

  private Renderer[] renderers;
  private static Material sharedOutlineMaskMaterial;
  private static Material sharedOutlineFillMaterial;
  private Material outlineMaskMaterial;
  private Material outlineFillMaterial;

  private bool needsUpdate;

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
      sharedOutlineMaskMaterial.name = "OutlineFill (Instance)";
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

  // Whether the renderer is the body model of a player or of an NPC built like one (e.g. the Viking ghosts in the Deep
  // North). Its mesh has two submeshes, the body and then the eyebrows, and Unity draws the materials appended past
  // the submesh count on the last submesh only, so an outline on it outlines just the eyebrows. Those sit too close to
  // the VR camera on the local player, and VisEquipment.UpdateColors() reads the body model's Renderer.materials every
  // frame, which replaces the outline materials with copies that no longer follow the outline's color and leaves the
  // eyebrows outlined in whatever color the outline had at that moment.
  private static bool IsCharacterBodyModel(Renderer renderer) {
    var visEquipment = renderer.GetComponentInParent<VisEquipment>();
    return visEquipment != null && visEquipment.m_bodyModel == renderer;
  }

  // For the same reason as above, the outline covers only the last submesh of a mesh with several of them.
  private static void WarnIfOnlyLastSubmeshOutlined(Renderer renderer) {
    var skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
    if (skinnedMeshRenderer == null) {
      return;
    }
    var mesh = skinnedMeshRenderer.sharedMesh;
    if (mesh == null || mesh.subMeshCount <= 1 || !multiSubmeshWarnedMeshes.Add(mesh)) {
      return;
    }
    LogUtils.LogWarning(
      "Outlining skinned mesh " + mesh.name + " on " + renderer.name + ", which has " + mesh.subMeshCount +
      " submeshes: only the last one will be outlined.");
  }

  void OnEnable() {
    foreach (var renderer in renderers) {

      if (renderer.GetType() == typeof(ParticleSystemRenderer)) {
        continue;
      }

      if (IsCharacterBodyModel(renderer)) {
        continue;
      }
      
      // Append outline shaders
      var materials = renderer.sharedMaterials.ToList();

      if (IsPlayerHairMaterials(materials)) {
        // Two reasons for not adding outlines to player hairs:
        // 1. The material array on player hairs are finicky and we might not be able find the correct outline material instances later when we attempt to remove them.
        // 2. The hair, espcially eyebrows, is too close to the camera and their outline may become visible even with a moderate near clip distance.
        continue;
      }

      WarnIfOnlyLastSubmeshOutlined(renderer);

      materials.Add(outlineMaskMaterial);
      materials.Add(outlineFillMaterial);

      renderer.materials = materials.ToArray();
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
  }

  void OnDisable() {
    foreach (var renderer in renderers) {

      if (renderer == null || renderer.GetType() == typeof(ParticleSystemRenderer) || renderer.sharedMaterials == null) {
        continue;
      }

      // Remove outline shaders
      var materials = renderer.sharedMaterials.ToList();
      // TODO: there is a chance that the vanilla game or other mods has modified the material array since we added the outline materials,
      // which would make the outline materials references here stale and cause us to fail to remove them.
      // Consider, instead, iterating over the materials and check materials[i].name.startWith("OutlineMask") || materials[i].name.startWith("OutlineFill")
      materials.Remove(outlineMaskMaterial);
      materials.Remove(outlineFillMaterial);
      renderer.materials = materials.ToArray();
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
