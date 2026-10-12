using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    /**
     * Moves GUI canvases that VRGUI doesn't know by name onto the VR GUI panel.
     *
     * VRGUI finds the vanilla canvases by name and turns them into world space canvases drawn by the GUI camera,
     * which is what puts them on the panel and lets the laser pointer reach them: the EventSystem raycasts each
     * canvas through its worldCamera at the simulated cursor position. A root canvas that some other mod creates
     * in screen space is missed by that, so it is only drawn on the desktop and nothing on it can be clicked in VR.
     * (A canvas parented under a vanilla canvas, e.g. most of Epic Loot's windows, is a nested canvas that follows
     * its root and needs nothing from here.)
     *
     * This adopts any root canvas that takes pointer input, i.e. has a GraphicRaycaster, and is still in screen
     * space, and lines it up with the vanilla GUI canvas so it lands on the panel exactly where it would be on the
     * desktop. Canvases are picked up whenever they appear, not only once, since mods often create their windows
     * on demand. Mods that want to be explicit can use VHVRGuiCompat instead.
     */
    class ForeignCanvasAdopter
    {
        private const float SCAN_INTERVAL = 0.5f;

        private readonly Camera guiCamera;
        // The vanilla GUI canvas that adopted canvases are lined up with. It can be replaced when the scene changes.
        private readonly Func<Canvas> getReferenceCanvas;
        // Canvases VRGUI already handles itself, which are not to be touched here.
        private readonly Func<Canvas, bool> isHandledByVrGui;
        private readonly HashSet<Canvas> adoptedCanvases = new HashSet<Canvas>();
        private float nextScanTime;

        // Whether any adopted canvas currently shows something the player can click, see IsClickableGuiOpen.
        public bool isShowingSelectable { get; private set; }

        private Canvas referenceCanvas { get { return getReferenceCanvas(); } }

        public ForeignCanvasAdopter(Camera guiCamera, Func<Canvas> getReferenceCanvas, Func<Canvas, bool> isHandledByVrGui)
        {
            this.guiCamera = guiCamera;
            this.getReferenceCanvas = getReferenceCanvas;
            this.isHandledByVrGui = isHandledByVrGui;
        }

        public void Update()
        {
            if (!VHVRConfig.AdoptForeignGuiCanvases() || referenceCanvas == null || Time.unscaledTime < nextScanTime)
            {
                return;
            }
            nextScanTime = Time.unscaledTime + SCAN_INTERVAL;

            adoptedCanvases.RemoveWhere(canvas => canvas == null);
            foreach (GraphicRaycaster raycaster in GameObject.FindObjectsOfType<GraphicRaycaster>())
            {
                Canvas canvas = raycaster.GetComponent<Canvas>()?.rootCanvas;
                if (shouldAdopt(canvas))
                {
                    adopt(canvas);
                }
            }
            foreach (Canvas canvas in VHVRGuiCompat.registeredCanvases)
            {
                if (canvas != null && !adoptedCanvases.Contains(canvas) && !isHandledByVrGui(canvas))
                {
                    adopt(canvas);
                }
            }

            bool showingSelectable = false;
            foreach (Canvas canvas in adoptedCanvases)
            {
                // A mod may switch its canvas back to screen space, e.g. when it rebuilds its window.
                if (canvas.renderMode != RenderMode.WorldSpace || canvas.worldCamera != guiCamera)
                {
                    adopt(canvas);
                }
                else
                {
                    // Children added since the last scan may have come with their own layer.
                    setCanvasLayers(canvas);
                }
                showingSelectable |= canvas.isActiveAndEnabled && canvas.GetComponentInChildren<Selectable>() != null;
            }
            isShowingSelectable = showingSelectable;
        }

        // Called by VRGUI when the panel resolution changes.
        public void Resize()
        {
            if (referenceCanvas == null)
            {
                return;
            }
            foreach (Canvas canvas in adoptedCanvases)
            {
                if (canvas != null)
                {
                    alignWithReferenceCanvas(canvas);
                }
            }
        }

        private bool shouldAdopt(Canvas canvas)
        {
            return canvas != null &&
                canvas.renderMode != RenderMode.WorldSpace &&
                !adoptedCanvases.Contains(canvas) &&
                !isHandledByVrGui(canvas) &&
                !VHVRConfig.GetForeignGuiCanvasBlacklist().Contains(canvas.name);
        }

        private void adopt(Canvas canvas)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = guiCamera;
            alignWithReferenceCanvas(canvas);
            setCanvasLayers(canvas);
            if (adoptedCanvases.Add(canvas))
            {
                LogInfo("Moved GUI canvas onto the VR GUI panel: " + getPath(canvas.transform));
            }
        }

        // Puts the canvas where the vanilla GUI canvas is, at the same size, so that the GUI camera sees it and
        // its layout comes out the same as on the desktop.
        private void alignWithReferenceCanvas(Canvas canvas)
        {
            Transform reference = referenceCanvas.transform;
            RectTransform rectTransform = canvas.GetComponent<RectTransform>();
            rectTransform.SetPositionAndRotation(reference.position, reference.rotation);
            Vector3 parentScale = rectTransform.parent == null ? Vector3.one : rectTransform.parent.lossyScale;
            rectTransform.localScale = new Vector3(
                reference.lossyScale.x / parentScale.x,
                reference.lossyScale.y / parentScale.y,
                reference.lossyScale.z / parentScale.z);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, VRGUI.GUI_DIMENSIONS.x);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, VRGUI.GUI_DIMENSIONS.y);
        }

        // The GUI camera only draws the layer of the vanilla GUI canvas, and a world space canvas is drawn by the
        // cameras that see the layer of its own (root or nested) canvas game object.
        private void setCanvasLayers(Canvas canvas)
        {
            int layer = referenceCanvas.gameObject.layer;
            foreach (Canvas c in canvas.GetComponentsInChildren<Canvas>(includeInactive: true))
            {
                c.gameObject.layer = layer;
            }
        }

        private static string getPath(Transform t)
        {
            return t.parent == null ? t.name : getPath(t.parent) + "/" + t.name;
        }
    }

    /**
     * For other mods that want to work with the VR GUI without depending on VHVR's heuristics. Call it through
     * reflection or a soft dependency, e.g.:
     *
     *   Type compat = Type.GetType("ValheimVRMod.VRCore.UI.VHVRGuiCompat, ValheimVRMod");
     *   compat?.GetMethod("RegisterCanvas")?.Invoke(null, new object[] { myCanvas });
     *   compat?.GetMethod("RegisterIsOpen")?.Invoke(null, new object[] { (Func<bool>)MyWindow.IsVisible });
     */
    public static class VHVRGuiCompat
    {
        internal static readonly List<Canvas> registeredCanvases = new List<Canvas>();
        private static readonly List<Func<bool>> isOpenChecks = new List<Func<bool>>();

        // Puts the given root canvas onto the VR GUI panel even if it has no GraphicRaycaster or is blacklisted.
        public static void RegisterCanvas(Canvas canvas)
        {
            if (canvas != null && !registeredCanvases.Contains(canvas))
            {
                registeredCanvases.Add(canvas);
            }
        }

        // Tells VHVR when a window of the calling mod is open, so that VHVR treats the laser pointer trigger as a
        // click on it rather than as an attack, building, etc. like it does for the vanilla menus.
        public static void RegisterIsOpen(Func<bool> isOpen)
        {
            if (isOpen != null)
            {
                isOpenChecks.Add(isOpen);
            }
        }

        internal static bool isAnyRegisteredGuiOpen
        {
            get
            {
                foreach (Func<bool> isOpen in isOpenChecks)
                {
                    try
                    {
                        if (isOpen())
                        {
                            return true;
                        }
                    }
                    catch (Exception e)
                    {
                        LogError("A GUI open check registered with VHVRGuiCompat failed: " + e);
                    }
                }
                return false;
            }
        }
    }
}
