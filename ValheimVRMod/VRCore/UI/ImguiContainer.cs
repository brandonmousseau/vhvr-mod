using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    /**
     * Runs an IMGUI draw function on events of our choosing, the way UI Toolkit's IMGUIContainer embeds IMGUI in a
     * panel (see UIElementsUtility.BeginContainerGUI() and IMGUIContainer.DoOnGUI()).
     *
     * A plain OnGUI() can't be fed the VR laser pointer: IMGUI only gets events from the OS, and it keeps its own
     * idea of where the mouse is, which the Input.mousePosition patch does not reach. Swapping in a made up
     * Event.current in the middle of an OnGUI() is not enough either, since that event is shared with every other
     * OnGUI() in the same pass, and control ids and GUIClip's mouse position are set up once for the whole OnGUI()
     * call. A container has its own control id list and GUILayout cache and sets all of it up again from the given
     * event, so several events can be run one after another within a single OnGUI().
     *
     * The entry points used are internal to Unity, so they are bound by reflection once, and isAvailable turns false
     * if any of the required ones can't be found in the Unity version the game ships with.
     */
    class ImguiContainer : IDisposable
    {
        private static bool initialized;
        private static bool available;
        private static ConstructorInfo objectGuiStateConstructor;
        private static ConstructorInfo layoutCacheConstructor;
        private static MethodInfo beginContainer;
        private static MethodInfo beginLayoutContainer;
        private static Action endContainer;
        private static Action<float, float> layoutFromContainer;
        private static Action resetGlobalState;
        private static MethodInfo selectIdList;
        private static FieldInfo originalId;
        private static FieldInfo skinMode;
        private static Func<int> getClipCount;
        private static Action popClip;
        // IMGUI does not take the mouse position from the event, but from an absolute one it keeps, and works it out
        // again from that for every clip. It also tests that absolute position against the clips to tell whether the
        // mouse is inside them, e. g. for a scroll view. See Run() for how the parent clip lines it up with the event.
        private static Action<Matrix4x4, Matrix4x4, Rect> pushParentClip;
        private static Action popParentClip;
        private static Func<Vector2> getAbsoluteMousePosition;

        // Set while a draw function runs in a container, see ModConfigurationManagerPanel.GUI_DragWindow_Patch.
        public static bool isDrawing { get; private set; }

        public static bool isAvailable
        {
            get
            {
                initialize();
                return available;
            }
        }

        private readonly object guiState;
        private readonly object layoutCache;
        private readonly object[] guiStateArgument;
        private readonly object[] layoutCacheArgument;
        // Reported to IMGUI as the id of the object that owns the GUI, keys e. g. the GUILayout cache.
        private readonly int ownerId;

        public ImguiContainer(int ownerId)
        {
            if (!isAvailable)
            {
                throw new InvalidOperationException("IMGUI containers are not available in this Unity version.");
            }
            this.ownerId = ownerId;
            guiState = objectGuiStateConstructor.Invoke(null);
            layoutCache = layoutCacheConstructor.Invoke(new object[] { -1 });
            guiStateArgument = new object[] { guiState };
            layoutCacheArgument = new object[] { layoutCache };
        }

        public void Dispose()
        {
            (guiState as IDisposable)?.Dispose();
        }

        // Runs the draw function for the given event in a container of the given size. Must be called from within
        // an OnGUI(), and leaves the state of that OnGUI(), including Event.current, as it was. A Repaint is drawn
        // into the active render target, which should be of the given size.
        public void Run(Event evt, Vector2 size, Action draw)
        {
            Vector2 mousePosition = evt.mousePosition;
            Event outerEvent = Event.current;
            int outerOriginalId = (int)originalId.GetValue(null);
            int outerSkinMode = (int)skinMode.GetValue(null);
            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            Color contentColor = GUI.contentColor;
            Color backgroundColor = GUI.backgroundColor;
            bool enabled = GUI.enabled;
            bool changed = GUI.changed;
            int clipCount = getClipCount();

            beginContainer.Invoke(null, guiStateArgument);
            bool pushedMatrix = false;
            try
            {
                // 0 is ContextType.Player, i. e. the runtime skin rather than the editor's.
                skinMode.SetValue(null, 0);
                originalId.SetValue(null, ownerId);
                Event.current = evt;
                GUI.enabled = true;
                beginLayoutContainer.Invoke(null, layoutCacheArgument);
                resetGlobalState();

                // IMGUI's absolute mouse position is still the real mouse's, so the input transform shifts it by the
                // difference, which puts the mouse where the event says it is.
                Vector2 offset = getAbsoluteMousePosition() - mousePosition;
                Matrix4x4 inputTransform = Matrix4x4.Translate(offset);
                if (evt.type == EventType.Repaint)
                {
                    // Drawn where it is. Clips inside the window, e. g. scroll views, then test the real mouse for
                    // hover highlights, which is only cosmetic.
                    pushParentClip(Matrix4x4.identity, inputTransform, new Rect(Vector2.zero, size));
                    GL.PushMatrix();
                    pushedMatrix = true;
                    GL.LoadPixelMatrix(0, size.x, size.y, 0);
                }
                else
                {
                    // Nothing is drawn, so the whole window can be moved over by the difference too. Every clip in it
                    // then contains the absolute mouse position exactly when it contains the event's, which e. g. a
                    // scroll view checks before its content gets any mouse input.
                    pushParentClip(inputTransform, inputTransform, new Rect(offset, size));
                }
                isDrawing = true;
                try
                {
                    draw();
                }
                catch (ExitGUIException)
                {
                    // Thrown on purpose by GUIUtility.ExitGUI() to skip the rest of the event.
                }
                finally
                {
                    isDrawing = false;
                    popParentClip();
                }

                if (Event.current.type == EventType.Layout)
                {
                    layoutFromContainer(size.x, size.y);
                }
            }
            finally
            {
                if (pushedMatrix)
                {
                    GL.PopMatrix();
                }
                while (getClipCount() > clipCount)
                {
                    popClip();
                }
                endContainer();
                Event.current = outerEvent;
                originalId.SetValue(null, outerOriginalId);
                // Point GUILayout back at the layout of the OnGUI() this ran in, like EndContainerGUI() does.
                selectIdList?.Invoke(null, new object[] { outerOriginalId, false });
                skinMode.SetValue(null, outerSkinMode);
                GUI.matrix = matrix;
                GUI.color = color;
                GUI.contentColor = contentColor;
                GUI.backgroundColor = backgroundColor;
                GUI.enabled = enabled;
                GUI.changed = changed;
            }
        }

        private static void initialize()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            try
            {
                Assembly imgui = typeof(GUI).Assembly;
                Type objectGuiState = imgui.GetType("UnityEngine.ObjectGUIState");
                Type layoutCache = typeof(GUILayoutUtility).GetNestedType("LayoutCache", BindingFlags.NonPublic);
                Type guiClip = imgui.GetType("UnityEngine.GUIClip");
                if (objectGuiState == null || layoutCache == null || guiClip == null)
                {
                    LogWarning("Unity IMGUI internals not found, the mod configuration manager can't be shown in VR.");
                    return;
                }

                objectGuiStateConstructor = AccessTools.Constructor(objectGuiState, Type.EmptyTypes);
                layoutCacheConstructor = AccessTools.Constructor(layoutCache, new Type[] { typeof(int) });
                beginContainer = AccessTools.Method(typeof(GUIUtility), "BeginContainer", new Type[] { objectGuiState });
                beginLayoutContainer = AccessTools.Method(typeof(GUILayoutUtility), "BeginContainer", new Type[] { layoutCache });
                endContainer = createDelegate<Action>(typeof(GUIUtility), "EndContainer", Type.EmptyTypes);
                layoutFromContainer =
                    createDelegate<Action<float, float>>(
                        typeof(GUILayoutUtility), "LayoutFromContainer", new Type[] { typeof(float), typeof(float) });
                resetGlobalState = createDelegate<Action>(typeof(GUIUtility), "ResetGlobalState", Type.EmptyTypes);
                selectIdList =
                    AccessTools.Method(typeof(GUILayoutUtility), "SelectIDList", new Type[] { typeof(int), typeof(bool) });
                originalId = AccessTools.Field(typeof(GUIUtility), "s_OriginalID");
                skinMode = AccessTools.Field(typeof(GUIUtility), "s_SkinMode");
                getClipCount = createDelegate<Func<int>>(guiClip, "Internal_GetCount", Type.EmptyTypes);
                popClip = createDelegate<Action>(guiClip, "Internal_Pop", Type.EmptyTypes);
                pushParentClip =
                    createDelegate<Action<Matrix4x4, Matrix4x4, Rect>>(
                        guiClip, "Internal_PushParentClip", new Type[] { typeof(Matrix4x4), typeof(Matrix4x4), typeof(Rect) });
                popParentClip = createDelegate<Action>(guiClip, "Internal_PopParentClip", Type.EmptyTypes);
                getAbsoluteMousePosition = createDelegate<Func<Vector2>>(guiClip, "GetAbsoluteMousePosition", Type.EmptyTypes);

                available =
                    objectGuiStateConstructor != null &&
                    layoutCacheConstructor != null &&
                    beginContainer != null &&
                    beginLayoutContainer != null &&
                    endContainer != null &&
                    layoutFromContainer != null &&
                    resetGlobalState != null &&
                    originalId != null && originalId.FieldType == typeof(int) &&
                    skinMode != null && skinMode.FieldType == typeof(int) &&
                    getClipCount != null &&
                    popClip != null &&
                    pushParentClip != null &&
                    popParentClip != null &&
                    getAbsoluteMousePosition != null;
                if (!available)
                {
                    LogWarning("Some Unity IMGUI internals are missing, the mod configuration manager can't be shown in VR.");
                }
            }
            catch (Exception e)
            {
                available = false;
                LogError("Failed to look up Unity IMGUI internals, the mod configuration manager can't be shown in VR: " + e);
            }
        }

        private static T createDelegate<T>(Type type, string name, Type[] parameters) where T : class
        {
            MethodInfo method = AccessTools.Method(type, name, parameters);
            if (method == null || !method.IsStatic)
            {
                return null;
            }
            return Delegate.CreateDelegate(typeof(T), method, throwOnBindFailure: false) as T;
        }
    }
}
