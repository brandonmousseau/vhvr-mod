using System.Collections.Generic;
using static ValheimVRMod.Utilities.LogUtils;

using UnityEngine;

namespace ValheimVRMod.Utilities
{
    static class CameraUtils
    {

        public const string MAIN_CAMERA = "Main Camera";
        public const string VR_CAMERA = "VRCamera";
        public const string VR_UI_CAMERA = "VRUICamera";
        public const string HANDS_CAMERA = "VRHandsCamera";
        public const string SKYBOX_CAMERA = "SkyboxCamera";
        public const string VRSKYBOX_CAMERA = "VRSkyboxCamera";
        public const string VRGUI_SCREENSPACE_CAM = "VRGuiScreenSpace";
        public const string WORLD_SPACE_UI_CAMERA = "WorldSpaceUiCamera";

        private static Camera _worldSpaceUiCamera;
        private static Dictionary<string, Camera> _cameraCache = new Dictionary<string, Camera>();
        private static int worldSpaceUiDepth = 2;

        public static void copyCamera(Camera from, Camera to)
        {
            if (from == null)
            {
                LogError("\"from\" camera is null!");
                return;
            }
            if (to == null)
            {
                LogError("\"to\" camera is null!");
                return;
            }
            to.farClipPlane = from.farClipPlane;
            to.clearFlags = from.clearFlags;
            to.renderingPath = from.renderingPath;
            to.clearStencilAfterLightingPass = from.clearStencilAfterLightingPass;
            to.depthTextureMode = from.depthTextureMode;
            to.layerCullDistances = from.layerCullDistances;
            to.layerCullSpherical = from.layerCullSpherical;
            to.cullingMask = from.cullingMask;
            to.useOcclusionCulling = from.useOcclusionCulling;
            to.allowHDR = false; // Force this to off for VR
            to.backgroundColor = from.backgroundColor;
        }

        public static Camera getWorldspaceUiCamera()
        {
            if (_worldSpaceUiCamera != null)
            {
                return _worldSpaceUiCamera;
            }
            Camera vrCam = getCamera(VR_CAMERA);
            if (vrCam == null || vrCam.gameObject == null)
            {
                LogWarning("VR Camera is null while creating world space UI camera.");
                return null;
            }
            GameObject worldSpaceUiCamParent = new GameObject(WORLD_SPACE_UI_CAMERA);
            worldSpaceUiCamParent.transform.SetParent(vrCam.transform);
            _worldSpaceUiCamera = worldSpaceUiCamParent.AddComponent<Camera>();
            _worldSpaceUiCamera.CopyFrom(vrCam);
            _worldSpaceUiCamera.clearFlags = CameraClearFlags.Depth;
            _worldSpaceUiCamera.depth = worldSpaceUiDepth;
            _worldSpaceUiCamera.renderingPath = RenderingPath.Forward;
            _worldSpaceUiCamera.cullingMask = LayerUtils.WORLDSPACE_UI_LAYER_MASK;
            _worldSpaceUiCamera.enabled = true;
            return _worldSpaceUiCamera;
        }

        public static Camera getCamera(string name)
        {
            //Check cache
            if(_cameraCache.ContainsKey(name) && _cameraCache[name] != null) return _cameraCache[name];

            //Update cache
            foreach (var c in GameObject.FindObjectsOfType<Camera>())
            {
                if (c.name == name)
                {
                    _cameraCache.Remove(name);
                    _cameraCache.Add(name, c);
                    return c;
                }
            }

            return null;
        }


