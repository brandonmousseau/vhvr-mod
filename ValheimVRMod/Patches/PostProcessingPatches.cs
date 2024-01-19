using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.Rendering;
using UnityEngine.XR;
using ValheimVRMod.Scripts;
using ValheimVRMod.Scripts.PostProcessing;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches
{
    [HarmonyPatch]
    public class PostProcessingPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PostProcessingBehaviour), "OnEnable")]
        static void PostfixPostProcessingOnEnable(PostProcessingBehaviour __instance, TaaComponent ___m_Taa, Dictionary<PostProcessingComponentBase, bool> ___m_ComponentStates, List<PostProcessingComponentBase> ___m_Components)
        {
            if (VHVRConfig.NonVrPlayer()) return;
            ___m_ComponentStates.Remove(___m_Taa);
            ___m_Components.Remove(___m_Taa);

            var vrTaaComponent = new VRTaaComponent();
            VRTaaComponent.PostProcessingExtension.Add(__instance, vrTaaComponent);

            ___m_Components.Add(vrTaaComponent);
            ___m_ComponentStates.Add(vrTaaComponent, false);
        }

        internal static readonly int RenderViewportScaleFactor = Shader.PropertyToID("_RenderViewportScaleFactor");
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PostProcessingBehaviour), "OnPostRender")]
        static void PostfixOnPostRender(PostProcessingBehaviour __instance, PostProcessingProfile ___profile, bool ___m_RenderingInSceneView, Camera ___m_Camera, PostProcessingContext ___m_Context)
        {
            if (VHVRConfig.NonVrPlayer()) return;
            if (!(___profile == null) && !(___m_Camera == null) && !___m_RenderingInSceneView && VRTaaComponent.PostProcessingExtension.GetOrCreateValue(__instance).active && !___profile.debugViews.willInterrupt)
            {
                ___m_Context.camera.ResetStereoProjectionMatrices();
                Shader.SetGlobalFloat(RenderViewportScaleFactor, XRSettings.renderViewportScale);
            }
        }

        private static FieldInfo LoadsTaaComponent =
            AccessTools.Field(typeof(PostProcessingBehaviour), "m_Taa");
        private static MethodInfo CallsTaaSetProjectionMatrix =
            AccessTools.Method(typeof(TaaComponent), nameof(TaaComponent.SetProjectionMatrix), new Type[] { typeof(Func<Vector2, Matrix4x4>) });
        private static MethodInfo CallsTaaRender =
            AccessTools.Method(typeof(TaaComponent), nameof(TaaComponent.Render), new Type[] { typeof(RenderTexture), typeof(RenderTexture) });
        private static MethodInfo CallsTaaResetHistory = AccessTools.Method(typeof(TaaComponent), nameof(TaaComponent.ResetHistory));
        private static MethodInfo CallsTaaGetJitterVector = AccessTools.PropertyGetter(typeof(TaaComponent), nameof(TaaComponent.jitterVector));

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(PostProcessingBehaviour), "OnPostRender")]
        [HarmonyPatch(typeof(PostProcessingBehaviour), "OnPreCull")]
        [HarmonyPatch(typeof(PostProcessingBehaviour), "OnRenderImage")]
        [HarmonyPatch(typeof(PostProcessingBehaviour), "ResetTemporalEffects")]
        static IEnumerable<CodeInstruction> TranspileTaaComponentAway(IEnumerable<CodeInstruction> instructions)
        {
            if (VHVRConfig.NonVrPlayer()) return instructions;
            
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            
            foreach (var instruction in original)
            {
                if (instruction.LoadsField(LoadsTaaComponent))
                {
                    Debug.Log("Patched TAA reference");
                    var lastInstuction = patched[patched.Count - 1];
                    if (lastInstuction != null && lastInstuction.opcode == OpCodes.Ldarg_0)
                        patched.RemoveAt(patched.Count - 1);
                    patched.Add(new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(VRTaaComponent), nameof(VRTaaComponent.PostProcessingExtension))));
                    patched.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    patched.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ConditionalWeakTable<PostProcessingBehaviour, VRTaaComponent>), nameof(ConditionalWeakTable<PostProcessingBehaviour, VRTaaComponent>.GetOrCreateValue), new Type[] { typeof(PostProcessingBehaviour)})));
                }
                else if (instruction.Calls(CallsTaaSetProjectionMatrix))
                {
                    Debug.Log("Patched TAA SetProjectionMatrix");
                    patched.Add(new CodeInstruction(instruction.opcode, AccessTools.Method(typeof(VRTaaComponent), nameof(VRTaaComponent.ConfigureStereoMonoProjectionMatrices), new Type[] { typeof(Func<Vector2, Matrix4x4>) })));
                }
                else if (instruction.Calls(CallsTaaRender))
                {
                    Debug.Log("Patched TAA Render");
                    patched.Add(new CodeInstruction(instruction.opcode, AccessTools.Method(typeof(VRTaaComponent), nameof(VRTaaComponent.Render), new Type[] { typeof(RenderTexture), typeof(RenderTexture) })));
                }
                else if (instruction.Calls(CallsTaaGetJitterVector))
                {
                    Debug.Log("Patched TAA GetJitterVector");
                    patched.Add(new CodeInstruction(instruction.opcode, AccessTools.PropertyGetter(typeof(VRTaaComponent), nameof(VRTaaComponent.jitterVector))));
                }
                else if (instruction.Calls(CallsTaaResetHistory))
                {
                    Debug.Log("Patched TAA ResetHistory");
                    patched.Add(new CodeInstruction(instruction.opcode, AccessTools.Method(typeof(VRTaaComponent), nameof(VRTaaComponent.ResetHistory))));
                }
                else
                {
                    patched.Add(instruction);
                }
            }
            return patched;
        }
    
        /**
            Motion Vector Fixes for some objects
        */
        static void EnableCameraMotionVectors(GameObject gameObject)
        {
            var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            foreach(var renderer in renderers.Where(x => x is LineRenderer || x is SkinnedMeshRenderer))
            {
                if(renderer.motionVectorGenerationMode != MotionVectorGenerationMode.Camera)
                    Debug.Log($"{renderer.name} had motion vectors to {renderer.motionVectorGenerationMode}");
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Ship), "Start")]
        static void PostfixShipStart(Ship __instance)
        {
            if (VHVRConfig.NonVrPlayer())
            {
                return;
            }
            EnableCameraMotionVectors(__instance.gameObject);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PostProcessingBehaviour), "OnPreRender")]
        static void PrefixPostProcessingBehaviorPreRender(PostProcessingBehaviour __instance)
        {
            if (VHVRConfig.NonVrPlayer())
            {
                return;
            }
            __instance.profile.bloom.m_Settings.lensDirt.intensity = 0f;
        }
    }
}