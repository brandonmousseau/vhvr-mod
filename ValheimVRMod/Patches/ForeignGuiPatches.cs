using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Patches
{
    // Code written for a screen space overlay canvas correctly passes no camera to RectTransformUtility, e.g.
    // RectangleContainsScreenPoint(rect, Input.mousePosition, null) to test whether the cursor is over a window. On
    // the VR GUI panel every GUI canvas is a world space canvas drawn by the GUI camera instead, so without a camera
    // such tests compare the cursor against raw world coordinates and miss (or hit) by however far the canvas is
    // from the origin. This hands them the GUI camera for any rect on a canvas it draws, which covers vanilla code
    // as well as other mods' and makes all of them agree with what the EventSystem considers hovered.
    [HarmonyPatch]
    static class RectTransformUtility_NullCamera_Patch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(RectTransformUtility)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method =>
                    (method.Name == nameof(RectTransformUtility.RectangleContainsScreenPoint) ||
                        method.Name == nameof(RectTransformUtility.ScreenPointToLocalPointInRectangle) ||
                        method.Name == nameof(RectTransformUtility.ScreenPointToWorldPointInRectangle)) &&
                    method.GetParameters().Any(parameter => parameter.ParameterType == typeof(Camera)));
        }

        static void Prefix(RectTransform rect, ref Camera cam)
        {
            if (cam != null || rect == null || VHVRConfig.NonVrPlayer())
            {
                return;
            }
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }
            Canvas root = canvas.rootCanvas;
            if (root.renderMode == RenderMode.WorldSpace && root.worldCamera != null &&
                root.worldCamera.name == CameraUtils.VRGUI_SCREENSPACE_CAM)
            {
                cam = root.worldCamera;
            }
        }
    }
}