        public static void PrintCamera(Camera c)
        {
            if (c == null)
            {
                LogWarning("Null camera, cannot print properties!");
                return;
            }
            LogDebug("Camera: " + c.name);
            LogDebug("  activeTexture: " + c.activeTexture);
            LogDebug("  actualRenderingPath: " + c.actualRenderingPath);
            LogDebug("  allowDynamicResolution: " + c.allowDynamicResolution);
            LogDebug("  allowHDR: " + c.allowHDR);
            LogDebug("  allowMSAA: " + c.allowMSAA);
            LogDebug("  areVRStereoViewMatricesWithinSingleCullTolerance: " + c.areVRStereoViewMatricesWithinSingleCullTolerance);
            LogDebug("  aspect: " + c.aspect);
            LogDebug("  backgroundColor: " + c.backgroundColor);
            LogDebug("  cameraToWorldMatrix: " + c.cameraToWorldMatrix);
            LogDebug("  cameraType: " + c.cameraType);
            LogDebug("  clearFlags: " + c.clearFlags);
            LogDebug("  clearStencilAfterLightingPass: " + c.clearStencilAfterLightingPass);
            LogDebug("  commandBufferCount: " + c.commandBufferCount);
            LogDebug("  cullingMask: " + c.cullingMask);
            LogDebug("  cullingMatrix: " + c.cullingMatrix);
            LogDebug("  depth: " + c.depth);
            LogDebug("  depthTextureMode: " + c.depthTextureMode);
            LogDebug("  eventMask: " + c.eventMask);
            LogDebug("  farClipPlane: " + c.farClipPlane);
            LogDebug("  fieldOfView: " + c.fieldOfView);
            LogDebug("  focalLength: " + c.focalLength);
            LogDebug("  forceIntoRenderTexture: " + c.forceIntoRenderTexture);
            LogDebug("  getFit: " + c.gateFit);
            LogDebug("  layerCullDistances: " + c.layerCullDistances);
            LogDebug("  layerCullSpherical: " + c.layerCullSpherical);
            LogDebug("  lensShift: " + c.lensShift);
            LogDebug("  nearClipPlane: " + c.nearClipPlane);
            LogDebug("  nonJitteredProjectionMatrix: " + c.nonJitteredProjectionMatrix);
            LogDebug("  opaqueSortMode: " + c.opaqueSortMode);
            LogDebug("  orthographic: " + c.orthographic);
            LogDebug("  orthographicSize: " + c.orthographicSize);
            LogDebug("  orverrideSceneCullingMask: " + c.overrideSceneCullingMask);
            LogDebug("  pixelHeight: " + c.pixelHeight);
            LogDebug("  pixelRect: " + c.pixelRect);
            LogDebug("  pixelWidth: " + c.pixelWidth);
            LogDebug("  previousViewProjectionMatrix: " + c.previousViewProjectionMatrix);
            LogDebug("  projectionMatrix: " + c.projectionMatrix);
            LogDebug("  rect: " + c.rect);
            LogDebug("  renderingPath: " + c.renderingPath);
            LogDebug("  scaledPixelHeight: " + c.scaledPixelHeight);
            LogDebug("  scaledPixelWidth: " + c.scaledPixelWidth);
            LogDebug("  scene: " + c.scene);
            LogDebug("  sensorSize: " + c.sensorSize);
            LogDebug("  stereoActiveEye: " + c.stereoActiveEye);
            LogDebug("  stereoConvergence: " + c.stereoConvergence);
            LogDebug("  stereoEnabled: " + c.stereoEnabled);
            LogDebug("  stereoSeparation: " + c.stereoSeparation);
            LogDebug("  stereoTargetEye: " + c.stereoTargetEye);
            LogDebug("  targetDisplay: " + c.targetDisplay);
            LogDebug("  targetTexture: " + c.targetTexture);
            LogDebug("  transparencySortAxis: " + c.transparencySortAxis);
            LogDebug("  transparencySortMode: " + c.transparencySortMode);
            LogDebug("  useJitteredProjectionMatrixForTransparentRendering: "
                + c.useJitteredProjectionMatrixForTransparentRendering);
            LogDebug("  useOcclusionCulling: " + c.useOcclusionCulling);
            LogDebug("  usePhysicalProperties: " + c.usePhysicalProperties);
            LogDebug("  velocity: " + c.velocity);
            LogDebug("  worldToCameraMatrix: " + c.worldToCameraMatrix);
            var skybox = c.GetComponent<Skybox>();
            if (skybox != null)
            {
                LogDebug("Skybox : " + skybox.name + "  Material: " + skybox.material);
            }
        }

    }
}
