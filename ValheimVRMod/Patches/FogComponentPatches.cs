// Fixes stereo disparity on distant fog when the sun is lighting up the fog
using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.Rendering;
using ValheimVRMod.Utilities;

class FogComponentPatches
{
    [HarmonyPatch(typeof(FogComponent), "PopulateCommandBuffer")]
    class FogComponent_PopulateCommandBuffer_Patch
    {
        private static class Uniforms
        {
            internal static readonly int _FogColor = Shader.PropertyToID("_FogColor");
            internal static readonly int _Density = Shader.PropertyToID("_Density");
            internal static readonly int _Start = Shader.PropertyToID("_Start");
            internal static readonly int _End = Shader.PropertyToID("_End");
            internal static readonly int _TempRT = Shader.PropertyToID("_TempRT");
            internal static readonly int _TopLeft = Shader.PropertyToID("_TopLeft");
            internal static readonly int _TopRight = Shader.PropertyToID("_TopRight");
            internal static readonly int _BottomLeft = Shader.PropertyToID("_BottomLeft");
            internal static readonly int _BottomRight = Shader.PropertyToID("_BottomRight");
            internal static readonly int _SunDir = Shader.PropertyToID("_SunDir");
            internal static readonly int _SunFogColor = Shader.PropertyToID("_SunFogColor");
        }

        private static Vector3[] _frustumCornersBuffer = new Vector3[4];
        private static bool Prefix(CommandBuffer cb, FogComponent __instance)
        {
            if (VHVRConfig.NonVrPlayer())
            {
                return true;
            }
            FogModel.Settings settings = __instance.model.settings;
            Material material = __instance.context.materialFactory.Get("Hidden/Post FX/Fog");
            material.shaderKeywords = null;
            Color value = (GraphicsUtils.isLinearColorSpace ? RenderSettings.fogColor.linear : RenderSettings.fogColor);
            material.SetColor(Uniforms._FogColor, value);
            material.SetFloat(Uniforms._Density, RenderSettings.fogDensity);
            material.SetFloat(Uniforms._Start, RenderSettings.fogStartDistance);
            material.SetFloat(Uniforms._End, RenderSettings.fogEndDistance);

            //Instead of the camera direction (used in the game) we use world corners so the effect is stereoscopically accurate
            var cam = __instance.context.camera;
            var camTransform = cam.transform;
            cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), cam.farClipPlane, cam.stereoActiveEye, _frustumCornersBuffer);
            var topLeftWorldFrustum = camTransform.TransformVector(_frustumCornersBuffer[1]);
            var topRightWorldFrustum = camTransform.TransformVector(_frustumCornersBuffer[2]);
            var bottomLeftWorldFrustum = camTransform.TransformVector(_frustumCornersBuffer[0]);
            var bottomRightWorldFrustum = camTransform.TransformVector(_frustumCornersBuffer[3]);
            material.SetVector(Uniforms._TopLeft, topLeftWorldFrustum);
            material.SetVector(Uniforms._TopRight, topRightWorldFrustum);
            material.SetVector(Uniforms._BottomLeft, bottomLeftWorldFrustum);
            material.SetVector(Uniforms._BottomRight, bottomRightWorldFrustum);
            switch (RenderSettings.fogMode)
            {
            case FogMode.Linear:
                material.EnableKeyword("FOG_LINEAR");
                break;
            case FogMode.Exponential:
                material.EnableKeyword("FOG_EXP");
                break;
            case FogMode.ExponentialSquared:
                material.EnableKeyword("FOG_EXP2");
                break;
            }
            RenderTextureFormat format = (__instance.context.isHdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            cb.GetTemporaryRT(Uniforms._TempRT, __instance.context.width, __instance.context.height, 24, FilterMode.Bilinear, format);
            cb.Blit(BuiltinRenderTextureType.CameraTarget, Uniforms._TempRT);
            cb.Blit(Uniforms._TempRT, BuiltinRenderTextureType.CameraTarget, material, settings.excludeSkybox ? 1 : 0);
            cb.ReleaseTemporaryRT(Uniforms._TempRT);
            return false;
        }
    }
}