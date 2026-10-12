using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Valve.VR;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.Backends
{
    // Recenters the VR player after the tracking origin was moved from outside the mod: SteamVR's reset view, a long
    // press of the Oculus button, or the headset re-establishing its floor after it lost tracking. The mod's own
    // recentering only ever ran on the player's request (VRManager.tryRecenter), so such a move stayed as a shift
    // of the view and the body until the next one: a sideways jump read as roomscale walking, a yaw jump turned the
    // body, and a change of the floor height held, with the view too high or too low and the roomscale sneak and
    // sitting heights off.
    //
    // SteamVR reports such a move as a chaperone universe change or a zero pose reset, Unity's input subsystem as a
    // tracking origin update. Each is logged, since which of them a given headset and runtime raise is not known
    // beforehand. Any of them asks for the same recentering the player can ask for, applied from VRPlayer.Update()
    // as soon as movement is not paused (barber station, pause menu), rather than dropped like tryRecenter() drops
    // a request made then.
    static class TrackingOriginWatcher
    {
        // How long after a recentering, the mod's own included, further reports are taken to be echoes of it: the
        // mod's recentering resets the tracking origin itself, which raises the very same events.
        private const float ECHO_SECONDS = 0.5f;
        private const float SUBSYSTEM_CHECK_INTERVAL = 2f;

        private static readonly EVREventType[] EVENT_TYPES = new EVREventType[] {
            EVREventType.VREvent_ChaperoneUniverseHasChanged,
            EVREventType.VREvent_SeatedZeroPoseReset,
            EVREventType.VREvent_StandingZeroPoseReset,
        };

        private static bool listening;
        private static readonly List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
        private static readonly HashSet<XRInputSubsystem> watchedSubsystems = new HashSet<XRInputSubsystem>();
        private static float nextSubsystemCheck;
        private static string pendingReason;
        private static float lastRecenterTime = float.NegativeInfinity;

        public static void Start()
        {
            if (listening)
            {
                return;
            }
            listening = true;
            foreach (EVREventType eventType in EVENT_TYPES)
            {
                SteamVR_Events.System(eventType).Listen(OnSteamVREvent);
            }
            WatchInputSubsystems();
            LogInfo("VHVR: watching the tracking origin for recenters made outside the mod.");
        }

        // To be called by whatever recenters the player on its own, so that the events it raises are not acted on.
        public static void NoteRecentering()
        {
            lastRecenterTime = Time.unscaledTime;
            pendingReason = null;
        }

        // To be called every frame from VRPlayer.Update(). Applies a pending recentering once it can be applied.
        public static void Update()
        {
            if (!listening)
            {
                return;
            }
            if (Time.unscaledTime >= nextSubsystemCheck)
            {
                // Subsystems may come up after Start().
                WatchInputSubsystems();
            }
            if (pendingReason == null || !VRPlayer.attachedToPlayer || VRPlayer.ShouldPauseMovement)
            {
                return;
            }
            string reason = pendingReason;
            NoteRecentering();
            LogInfo("VHVR: the tracking origin was recentered outside the mod (" + reason + "), recentering the VR player.");
            VRPlayer.RequestRecentering(recaliberateHeight: true);
            VRPlayer.RequestPelvisCaliberation();
            VRPlayer.vrPlayerInstance?.ResetRoomscaleCamera();
        }

        private static void WatchInputSubsystems()
        {
            nextSubsystemCheck = Time.unscaledTime + SUBSYSTEM_CHECK_INTERVAL;
            inputSubsystems.Clear();
            SubsystemManager.GetInstances(inputSubsystems);
            foreach (XRInputSubsystem subsystem in inputSubsystems)
            {
                if (subsystem != null && watchedSubsystems.Add(subsystem))
                {
                    subsystem.trackingOriginUpdated += OnTrackingOriginUpdated;
                }
            }
        }

        private static void OnSteamVREvent(VREvent_t args)
        {
            Report(((EVREventType)args.eventType).ToString());
        }

        private static void OnTrackingOriginUpdated(XRInputSubsystem subsystem)
        {
            Report("XRInputSubsystem.trackingOriginUpdated, mode " + subsystem.GetTrackingOriginMode());
        }

        private static void Report(string reason)
        {
            if (Time.unscaledTime - lastRecenterTime < ECHO_SECONDS)
            {
                LogDebug("VHVR: tracking origin event " + reason + " right after a recentering, ignored.");
                return;
            }
            if (!VRPlayer.attachedToPlayer)
            {
                // Attaching to the player initializes the head position anyway, and the standing height is still to be
                // sampled on the first attachment.
                LogInfo("VHVR: tracking origin event " + reason + " while not attached to the player, nothing to do.");
                return;
            }
            if (pendingReason == null)
            {
                pendingReason = reason;
            }
        }
    }
}
