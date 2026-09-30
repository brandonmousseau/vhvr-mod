using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    /**
     * Talks to BepInEx.ConfigurationManager (the in-game mod settings window toggled with F1), which is an optional
     * third party plugin that this mod has no compile time reference to. Everything is looked up by reflection
     * and checked once, so if the plugin is missing, or an update renames or retypes a member used here, the Mods
     * tab is simply not offered instead of throwing at the player.
     *
     * Opening the window in flatscreen just asks the plugin to show it. In VR, its window is drawn onto the VR GUI
     * by ModConfigurationManagerPanel, which needs the plugin's window function and a few of its layout properties on top of that.
     */
    static class ModConfigurationManagerBridge
    {
        private const string PLUGIN_GUID = "com.bepis.bepinex.configurationmanager";
        // Same window id the plugin uses for its own window.
        public const int WINDOW_ID = -68;

        private static bool initialized;
        private static BaseUnityPlugin plugin;
        private static PropertyInfo displayingWindowProperty;
        private static Action<int> settingsWindow;
        private static PropertyInfo settingWindowRectProperty;
        private static PropertyInfo leftColumnWidthProperty;
        private static PropertyInfo rightColumnWidthProperty;
        // Set once drawing the window in VR has failed, so that it is not attempted again with the same broken
        // plugin version.
        private static bool VrDrawingBroken;

        // Whether the plugin is installed and exposes what opening and closing its window needs.
        public static bool IsAvailable
        {
            get
            {
                Initialize();
                return plugin != null && displayingWindowProperty != null;
            }
        }

        // Whether its window can also be drawn onto the VR GUI.
        public static bool IsAvailableInVr
        {
            get
            {
                return IsAvailable && settingsWindow != null && !VrDrawingBroken && ImguiContainer.isAvailable;
            }
        }

        public static bool isOpen
        {
            get
            {
                if (!IsAvailable)
                {
                    return false;
                }
                try
                {
                    return (bool)displayingWindowProperty.GetValue(plugin, null);
                }
                catch (Exception e)
                {
                    LogError("Failed to read whether the mod configuration manager window is open: " + e);
                    return false;
                }
            }
        }

        public static void SetOpen(bool open)
        {
            if (!IsAvailable)
            {
                return;
            }
            try
            {
                displayingWindowProperty.SetValue(plugin, open, null);
            }
            catch (Exception e)
            {
                LogError("Failed to " + (open ? "open" : "close") + " the mod configuration manager window: " + e);
            }
        }

        // Closes the window however it was opened, and gives it back to the plugin in case ModConfigurationManagerPanel had it.
        public static void CloseWindow()
        {
            if (!IsAvailable)
            {
                return;
            }
            if (isOpen)
            {
                SetOpen(false);
            }
            SetPluginEnabled(true);
        }

        // Stops or resumes the plugin's own Update() and OnGUI(). While its window is drawn in VR, it must not also
        // draw it on the desktop, where it would react to the real mouse, which sits locked in the screen center.
        public static void SetPluginEnabled(bool enabled)
        {
            if (plugin != null)
            {
                plugin.enabled = enabled;
            }
        }

        // Lays the window out for a size other than the desktop screen's, which the plugin sizes it by whenever it
        // is opened. The window function reads the height to skip drawing the plugins scrolled out of view.
        public static void SetWindowSize(Vector2 size)
        {
            try
            {
                settingWindowRectProperty?.SetValue(plugin, new Rect(0, 0, size.x, size.y), null);
                // Same split as ConfigurationManager.CalculateWindowRect().
                int leftColumnWidth = Mathf.RoundToInt(size.x / 2.5f);
                leftColumnWidthProperty?.SetValue(plugin, leftColumnWidth, null);
                rightColumnWidthProperty?.SetValue(plugin, (int)size.x - leftColumnWidth - 115, null);
            }
            catch (Exception e)
            {
                LogWarning("Failed to resize the mod configuration manager window: " + e);
            }
        }

        // Draws the content of the plugin's window, i. e. what it would pass to GUILayout.Window().
        public static void DrawWindowContent()
        {
            settingsWindow(WINDOW_ID);
        }

        public static void OnVrDrawingFailed(Exception e)
        {
            if (VrDrawingBroken)
            {
                return;
            }
            VrDrawingBroken = true;
            LogError("Drawing the mod configuration manager window in VR failed, it will not be shown in VR anymore: " + e);
        }

        private static void Initialize()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            try
            {
                PluginInfo pluginInfo;
                if (!Chainloader.PluginInfos.TryGetValue(PLUGIN_GUID, out pluginInfo) || pluginInfo.Instance == null)
                {
                    LogDebug("ConfigurationManager is not installed, the Mods tab is disabled.");
                    return;
                }
                BaseUnityPlugin instance = pluginInfo.Instance;
                Type type = instance.GetType();

                PropertyInfo displayingWindow = AccessTools.Property(type, "DisplayingWindow");
                if (displayingWindow == null || displayingWindow.PropertyType != typeof(bool) ||
                    !displayingWindow.CanRead || !displayingWindow.CanWrite)
                {
                    LogWarning(
                        "ConfigurationManager " + pluginInfo.Metadata.Version +
                        " has no DisplayingWindow property, the Mods tab is disabled.");
                    return;
                }

                MethodInfo settingsWindowMethod =
                    AccessTools.Method(type, "SettingsWindow", new Type[] { typeof(int) });
                if (settingsWindowMethod != null && settingsWindowMethod.ReturnType == typeof(void))
                {
                    settingsWindow =
                        (Action<int>)Delegate.CreateDelegate(
                            typeof(Action<int>), instance, settingsWindowMethod, throwOnBindFailure: false);
                }
                if (settingsWindow == null)
                {
                    LogWarning(
                        "ConfigurationManager " + pluginInfo.Metadata.Version +
                        " has no SettingsWindow(int) method, the Mods tab will open it on the desktop only.");
                }

                settingWindowRectProperty = FindWritableProperty(type, "SettingWindowRect", typeof(Rect));
                leftColumnWidthProperty = FindWritableProperty(type, "LeftColumnWidth", typeof(int));
                rightColumnWidthProperty = FindWritableProperty(type, "RightColumnWidth", typeof(int));

                plugin = instance;
                displayingWindowProperty = displayingWindow;
                LogInfo("Found ConfigurationManager " + pluginInfo.Metadata.Version + " for the Mods tab.");
            }
            catch (Exception e)
            {
                plugin = null;
                displayingWindowProperty = null;
                settingsWindow = null;
                LogError("Failed to look up ConfigurationManager, the Mods tab is disabled: " + e);
            }
        }

        private static PropertyInfo FindWritableProperty(Type type, string name, Type propertyType)
        {
            PropertyInfo property = AccessTools.Property(type, name);
            if (property == null || property.PropertyType != propertyType || property.GetSetMethod(nonPublic: true) == null)
            {
                LogDebug("ConfigurationManager has no writable " + name + ", the window may be laid out for the desktop.");
                return null;
            }
            return property;
        }
    }
}
