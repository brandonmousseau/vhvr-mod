using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.PostProcessing;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts.PostProcessing
{
    /// <summary>
    /// Halton sequence utility.
    /// </summary>
    public static class HaltonSeq
    {
        /// <summary>
        /// Gets a value from the Halton sequence for a given index and radix.
        /// </summary>
        /// <param name="index">The sequence index</param>
        /// <param name="radix">The sequence base</param>
        /// <returns>A number from the Halton sequence between 0 and 1.</returns>
        public static float Get(int index, int radix)
        {
            float result = 0f;
            float fraction = 1f / (float)radix;

            while (index > 0)
            {
                result += (float)(index % radix) * fraction;

                index /= radix;
                fraction /= (float)radix;
            }

            return result;
        }
    }

    /**
    Backport of VR Taa implementation in Post Processing Stack v2
    */
    public class VRTaaComponent : PostProcessingComponentRenderTexture<AntialiasingModel>
    {
        public static readonly ConditionalWeakTable<PostProcessingBehaviour, VRTaaComponent> PostProcessingExtension =
            new ConditionalWeakTable<PostProcessingBehaviour, VRTaaComponent>();

        private const string k_ShaderString = "Hidden/Post FX/Temporal Anti-aliasing";
        private const int k_SampleCount = 8;

        readonly RenderBuffer[] m_Mrt = new RenderBuffer[2];
        bool m_ResetHistory = true;
        Func<Vector2, Matrix4x4> m_jitteredFunc;

        /// <summary>
        /// The current sample index.
        /// </summary>
        public int sampleIndex { get; private set; }

        public Vector2 jitterVector { get; private set; }

        // Ping-pong between two history textures as we can't read & write the same target in the
        // same pass
        const int k_NumEyes = 2;
        const int k_NumHistoryTextures = 2;
        readonly RenderTexture[][] m_HistoryTextures = new RenderTexture[k_NumEyes][];

        readonly int[] m_HistoryPingPong = new int[k_NumEyes];

        private static class Uniforms
        {
            internal static int _Jitter = Shader.PropertyToID("_Jitter");

            internal static int _SharpenParameters = Shader.PropertyToID("_SharpenParameters");

            internal static int _FinalBlendParameters = Shader.PropertyToID("_FinalBlendParameters");

            internal static int _HistoryTex = Shader.PropertyToID("_HistoryTex");

            internal static int _MainTex = Shader.PropertyToID("_MainTex");
        }

        public override bool active
        {
            get
            {
                if (model.enabled && model.settings.method == AntialiasingModel.Method.Taa && SystemInfo.supportsMotionVectors && SystemInfo.supportedRenderTargetCount >= 2)
                {
                    return !context.interrupted;
                }
                return false;
            }
        }

        public override DepthTextureMode GetCameraFlags()
        {
            return DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
        }

        public void ResetHistory()
        {
            m_ResetHistory = true;
        }

        public void ConfigureStereoMonoProjectionMatrices(Func<Vector2, Matrix4x4> jitteredFunc)
        {
            m_jitteredFunc = jitteredFunc;
        }

        public void ApplyProjectionMatrices()
        {
            if (context.camera.stereoEnabled)
            {
                ConfigureStereoJitteredProjectionMatrices(m_jitteredFunc);
            }
            else
            {
                ConfigureJitteredProjectionMatrix(m_jitteredFunc);
            }
        }

        /// <summary>
        /// Generates a jittered projection matrix for a given camera.
        /// </summary>
        /// <param name="camera">The camera to get a jittered projection matrix for.</param>
        /// <returns>A jittered projection matrix.</returns>
        public Matrix4x4 GetJitteredProjectionMatrix(Camera camera, Func<Vector2, Matrix4x4> jitteredFunc)
        {
            AntialiasingModel.TaaSettings taaSettings = model.settings.taaSettings;
            Matrix4x4 cameraProj;
            jitterVector = GenerateRandomOffset();
            jitterVector *= taaSettings.jitterSpread;

            if (jitteredFunc != null)
            {
                cameraProj = jitteredFunc(jitterVector);
            }
            else
            {
                cameraProj = camera.orthographic
                    ? PostProcessingUtils.GetJitteredOrthographicProjectionMatrix(camera, jitterVector)
                    : PostProcessingUtils.GetJitteredPerspectiveProjectionMatrix(camera, jitterVector);
            }

            jitterVector = new Vector2(jitterVector.x / camera.pixelWidth, jitterVector.y / camera.pixelHeight);
            return cameraProj;
        }

        /// <summary>
        /// Prepares the jittered and non jittered projection matrices.
        /// </summary>
        /// <param name="context">The current post-processing context.</param>
        public void ConfigureJitteredProjectionMatrix(Func<Vector2, Matrix4x4> jitteredFunc)
        {
            var camera = context.camera;
            camera.nonJitteredProjectionMatrix = camera.projectionMatrix;
            camera.projectionMatrix = GetJitteredProjectionMatrix(camera, jitteredFunc);
            camera.useJitteredProjectionMatrixForTransparentRendering = false;
        }

        /// <summary>
        /// Prepares the jittered and non jittered projection matrices for stereo rendering.
        /// </summary>
        /// <param name="context">The current post-processing context.</param>
        // TODO: We'll probably need to isolate most of this for SRPs
        public void ConfigureStereoJitteredProjectionMatrices(Func<Vector2, Matrix4x4> jitteredFunc)
        {
            AntialiasingModel.TaaSettings taaSettings = model.settings.taaSettings;
            var camera = context.camera;
            var eye = (Camera.StereoscopicEye)camera.stereoActiveEye;
            jitterVector = GenerateRandomOffset();
            jitterVector *= taaSettings.jitterSpread;

            // This saves off the device generated projection matrices as non-jittered
            context.camera.CopyStereoDeviceProjectionMatrixToNonJittered(eye);
            var originalProj = context.camera.GetStereoNonJitteredProjectionMatrix(eye);

            // Currently no support for custom jitter func, as VR devices would need to provide
            // original projection matrix as input along with jitter 
            var jitteredMatrix = PostProcessingUtils.GenerateJitteredProjectionMatrixFromOriginal(context, originalProj, jitterVector);
            context.camera.SetStereoProjectionMatrix(eye, jitteredMatrix);

            // jitter has to be scaled for the actual eye texture size, not just the intermediate texture size
            // which could be double-wide in certain stereo rendering scenarios
            jitterVector = new Vector2(jitterVector.x / context.width, jitterVector.y / context.height);
            camera.useJitteredProjectionMatrixForTransparentRendering = false;
        }

        void GenerateHistoryName(RenderTexture rt, int id)
        {
            rt.name = "Temporal Anti-aliasing History id #" + id;

            if (context.camera.stereoEnabled)
                rt.name += " for eye " + context.camera.stereoActiveEye;
        }

        RenderTexture CheckHistory(int activeEye, int id, RenderTexture source, Material blitMaterial)
        {
            if (m_HistoryTextures[activeEye] == null)
                m_HistoryTextures[activeEye] = new RenderTexture[k_NumHistoryTextures];

            var rt = m_HistoryTextures[activeEye][id];

            if (m_ResetHistory || rt == null || rt.width != source.width || rt.height != source.height)
            {
                RenderTexture.ReleaseTemporary(rt);

                rt = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
                GenerateHistoryName(rt, id);

                rt.filterMode = FilterMode.Bilinear;
                m_HistoryTextures[activeEye][id] = rt;
                Graphics.Blit(source, rt, blitMaterial, 2);
            }

            return m_HistoryTextures[activeEye][id];
        }

        public void Render(RenderTexture source, RenderTexture destination)
        {
            ApplyProjectionMatrices();

            Material material = context.materialFactory.Get(k_ShaderString);
            material.shaderKeywords = null;
            AntialiasingModel.TaaSettings taaSettings = model.settings.taaSettings;

            int activeEye = (int)context.camera.stereoActiveEye % k_NumEyes;
            int pp = m_HistoryPingPong[activeEye];
            var historyRead = CheckHistory(activeEye, ++pp % k_NumEyes, source, material);
            var historyWrite = CheckHistory(activeEye, ++pp % k_NumEyes, source, material);
            m_HistoryPingPong[activeEye] = ++pp % k_NumEyes;

            material.SetVector(Uniforms._Jitter, jitterVector);
            material.SetVector(Uniforms._SharpenParameters, new Vector4(VHVRConfig.GetTaaSharpenAmmount(), 0f, 0f, 0f));
            material.SetVector(Uniforms._FinalBlendParameters, new Vector4(taaSettings.stationaryBlending, taaSettings.motionBlending, 6000f, 0f));
            material.SetTexture(Uniforms._MainTex, source);
            material.SetTexture(Uniforms._HistoryTex, historyRead);

            m_Mrt[0] = destination.colorBuffer;
            m_Mrt[1] = historyWrite.colorBuffer;
            Graphics.SetRenderTarget(m_Mrt, source.depthBuffer);
            GraphicsUtils.Blit(material, context.camera.orthographic ? 1 : 0);
            m_ResetHistory = false;
        }

        private float GetHaltonValue(int index, int radix)
        {
            float num = 0f;
            float num2 = 1f / (float)radix;
            while (index > 0)
            {
                num += (float)(index % radix) * num2;
                index /= radix;
                num2 /= (float)radix;
            }
            return num;
        }

        Vector2 GenerateRandomOffset()
        {
            // The variance between 0 and the actual halton sequence values reveals noticeable instability
            // in Unity's shadow maps, so we avoid index 0.
            var offset = new Vector2(
                    HaltonSeq.Get((sampleIndex & 1023) + 1, 2) - 0.5f,
                    HaltonSeq.Get((sampleIndex & 1023) + 1, 3) - 0.5f
                );

            if (++sampleIndex >= k_SampleCount)
                sampleIndex = 0;

            return offset;
        }

        public override void OnDisable()
        {
            if (m_HistoryTextures != null)
            {
                for (int i = 0; i < m_HistoryTextures.Length; i++)
                {
                    if (m_HistoryTextures[i] == null)
                        continue;

                    for (int j = 0; j < m_HistoryTextures[i].Length; j++)
                    {
                        RenderTexture.ReleaseTemporary(m_HistoryTextures[i][j]);
                        m_HistoryTextures[i][j] = null;
                    }

                    m_HistoryTextures[i] = null;
                }
            }

            sampleIndex = 0;
            m_HistoryPingPong[0] = 0;
            m_HistoryPingPong[1] = 0;
            ResetHistory();
            context.camera.useJitteredProjectionMatrixForTransparentRendering = true;
            base.OnDisable();
        }
    }
}