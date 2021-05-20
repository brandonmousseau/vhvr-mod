using UnityEngine;

namespace ValheimVRMod.Utilities
{
    static class LogUtils
    {
        private static string TAG = "[ValheimVRMod] ";

        public static void LogError(string message)
        {
            Debug.LogError(TAG + message);
        }

        public static void LogInfo(string message)
        {
            Debug.Log(TAG + message);
        }

        public static void LogWarning(string message)
        {
            Debug.LogWarning(TAG + message);
        }

        public static void LogDebug(string message)
        {
#if DEBUG
            Debug.Log(TAG + " [DEBUG] " + message);
#endif
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


        }
        
        /**
         * Logs all Components of a given object
         */
        public static void LogComponents(Transform obj)
        {
            
            string output = "LOG COMPONENTS OF " + obj + ": ";
            output += getComponentTypes(obj);
            LogDebug(output);
            
        }
        
        /**
         * Logs the all Child Objects and their Component Names of a given parent object
         */
        public static void LogChildTree(Transform obj, bool withComponents = true)
        {
            LogDebug("LOG CHILD TREE:");
            loopChildren(obj, withComponents);
        }
        
        /**
         * Logs all Components in Parents of a given object
         */
        public static void LogParentComponents(Transform obj)
        {
            
            string output = "LOG PARENT COMPONENTS OF " + obj + ": ";
            Component[] components = obj.GetComponentsInParent(typeof(Component));
            for (int i = 0; i < components.Length; i++)
            {
                output += components[i].GetType() + ";";
            }
            LogDebug(output);
            
        }
        
        private static string getComponentTypes(Transform obj)
        {
            string output = "";
            
            Component[] components = obj.GetComponents(typeof(Component));
            for (int i = 0; i < components.Length; i++)
            {
                output += components[i].GetType() + ";";
            }

            return output;
        }

        private static void loopChildren(Transform obj, bool withComponents = false, int spaceOffset = 0)
        {
            
            string msg = "";
                
            for (int i = 0; i < spaceOffset; i++)
            {
                msg += "   |";
            }
            
            msg += obj.name;

            if (withComponents)
            {
                msg += " (" + getComponentTypes(obj) + ")";
            }
                
            LogDebug(msg);

            for (int i = 0; i < obj.childCount; i++)
            {
                loopChildren(obj.GetChild(i), withComponents, spaceOffset++);
            }
        }
    }
}
