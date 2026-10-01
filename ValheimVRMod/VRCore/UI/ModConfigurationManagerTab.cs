using TMPro;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.VRCore.UI
{
    /**
     * The Mods tab of the VHVR settings dialog. The tab itself is empty: selecting it opens the ConfigurationManager
     * window, on the VR GUI in VR (see ModConfigurationManagerPanel) and as usual in flatscreen. Leaving the tab or hiding the
     * dialog keeps the window open. It is closed by the tab's button, from within, or by the ToggleMenu action (see
     * VRGUI.Update()).
     *
     * The window opened any other way, i. e. with its hotkey, is left to the plugin, which draws it on the desktop
     * only. The tab's button closes it whichever way it was opened, which is the way out of a window that can't be
     * seen in the headset, and opens it on the VR GUI otherwise.
     */
    class ModConfigurationManagerTab : MonoBehaviour
    {
        private const string OPEN_LABEL = "Open mod configuration manager";
        private const string CLOSE_LABEL = "Close mod configuration manager";

        public TMP_Text statusText;
        public TMP_Text buttonLabel;

        private ModConfigurationManagerPanel panel;

        public void ToggleOpen()
        {
            if (ModConfigurationManagerBridge.isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (!ModConfigurationManagerBridge.IsAvailable)
            {
                SetStatus("ConfigurationManager is not available.");
                return;
            }
            if (ModConfigurationManagerBridge.isOpen && (panel == null || !panel.ownsWindow))
            {
                // Opened with its hotkey, which keeps it the plugin's.
                return;
            }

            if (VHVRConfig.NonVrPlayer())
            {
                ModConfigurationManagerBridge.SetOpen(true);
                SetStatus(null);
                return;
            }

            if (panel == null)
            {
                panel = ModConfigurationManagerPanel.Create(transform);
            }
            if (panel == null)
            {
                SetStatus("The mod configuration manager can't be shown in VR with this version of ConfigurationManager.");
                return;
            }
            panel.Show();
            SetStatus(panel.ownsWindow ? null : "The mod configuration manager failed to open.");
        }

        // Closes the window however it was opened.
        public void Close()
        {
            if (panel != null)
            {
                panel.Hide();
            }
            ModConfigurationManagerBridge.CloseWindow();
        }

        private void OnEnable()
        {
            Open();
        }

        private void OnDisable()
        {
            if (panel != null)
            {
                panel.Suspend();
            }
        }

        private void Update()
        {
            if (buttonLabel != null)
            {
                string label = ModConfigurationManagerBridge.isOpen ? CLOSE_LABEL : OPEN_LABEL;
                if (buttonLabel.text != label)
                {
                    buttonLabel.text = label;
                }
            }
        }

        private void OnDestroy()
        {
            if (panel != null)
            {
                // Not a child of the tab, see ModConfigurationManagerPanel.Create().
                Destroy(panel.gameObject);
            }
        }

        private void SetStatus(string status)
        {
            if (statusText != null)
            {
                statusText.text = status ?? "";
            }
        }
    }
}
