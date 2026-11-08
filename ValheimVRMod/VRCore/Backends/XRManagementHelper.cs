using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using ValheimVRMod.Utilities;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.Backends
{
    // The XR Plug-in Management steps that every runtime's loader goes through the same way.
    static class XRManagementHelper
    {
        // Initializes the manager's loaders, trying again up to MaxVRInitializationTries times, since a runtime that
        // is still starting up can fail the first attempts.
        public static bool InitializeLoaders(XRManagerSettings managerSettings)
        {
            LogDebug("Initializing XRSDK Loaders...");
            if (managerSettings == null)
            {
                LogError("XRManagerSettings instance is null, cannot initialize loader.");
                return false;
            }
            int tries = 0;
            do
            {
                managerSettings.InitializeLoaderSync();
                tries++;
            } while ((managerSettings.activeLoader == null) && (tries < VHVRConfig.MaxVRInitializationTries()));
            if (managerSettings.activeLoader == null)
            {
                LogError("managerSettings.activeLoader is null after " + tries + " tries.");
                return false;
            }
            LogDebug("Got non-null Active Loader.");
            return true;
        }

        public static bool StartSubsystems()
        {
            LogInfo("Starting XRSDK!");
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null
                && XRGeneralSettings.Instance.Manager.activeLoader != null)
            {
                XRGeneralSettings.Instance.Manager.StartSubsystems();
                return true;
            }
            LogError("Error Starting XRSDK!");
            if (XRGeneralSettings.Instance == null)
            {
                LogError("XRGeneralSettings Instance is null!");
            }
            else if (XRGeneralSettings.Instance.Manager == null)
            {
                LogError("XRManager instance is null!");
            }
            else
            {
                LogError("Active loader is null!");
            }
            return false;
        }

        public static void Recenter()
        {
            if (VRPlayer.ShouldPauseMovement)
            {
                return;
            }

            List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetInstances(inputSubsystems);
            foreach (var subsystem in inputSubsystems)
            {
                LogDebug("Recentering Input Subsystem: " + subsystem);
                subsystem.TryRecenter();
            }

            // Trigger recentering head position on player body
            VRPlayer.RequestRecentering(recaliberateHeight: true);
            VRPlayer.RequestPelvisCaliberation();
            VRPlayer.vrPlayerInstance?.ResetRoomscaleCamera();
        }
    }
}
