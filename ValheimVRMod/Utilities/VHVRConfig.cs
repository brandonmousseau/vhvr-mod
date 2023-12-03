using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using NDesk.Options;
using Unity.XR.OpenVR;
using ValheimVRMod.VRCore;
using UnityEngine;

namespace ValheimVRMod.Utilities
{

    static class VHVRConfig {

        public static ConfigFile config;

        // Immutable Settings
        private static ConfigEntry<bool> vrModEnabled;
        private static ConfigEntry<bool> nonVrPlayer;
        private static ConfigEntry<bool> useVrControls;
        private static ConfigEntry<bool> useOverlayGui;
        private static ConfigEntry<string> pluginVersion;
        private static ConfigEntry<bool> bhapticsEnabled;

        // General Settings
        private static ConfigEntry<string> mirrorMode;
        private static ConfigEntry<float> headOffsetX;
        private static ConfigEntry<float> headOffsetZ;
        private static ConfigEntry<float> headOffsetY;
        private static ConfigEntry<float> headOffsetThirdPersonX;
        private static ConfigEntry<float> headOffsetThirdPersonZ;
        private static ConfigEntry<float> headOffsetThirdPersonY;
        private static ConfigEntry<bool> enableHeadReposition;
        private static ConfigEntry<bool> recenterOnStart;
        private static ConfigEntry<bool> roomscaleFadeToBlack;
        private static ConfigEntry<bool> disableRecenterPose;
        private static ConfigEntry<bool> immersiveShipCameraSitting;
        private static ConfigEntry<string> immersiveShipCameraStanding;
        private static ConfigEntry<bool> immersiveDodgeRoll;
        private static ConfigEntry<bool> allowMovementWhenInMenu;

        // UI Settings
        private static ConfigEntry<float> overlayCurvature;
        private static ConfigEntry<float> overlayWidth;
        private static ConfigEntry<float> overlayVerticalPosition;
        private static ConfigEntry<float> overlayDistance;
        private static ConfigEntry<float> uiPanelSize;
        private static ConfigEntry<Vector2> uiPanelResolution;
        private static ConfigEntry<bool> uiPanelResolutionCompat;
        private static ConfigEntry<float> uiPanelVerticalOffset;
        private static ConfigEntry<float> uiPanelDistance;
        private static ConfigEntry<bool> showStaticCrosshair;
        private static ConfigEntry<float> crosshairScale;
        private static ConfigEntry<bool> showRepairHammer;
        private static ConfigEntry<bool> showEnemyHuds;
        private static ConfigEntry<float> enemyHudScale;
        private static ConfigEntry<float> stationaryGuiRecenterAngle;
        private static ConfigEntry<float> mobileGuiRecenterAngle;
        private static ConfigEntry<bool> recenterGuiOnMove;
        private static ConfigEntry<int> guiRecenterSpeed;
        private static ConfigEntry<bool> unlockDesktopCursor;
        private static ConfigEntry<string> QuickMenuType;
        private static ConfigEntry<int> QuickMenuVerticalAngle;
        private static ConfigEntry<bool> QuickMenuClassicSeperate;
        private static ConfigEntry<bool> lockGuiWhileInventoryOpen;
        private static ConfigEntry<bool> autoOpenKeyboardOnInteract;

        // VR Hud Settings
        private static ConfigEntry<bool> useLegacyHud;
        private static ConfigEntry<float> cameraHudX;
        private static ConfigEntry<float> cameraHudY;
        private static ConfigEntry<float> cameraHudScale;
        private static ConfigEntry<Vector3> leftWristPos;
        private static ConfigEntry<Quaternion> leftWristRot;
        private static ConfigEntry<Vector3> rightWristPos;
        private static ConfigEntry<Quaternion> rightWristRot;
        private static ConfigEntry<string> healthPanelPlacement;
        private static ConfigEntry<string> staminaPanelPlacement;
        private static ConfigEntry<string> eitrPanelPlacement;
        private static ConfigEntry<string> staggerPanelPlacement;
        private static ConfigEntry<string> minimapPanelPlacement;
        private static ConfigEntry<bool> allowHudFade;
        private static ConfigEntry<bool> hideHotbar;
        private static ConfigEntry<bool> alwaysShowStamina;

        private static ConfigEntry<Vector3> rightWristQuickBarPos;
        private static ConfigEntry<Quaternion> rightWristQuickBarRot;
        private static ConfigEntry<Vector3> leftWristQuickBarPos;
        private static ConfigEntry<Quaternion> leftWristQuickBarRot;
        private static ConfigEntry<bool> quickActionOnLeftHand;

        // Controls Settings
        private static ConfigEntry<bool> useLookLocomotion;
        private static ConfigEntry<string> dominantHand;
        private static ConfigEntry<KeyCode> headReposFowardKey;
        private static ConfigEntry<KeyCode> headReposBackwardKey;
        private static ConfigEntry<KeyCode> headReposLeftKey;
        private static ConfigEntry<KeyCode> headReposRightKey;
        private static ConfigEntry<KeyCode> headReposUpKey;
        private static ConfigEntry<KeyCode> headReposDownKey;
        private static ConfigEntry<KeyCode> hmdRecenterKey;
        private static ConfigEntry<bool> snapTurnEnabled;
        private static ConfigEntry<int> snapTurnAngle;
        private static ConfigEntry<bool> smoothSnapTurn;
        private static ConfigEntry<float> smoothSnapSpeed;
        private static ConfigEntry<bool> roomScaleSneaking;
        private static ConfigEntry<float> roomScaleSneakHeight;
        private static ConfigEntry<bool> exclusiveRoomScaleSneak;
        private static ConfigEntry<bool> gesturedLocomotion;
        private static ConfigEntry<bool> weaponNeedsSpeed;
        private static ConfigEntry<float> altPieceRotationDelay;
        private static ConfigEntry<bool> runIsToggled;
        private static ConfigEntry<bool> viewTurnWithMountedAnimal;
        private static ConfigEntry<bool> advancedBuildMode;
        private static ConfigEntry<bool> freePlaceAutoReturn;
        private static ConfigEntry<bool> advancedRotationUpWorld;
        private static ConfigEntry<bool> buildOnRelease;
        private static ConfigEntry<string> buildAngleSnap;
        private static ConfigEntry<float> smoothTurnSpeed;

        // Graphics Settings
        private static ConfigEntry<bool> useAmplifyOcclusion;
        private static ConfigEntry<float> taaSharpenAmmount;
        private static ConfigEntry<float> nearClipPlane;
        private static ConfigEntry<string> bowGlow;
        private static ConfigEntry<float> enemyRenderDistance;

        // Motion Control Settings
        private static ConfigEntry<bool> useArrowPredictionGraphic;
        private static ConfigEntry<float> arrowParticleSize;
        private static ConfigEntry<string> spearThrowingType;
        private static ConfigEntry<bool> useSpearDirectionGraphic;
        private static ConfigEntry<bool> spearThrowSpeedDynamic;
        private static ConfigEntry<bool> spearInverseWield;
        private static ConfigEntry<bool> twoHandedWield;
        private static ConfigEntry<bool> twoHandedWithShield;
        private static ConfigEntry<float> arrowRestElevation;
        private static ConfigEntry<string> arrowRestSide;
        private static ConfigEntry<string> bowDrawRestrictType;
        private static ConfigEntry<float> bowFullDrawLength;
        private static ConfigEntry<bool> bowAccuracyIgnoresDrawLength;
        private static ConfigEntry<float> bowStaminaAdjust;
        private static ConfigEntry<string> crossbowSaggitalRotationSource;
        private static ConfigEntry<bool> crossbowManualReload;
        private static ConfigEntry<string> blockingType;

#if DEBUG
        private static ConfigEntry<float> DebugPosX;
        private static ConfigEntry<float> DebugPosY;
        private static ConfigEntry<float> DebugPosZ;
        private static ConfigEntry<float> DebugRotX;
        private static ConfigEntry<float> DebugRotY;
        private static ConfigEntry<float> DebugRotZ;
        private static ConfigEntry<float> DebugScale;
#endif

        private static Dictionary<int, bool> commandLineOverrides = new Dictionary<int, bool>();

        // Common values
        private static readonly string[] k_HudAlignmentValues = { "LeftWrist", "RightWrist", "CameraLocked", "Legacy" };

        private const string k_arrowRestCenter = "Center";
        private const string k_arrowRestAsiatic = "Asiatic";
        private const string k_arrowRestMediterranean = "Mediterranean";

        public static void InitializeConfiguration(ConfigFile mConfig) {

            config = mConfig;
            InitializeImmutableSettings();
            InitializeGeneralSettings();
            InitializeUISettings();
            InitializeVrHudSettings();
            InitializeControlsSettings();
            InitializeGraphicsSettings();
            InitializeMotionControlSettings();
            DoVersionInit();
        }

        // Using this method to create a marker in the config file so we can
        // detect when the mod is being upgraded from an older version
        // and doing any one time initialization. E.g., since prior to 0.9.0, the VR
        // HUD canvases were parented on a different transform, for anyone who upgrades
        // to 0.9.0, the first time they start the mod their VR HUD settings will be reset
        // so that it isn't completely broken.
        private static void DoVersionInit()
        {
            LogUtils.LogInfo("Perform pre-initialization...");
            var version = ValheimVRMod.PLUGIN_VERSION;
            System.Version parsedVersion = null;
            string configVersion = pluginVersion.Value;
            System.Version.TryParse(configVersion, out parsedVersion);
            if (parsedVersion == null || parsedVersion.CompareTo(System.Version.Parse("0.9.0")) < 0)
            {
                // If the parsed version was not set or is less than 0.9.0, which
                // is the first version with updated HUD transforms, we will automatically
                // set the positions back to default for their first time setup.
                ResetVrHudPositions();
            }
            pluginVersion.Value = version.ToString();
        }

        private static void ResetVrHudPositions()
        {
            LogUtils.LogDebug("Resetting HUD Positions for new version.");
            leftWristPos.Value = (Vector3)leftWristPos.DefaultValue;
            leftWristRot.Value = (Quaternion)leftWristRot.DefaultValue;
            rightWristPos.Value = (Vector3)rightWristPos.DefaultValue;
            rightWristRot.Value = (Quaternion)rightWristRot.DefaultValue;

            rightWristQuickBarPos.Value = (Vector3)rightWristQuickBarPos.DefaultValue;
            rightWristQuickBarRot.Value = (Quaternion)rightWristQuickBarRot.DefaultValue;
            leftWristQuickBarPos.Value = (Vector3)leftWristQuickBarPos.DefaultValue;
            leftWristQuickBarRot.Value = (Quaternion)leftWristQuickBarRot.DefaultValue;
        }

        private static void InitializeImmutableSettings()
        {
            vrModEnabled = createImmutableSettingWithOverride("Immutable",
                "ModEnabled",
                true,
                "Used to toggle the mod on and off.");
            nonVrPlayer = createImmutableSettingWithOverride("Immutable",
                "nonVrPlayer",
                false,
                "Disables VR completely. This is for Non-Vr Players that want to see their multiplayer VR companions animations in game.");
            useVrControls = createImmutableSettingWithOverride("Immutable",
                "UseVRControls",
                true,
                "This setting enables the use of the VR motion controllers as input (Only Oculus Touch and Valve Index supported)." +
                "This setting, if true, will also force UseOverlayGui to be false as this setting Overlay GUI is not compatible with VR laser pointer inputs.");
            useOverlayGui = createImmutableSettingWithOverride("Immutable",
                "UseOverlayGui",
                false,
                "WARNING: Setting this option will result in disabling the game from pausing due to a conflict. " +
                " Only use this if you are okay with the game not pausing while the menu is active. " +
                "Whether or not to use OpenVR overlay for the GUI. This produces a" +
                " cleaner GUI but will only be compatible with M&K or Gamepad controls.");
            // Do not allow overriding pluginVersion via command line
            pluginVersion = config.Bind("Immutable",
                "PluginVersion",
                "",
                "For internal use only. Do not edit.");
            bhapticsEnabled = createImmutableSettingWithOverride("Immutable",
                "bhapticsEnabled",
                false,
                "Enables bhaptics feedback. Only usable if vrModEnabled true AND nonVrPlayer false.");
        }

        private static ConfigEntry<bool> createImmutableSettingWithOverride(
            string section,
            string key,
            bool defaultValue,
            string description)
        {
            ConfigEntry<bool> immutableSetting = config.Bind<bool>(section, key, defaultValue, description);
            // now trying to find same setting in start options and override on match
            var p = new OptionSet {
                { key + "=",
                    "the immutable " + key + " to get the value of",
                    v => {
                            LogUtils.LogInfo("Overriding value for mod setting with command line argument: -" + key + "=" + v);
                            if (bool.TryParse(v, out bool result)) {
                                commandLineOverrides.Add(immutableSetting.GetHashCode(), result);
                            } else {
                                LogUtils.LogError("Invalid boolean string provided for command line option value: " + key + "=" + v);
                            }
                        }}
            };

            try {
                p.Parse(Environment.GetCommandLineArgs());
            }
            catch (Exception e) {
                Debug.LogError("Error parsing Start Option [" + key + "]: " + e.Message);
            }

            return immutableSetting;
        }

        private static void InitializeGeneralSettings()
        {
            recenterOnStart = config.Bind("General",
                                          "RecenterOnStart",
                                          true,
                                          "Set this to true if you want tracking to be automatically re-centered when the game first starts up.");
            disableRecenterPose = config.Bind("General",
                                          "DisableRecenterPose",
                                          false,
                                          "Set this true if you don't want to disable the re-centering the VR view using the \"Hands in front of face\" pose.");
            roomscaleFadeToBlack = config.Bind("General",
                                          "RoomscaleFadeToBlack",
                                          false,
                                          "Set this to true if you want the game to fade to black when roomscale movement causes the player to being pushed back.");
            mirrorMode = config.Bind("General",
                                     "MirrorMode",
                                     "Right",
                                     new ConfigDescription("The VR mirror mode.Legal values: OpenVR, Right, Left, None. Note: OpenVR is" +
                                     " required if you want to see the Overlay-type GUI in the mirror image. However, I've found that OpenVR" +
                                     " mirror mode causes some issue that requires SteamVR to be restarted after closing the game, so unless you" +
                                     " need it for some specific reason, I recommend using another mirror mode or None.",
                                     new AcceptableValueList<string>(new string[] { "Right", "Left", "OpenVR", "None" })));
            headOffsetX = config.Bind("General",
                                      "FirstPersonHeadOffsetX",
                                      0.0f,
                                      new ConfigDescription("**OBSOLETE**: Due to some changes made to recentering tracking, the first person positional values are" +
                                      "no longer saved/used between play sessions. You can still manually adjust position in game, but it will be reset each time you restart" +
                                      " the game or recenter HMD tracking.",
                                      new AcceptableValueRange<float>(-2.0f, 2.0f)));
            headOffsetZ = config.Bind("General",
                          "FirstPersonHeadOffsetZ",
                          0.0f,
                          new ConfigDescription("**OBSOLETE**: See FirstPersonHeadOffsetX description.",
                          new AcceptableValueRange<float>(-2.0f, 2.0f)));
            headOffsetY = config.Bind("General",
                          "FirstPersonHeadOffsetY",
                          0.0f,
                          new ConfigDescription("**OBSOLETE**: See FirstPersonHeadOffsetX description.",
                          new AcceptableValueRange<float>(-2.0f, 2.0f)));
            headOffsetThirdPersonX = config.Bind("General",
                          "ThirdPersonHeadOffsetX",
                          0.0f,
                          new ConfigDescription("Adjusts X position in third person cam. All third person zoom levels all share same offset.",
                          new AcceptableValueRange<float>(-2.0f, 2.0f)));
            headOffsetThirdPersonZ = config.Bind("General",
                          "ThirdPersonHeadOffsetZ",
                          0.0f,
                          new ConfigDescription("Adjusts Z position in third person cam. All third person zoom levels all share same offset.",
                          new AcceptableValueRange<float>(-2.0f, 2.0f)));
            headOffsetThirdPersonY = config.Bind("General",
                          "ThirdPersonHeadOffsetY",
                          0.0f,
                          new ConfigDescription("Adjusts Y position in third person cam. All third person zoom levels all share same offset.",
                          new AcceptableValueRange<float>(-2.0f, 2.0f)));
            enableHeadReposition = config.Bind("General",
                                                "EnableHeadRepositioning",
                                                true,
                                                "Set to this true enable using the arrow keys to position the camera when in first or third person mode. You can use this to" +
                                                " set the values of First/ThirdPersonHeadOffsetX/Y/Z while in game rather than having to edit them manually in the config file. " +
                                                "Your settings will be remembered between gameplay sessions via this config file.");
            immersiveShipCameraSitting = config.Bind("General",
                                          "ImmersiveShipCameraSitting",
                                          false,
                                          "Make the camera follows the ship tilt while standing/sitting on ship (may induce motion sickness)");
            immersiveShipCameraStanding = config.Bind("General",
                                          "ImmersiveShipCameraStanding",
                                          "WorldUp",
                                          new ConfigDescription("Make the camera follows the ship direction while standing on it, World up will only follow the ship direction, while ShipUp will follow both ship tilt and direction",
                                          new AcceptableValueList<string>(new string[] { "None", "WorldUp", "ShipUp" })));
            immersiveDodgeRoll = config.Bind("General",
                                          "ImmersiveDodgeRoll",
                                          false,
                                          "Make the camera rotate with character head during dodge roll (may induce motion sickness)");
            allowMovementWhenInMenu = config.Bind("General",
                                          "AllowMovementWhenInMenu",
                                          true,
                                          "Allow player character movement when the menu is open. Note that in single player this has no effect due to game pause.");
        }

        private static void InitializeUISettings()
        {
            overlayWidth = config.Bind("UI",
                                       "OverlayWidth",
                                       4f,
                                       new ConfigDescription("Width, in meters, that you want the Overlay GUI to be.",
                                       new AcceptableValueRange<float>(0.5f, 15f)));
            overlayDistance = config.Bind("UI",
                                          "OverlayDistance",
                                          2f,
                                           new ConfigDescription("The distance from you that you want the Overlay GUI to be rendered at.",
                                           new AcceptableValueRange<float>(0.5f, 15f)));
            overlayVerticalPosition = config.Bind("UI",
                                                   "OverlayVerticalPosition",
                                                   1f,
                                                   new ConfigDescription("Vertical offset for the Overlay GUI to make it higher or lower",
                                                   new AcceptableValueRange<float>(-0.5f, 3f)));
            overlayCurvature = config.Bind("UI",
                                           "OverlayCurvature",
                                           0.25f,
                                           new ConfigDescription("The amount of curvature to use for the GUI overlay. Only used when UseOverlayGui is true. " +
                                           "Valid values are  0.0 - 1.0. Use the -/= keys to adjust in game (setting will be remembered).",
                                           new AcceptableValueRange<float>(0f, 1f)));
            uiPanelSize = config.Bind("UI",
                                      "UIPanelSize",
                                      3f,
                                      new ConfigDescription("Size for the UI panel display (non-Overlay GUI).",
                                      new AcceptableValueRange<float>(0.5f, 15f)));

            uiPanelResolution = config.Bind("UI",
                                      "UIPanelResolution",
                                      new Vector2(1920, 1080),
                                      new ConfigDescription("The resolution of the UI Panel display (non-Overlay GUI), Use above 1300 width and 940 height for no crop/clipping for vanilla ui, need restart to update"));
            uiPanelResolutionCompat = config.Bind("UI",
                                      "UIPanelResolutionCompatibility",
                                      false,
                                      new ConfigDescription("Set UI resolution panel display compatibility mode, in case some mod have some mouse offset problem, use this setting, set panel resolution below your monitor resolution, need restart to update"));
            uiPanelDistance = config.Bind("UI",
                                      "UIPanelDistance",
                                      3f,
                                      new ConfigDescription("Distance to draw the UI panel at.",
                                      new AcceptableValueRange<float>(0.5f, 15f)));
            uiPanelVerticalOffset = config.Bind("UI",
                                      "UIPanelVerticalOffset",
                                      1f,
                                      new ConfigDescription("Height the UI Panel will be drawn.",
                                      new AcceptableValueRange<float>(-0.5f, 3f)));
            showStaticCrosshair = config.Bind("UI",
                                   "ShowStaticCrosshair",
                                   true,
                                   "This determines whether or not the normal crosshair that is visible by default is visible in VR.");
            crosshairScale = config.Bind("UI",
                                         "CrosshairScale",
                                         1.0f,
                                         new ConfigDescription("Scalar multiplier to adjust the size of the crosshair to your preference. 1.0 is probably fine.",
                                         new AcceptableValueRange<float>(0.8f, 2.5f)));
            showRepairHammer = config.Bind("UI",
                                           "ShowRepairHammer",
                                           true,
                                           "This adds an indicator on screen when in repair mode that shows where the repair cursor currently is.");
            showEnemyHuds = config.Bind("UI",
                                        "ShowEnemyHuds",
                                        true,
                                        "Enable or disable displaying enemy names and stats above them in game.");
            enemyHudScale = config.Bind("UI",
                                        "EnemyHudScale",
                                         1.0f,
                                         new ConfigDescription("Scalar multiplier to adjust the size of enemy huds to your preference. 1.0 is probably fine.",
                                         new AcceptableValueRange<float>(0.5f, 3f)));
            stationaryGuiRecenterAngle = config.Bind("UI",
                                                     "StationaryGuiRecenterAngle",
                                                     75f,
                                                     new ConfigDescription("Only used when UseLookLocomotion is true. This is the angle away from the center of the GUI that will trigger the GUI to" +
                                                     " recenter on your current look direction while stationary.",
                                                     new AcceptableValueRange<float>(0f, 100f)));
            mobileGuiRecenterAngle = config.Bind("UI",
                                     "MobileGuiRecenterAngle",
                                     50f,
                                     new ConfigDescription("Only used when UseLookLocomotion is true. This is the angle away from the center of the GUI that will trigger the GUI to" +
                                     " recenter on your current look direction while moving.",
                                     new AcceptableValueRange<float>(0f, 100f)));
            recenterGuiOnMove = config.Bind("UI",
                                            "RecenterGuiOnMove",
                                            true,
                                            "Only used when UseLookLocomotion is true. This will cause the GUI to recenter to your current look direction when you first start moving.");
            guiRecenterSpeed = config.Bind("UI",
                                            "GuiRecenterSpeed",
                                            180,
                                            new ConfigDescription("Speed in degrees per second of the Gui recentering algorithm",
                                                new AcceptableValueRange<int>(0, 360)));
            unlockDesktopCursor = config.Bind("UI",
                "UnlockDesktopCursor",
                true,
                "Normally the desktop cursor is locked to the center of the screen to avoid having the player accidentally lose focus when playing. This option can be used to free the mouse " +
                "cursor which some users may want, especially if exclusively using motion controls where window focus is not needed.");
            QuickMenuType = config.Bind("UI",
                "QuickMenuType",
                "Hand-Player",
                new ConfigDescription("Quick menu will follow specific orientation, Full Hand - follow hand rotation, Hand-Player - follow hand rotation with up always follow the player orientation, Full Player - follows player orientation, Hand Follow cam - follow hand rotation, but always face player head",
                new AcceptableValueList<string>(new string[] { "Full Hand", "Hand-Player", "Full Player", "Hand Follow Cam" })));
            QuickMenuVerticalAngle = config.Bind("UI",
                "QuickMenuVerticalAngle",
                180,
                new ConfigDescription("Set the quickmenu vertical angle, affects all QuickMenu type except Hand Follow Cam ",
                    new AcceptableValueRange<int>(0, 360)));
            QuickMenuClassicSeperate = config.Bind("UI",
                "QuickMenuClassicSeperate",
                false,
                new ConfigDescription("Set the quickmenu to have seperate types of item on left and right (melee weapon on one side, and shield,bow, other items on the other side)"));
            lockGuiWhileInventoryOpen = config.Bind("UI",
                "LockGuiPositionWhenMenuOpen",
                true,
                "Use this so that the GUI will remain in place whenever the Inventory or Menu is open.");

            autoOpenKeyboardOnInteract = config.Bind("UI",
                "AutoOpenKeyboardOnInteract",
                true,
                "Automatically open keyboard when interact with things that have text input (eg. Signs, Portal), Turning it off would have better support with modded stuff (especially modded portal)");
        }

        private static void InitializeVrHudSettings()
        {
            useLegacyHud = config.Bind("VRHUD",
                                            "UseLegacyHud",
                                            false,
                                            "Disables custom VR HUD features and moves HUD elements to main UI panel.");
            cameraHudX = config.Bind("VRHUD",
                                            "CameraHudX",
                                            0f,
                                            new ConfigDescription("Offset to reposition VR health panel for Camera Position.",
                                                new AcceptableValueRange<float>(-0.001f, 0.001f)));
            cameraHudY = config.Bind("VRHUD",
                                            "CameraHudY",
                                            0f,
                                            new ConfigDescription("Offset to reposition VR health panel for Camera Position.",
                                                new AcceptableValueRange<float>(-0.001f, 0.001f)));
            cameraHudScale = config.Bind("VRHUD",
                                            "CameraHudScale",
                                            1f,
                                            new ConfigDescription("Scalar multiple to determine VR Camera Hud scale.",
                                                new AcceptableValueRange<float>(.25f, 3f)));
            leftWristPos = config.Bind("VRHUD",
                                            "LeftWrist",
                                            new Vector3(0.031569551676511767f, 0.11530060321092606f, -0.11794090270996094f),
                                            "Position of VR Hud on Left Wrist.");
            leftWristRot = config.Bind("VRHUD",
                                            "LeftWristRot",
                                            new Quaternion(-0.29706940054893496f, 0.6141623258590698f, 0.3103596866130829f, -0.6619904041290283f),
                                            "Rotation for reposition VR Hud on Left Wrist.");
            rightWristPos = config.Bind("VRHUD",
                                            "RightWrist",
                                            new Vector3(-0.09589999914169312f, 0.008500000461935997f, -0.07050000131130219f),
                                            "Position of VR Hud on Right Wrist.");
            rightWristRot = config.Bind("VRHUD",
                                            "RightWristRot",
                                            new Quaternion(0.29660001397132876f, 0.03720000013709068f, 0.10580000281333924f, 0.9484000205993652f),
                                            "Rotation for reposition VR Hud on Right Wrist");

            healthPanelPlacement = config.Bind("VRHUD",
                                              "HealthPanelPlacement",
                                              "LeftWrist",
                                              new ConfigDescription("Where should the health panel be placed?",
                                              new AcceptableValueList<string>(k_HudAlignmentValues)));
            staminaPanelPlacement = config.Bind("VRHUD",
                                            "StaminaPanelPlacement",
                                            "CameraLocked",
                                            new ConfigDescription("Where should the stamina panel be placed?",
                                                new AcceptableValueList<string>(k_HudAlignmentValues)));
            eitrPanelPlacement = config.Bind("VRHUD",
                                            "EitrPanelPlacement",
                                            "CameraLocked",
                                            new ConfigDescription("Where should the eitr panel be placed?",
                                                new AcceptableValueList<string>(k_HudAlignmentValues)));
            staggerPanelPlacement = config.Bind("VRHUD",
                                            "StaggerPanelPlacement",
                                            "CameraLocked",
                                            new ConfigDescription("Where should the stagger panel be placed?",
                                                new AcceptableValueList<string>(k_HudAlignmentValues)));
            minimapPanelPlacement = config.Bind("VRHUD",
                                            "MinimapPanelPlacement",
                                            "RightWrist",
                                            new ConfigDescription("Where should the minimap panel be placed?",
                                                new AcceptableValueList<string>(k_HudAlignmentValues)));
            allowHudFade = config.Bind("VRHUD",
                                        "AllowHudFade",
                                        true,
                                        "When the HUD is attached to a wrist, allow it to fade away unless you are actively looking at it.");
            hideHotbar = config.Bind("VRHUD",
                                        "HideHotbar",
                                        true,
                                        "Hide the hotbar, as it is generally unused when playing in VR. Ignored if not playing with VR controls.");
            alwaysShowStamina = config.Bind("VRHUD",
                                        "AlwaysShowStamina",
                                        true,
                                        "Always show the stamina bar even if its full");
            rightWristQuickBarPos = config.Bind("VRHUD",
                                            "RightWristQuickAction",
                                            new Vector3(0.001420379034243524f, 0.09409096091985703f, -0.19604730606079102f),
                                            "Position of extra Quick Action bar on Right Wrist.");
            rightWristQuickBarRot = config.Bind("VRHUD",
                                            "RightWristQuickActionRot",
                                            new Quaternion(0.6157100796699524f, 0.6319897174835205f, -0.16848529875278474f, 0.4394344985485077f),
                                            "Rotation for extra Quick Action bar on Right Wrist.");
            leftWristQuickBarPos = config.Bind("VRHUD",
                                            "LeftWristQuickSwitch",
                                            new Vector3(-0.005364172160625458f, 0.11832620203495026f, -0.19671690464019776f),
                                            "Position of extra Quick Switch bar on Left Wrist.");
            leftWristQuickBarRot = config.Bind("VRHUD",
                                            "LeftWristQuickSwitchRot",
                                            new Quaternion(-0.5040010213851929f, 0.7026780843734741f, -0.09470813721418381f, -0.4932106137275696f),
                                            "Rotation for reposition extra Quick Switch bar on Left Wrist");
            quickActionOnLeftHand = config.Bind("VRHUD",
                                        "QuickActionOnLeftHand",
                                        false,
                                        "Switch hand placement of Quick Action and Quick Switch Hotbar");
        }

        private static void InitializeControlsSettings()
        {
            useLookLocomotion = config.Bind("Controls",
                                            "UseLookLocomotion",
                                            true,
                                            "Setting this to true ties the direction you are looking to the walk direction while in first person mode. " +
                                            "Set this to false if you prefer to disconnect these so you can look" +
                                            " look by turning your head without affecting movement direction.");
            smoothTurnSpeed = config.Bind("Controls",
                                          "SmoothTurnSpeed",
                                          1f,
                                          new ConfigDescription("Controls the sensitivity for smooth turning while motion controls active.",
                                          new AcceptableValueRange<float>(.25f, 2.5f)));
            snapTurnEnabled = config.Bind("Controls",
                                          "SnapTurnEnabled",
                                          false,
                                          "Enable Snap Turning");
            snapTurnAngle = config.Bind("Controls",
                                        "SnapTurnAngle",
                                        45,
                                        new ConfigDescription("Angle to snap to when snap turning is used.",
                                            new AcceptableValueRange<int>(0, 90)));
            smoothSnapTurn = config.Bind("Controls",
                                         "SmoothSnapTurn",
                                         true,
                                         "While snap turn is enabled, this will cause the snap to be a very quick turn rather than an immediate change to the new snapped angle.");
            smoothSnapSpeed = config.Bind("Controls",
                                          "SmoothSnapSpeed",
                                          10f,
                                          new ConfigDescription("This will affect the speed that the smooth snap turns occur at.",
                                              new AcceptableValueRange<float>(5, 30)));
            roomScaleSneaking = config.Bind("Controls",
                                          "RoomScaleSneaking",
                                          false,
                                          "Enable RoomScale Sneaking.");
            roomScaleSneakHeight = config.Bind("Controls",
                                          "RoomScaleSneakHeight",
                                          0.7f,
                                          new ConfigDescription("This will affect the eye height that the roomscale sneak occur at.  (e.g. 0.7 means if your headset lower than 70% of your height, it will do sneak)  " +
                                           "Valid values are  0.0 - 0.95.",
                                           new AcceptableValueRange<float>(0f, 0.95f)));
            exclusiveRoomScaleSneak = config.Bind("Controls",
                                          "ExclusiveRoomScaleSneak",
                                          false,
                                          "If this is set to true and Room Scale sneaking is on, Controller-based sneak inputs will be disabled. Use this if you ONLY want to sneak by phsyically crouching.");
            gesturedLocomotion = config.Bind("Controls",
                                             "Gestured Locomotion",
                                             false,
                                             "Enables using arm movements to swim");
            dominantHand = config.Bind("Controls",
                                        "DominantHand",
                                        "Right",
                                        new ConfigDescription("The dominant hand of the player",
                                        new AcceptableValueList<string>(new string[] { "Right", "Left" })));
            weaponNeedsSpeed = config.Bind("Controls",
                "SwingWeapons",
                true,
                "Defines if Swinging a Weapon needs certain speed. if set to false, single touch will already trigger hit");
            altPieceRotationDelay = config.Bind("Controls",
                                                "AltPieceRotationDelay",
                                                1f,
                                                new ConfigDescription("Affects speed of piece rotation when using 'Grab' + 'Joystick' method of rotating build objects. Legal values 0.1 - 3. Higher is longer delay.",
                                                new AcceptableValueRange<float>(0.1f, 3f)));
            runIsToggled = config.Bind("Controls",
                                       "RunIsToggled",
                                       true,
                                       "Determine whether or not you need to hold run or it is a toggle. Keep it as toggle (true) to have your thumb free when sprinting.");
            viewTurnWithMountedAnimal = config.Bind("Controls",
                                       "ViewTurnWithMountedAnimal",
                                       false,
                                       "Whether the view turns automatically together with the mounted animal when the animal turns.");
            InitializeConfigurableKeyBindings(config);
        }

        private static void InitializeConfigurableKeyBindings(ConfigFile config)
        {
            headReposFowardKey = config.Bind("Controls",
                                             "HeadRepositionForwardKey",
                                             KeyCode.UpArrow,
                                             "Key used to move head camera forwards. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposBackwardKey = config.Bind("Controls",
                                               "HeadRepositionBackwardKey",
                                               KeyCode.DownArrow,
                                               "Key used to move head camera backwards. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposLeftKey = config.Bind("Controls",
                                           "HeadRepositionLeftKey",
                                           KeyCode.LeftArrow,
                                           "Key used to move head camera left. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposRightKey = config.Bind("Controls",
                                           "HeadRepositionRightKey",
                                           KeyCode.RightArrow,
                                           "Key used to move head camera right. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposUpKey = config.Bind("Controls",
                                           "HeadRepositionUpKey",
                                           KeyCode.PageUp,
                                           "Key used to move head camera up. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            headReposDownKey = config.Bind("Controls",
                                           "HeadRepositionDownKey",
                                           KeyCode.PageDown,
                                           "Key used to move head camera down. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
            hmdRecenterKey = config.Bind("Controls",
                                           "HMDRecenterKey",
                                           KeyCode.Home,
                                           "Key used to recenter HMD tracking. Must be matching key from https://docs.unity3d.com/2019.4/Documentation/ScriptReference/KeyCode.html");
        }

        private static void InitializeGraphicsSettings()
        {
            useAmplifyOcclusion = config.Bind("Graphics",
                                              "UseAmplifyOcclusion",
                                              true,
                                              "RECOMMENDED - Determines whether or not to use the Amplify Occlusion post processing effect." +
                                              " This implements an effect similar to SSAO but with much less performance" +
                                              " cost. While you can enable SSAO and UseAmplifyOcclusion simultaneously, it is" +
                                              " not recommended. SSAO impacts performance significantly, which is bad for VR especially. Therefore" +
                                              " you should disable SSAO in the graphics settings of the game when using this.");
            taaSharpenAmmount = config.Bind("Graphics",
                                              "TAASharpenAmmount",
                                              0.3f,
                                              new ConfigDescription("Ammount of Sharpen applied after the TAA filter, values should be in the range [0.0,3.0]." +
                                                                    " Values outside this range will be ignored and the default game settings will be used instead.",
                                                  new AcceptableValueRange<float>(0, 3)));
            nearClipPlane = config.Bind("Graphics",
                                        "NearClipPlane",
                                        .09f,
                                        new ConfigDescription("This can be used to adjust the distance where where anything inside will be clipped out and not rendered. You can try adjusting this if you experience" +
                                                              " problems where you see the nose of the player character for example.",
                                        new AcceptableValueRange<float>(0.05f, 0.5f)));
            bowGlow = config.Bind("Graphics",
                                  "BowGlow",
                                  "None",
                                  new ConfigDescription(
                                      "Whether the glowing effect of the bow (if any in the Vanilla game) should be enabled. Disable it if you find the glow affects you aim negatively.",
                                      new AcceptableValueList<string>(new string[] {"None", "LightWithoutParticles", "Full"})));
            enemyRenderDistance = config.Bind("Graphics",
                                        "EnemyRenderDistance",
                                        8f,
                                        new ConfigDescription("Increase the mobs render distance, does not apply to tamed creature, only raise mob render distance, not lowering them (default eg. deer render distance is around 2, neck is around 10) (also limited by default ingame draw distance option)",
                                        new AcceptableValueRange<float>(1f, 50f)));

        }

        private static void InitializeMotionControlSettings() {
            //Bow Changes
            useArrowPredictionGraphic = config.Bind("Motion Control",
                "UseArrowPredictionGraphic",
                true,
                "Use this to toggle the path predictor when using the bow and arrow with VR controls.");
            arrowParticleSize = config.Bind("Motion Control",
                "ArrowParticleSize",
                0.5f,
                new ConfigDescription("set size of the particles on drawing arrows (fire,poison, etc.)",
                    new AcceptableValueRange<float>(0, 1)));
            arrowRestElevation = config.Bind("Motion Control",
                "ArrowRestElevation",
                0.15f,
                new ConfigDescription("The amount by which the arrow rest is higher than the center of the bow handle",
                    new AcceptableValueRange<float>(0, 0.25f)));
            arrowRestSide = config.Bind("Motion Control",
                "ArrowRestSide",
                k_arrowRestCenter,
                new ConfigDescription("Whether the arrow should rest on the side of the bow farther from the eyes (Asiatic), the side closer to the eyes (Mediterranean), or the center.",
                new AcceptableValueList<string>(new string[] { k_arrowRestCenter, k_arrowRestAsiatic, k_arrowRestMediterranean })));
            bowDrawRestrictType = config.Bind("Motion Control",
                "BowDrawRestrictType",
                "Full",
                new ConfigDescription("Whether to apply vanilla-style restriction on bow drawing speed and make premature releases inaccurate. Full - Use Vanilla charge time, with physical hand drawing restrict. Partial - Use Vanilla charge time, but allow you to fully draw it from start. None - no restriction to draw speed but use extra stamina drain",
                new AcceptableValueList<string>(new string[] { "Full", "Partial", "None" })));

            bowFullDrawLength = config.Bind("Motion Control",
                "BowFullDrawLength",
                0.6f,
                new ConfigDescription("Adjust the full draw length of bow; Lower value is useful for controller with inside out tracking",
                new AcceptableValueRange<float>(0.2f, 0.8f)));

            bowAccuracyIgnoresDrawLength = config.Bind("Motion Control",
                                                    "BowAccuracyIgnoresDrawLength",
                                                    true,
                                                    "Use charging time instead of draw length to determine bow accuracy");

            bowStaminaAdjust = config.Bind("Motion Control",
                "BowStaminaAdjust",
                1.0f,
                new ConfigDescription("Multiplier for stamina drain on bow. Reduce for less stamina drain.",
                new AcceptableValueRange<float>(0.25f, 1.0f)));

            //Spear Changes
            spearThrowingType = config.Bind("Motion Control",
                                            "SpearThrowingMode",
                                            "Classic",
                                            new ConfigDescription("Change the throwing mode, Throw by holding grab and trigger and then release trigger." +
                                            "Classic - Throw aim is based on swing direction" +
                                            "DartType - Throw aim is based on first trigger pressed to release in a straight line" +
                                            "TwoStagedThrowing - Throw aim is based on first grab and then aim is locked after pressing trigger" +
                                            "SecondHandAiming - Throw aim is based from your head to your left hand in a straight line",
                                            new AcceptableValueList<string>(new string[] { "Classic", "DartType", "TwoStagedThrowing", "SecondHandAiming" })));
            spearThrowSpeedDynamic = config.Bind("Motion Control",
                                                "SpearThrowSpeedDynamic",
                                                true,
                                                "Determine whether or not your throw power depends on swing speed, setting to false make the throw always on fixed speed.");
            spearInverseWield = config.Bind("Motion Control",
                                                "SpearInverseWield",
                                                true,
                                                "Use this to flip the spear tip, so you can stab forward instead of needing to do downward stabbing");
            useSpearDirectionGraphic = config.Bind("Motion Control",
                                                    "UseSpearDirectionGraphic",
                                                    true,
                                                    "Use this to toggle the direction line of throwing when using the spear with VR controls.");
            //Two-handed Changes
            twoHandedWield = config.Bind("Motion Control",
                                                    "TwoHandedWield",
                                                    true,
                                                    "Use this to toggle controls of two handed weapon (left & right hand grab on weapon), allow blocking and better weapon handling");
            twoHandedWithShield = config.Bind("Motion Control",
                                                    "TwoHandedWithShield",
                                                    false,
                                                    "Allows Two Handed Wield while using shield");

            crossbowSaggitalRotationSource = config.Bind("Motion Control",
                                        "CrossbowSaggitalRotationSource",
                                        "MotionControl",
                                        new ConfigDescription("Which hand(s) can rotate the crossbow along its saggital axis during two-handed hold",
                                        new AcceptableValueList<string>(new string[] { "RearHand", "BothHands" })));
            crossbowManualReload = config.Bind("Motion Control",
                                                    "CrossbowManualReload",
                                                    true,
                                                    "When supported, crossbows requires manually pulling the string to reload");
            blockingType = config.Bind("Motion Control",
                                        "BlockingType",
                                        "Gesture",
                                        new ConfigDescription("Block logic: " +
                                        "Gesture - Block by holding the shield or weapon perpendicular to the attack, swing while blocking to parry. " +
                                        "Grab button - Block by aiming and pressing grab button, parry by timing the grab button. " +
                                        "Realistic - Block precisely where the enemy hits, swing while blocking to parry",
                                        new AcceptableValueList<string>(new string[] { "Gesture", "GrabButton", "Realistic" })));


            advancedBuildMode = config.Bind("Motion Control",
                                                   "AdvancedBuildMode",
                                                   false,
                                                   "Enable Advanced Building mode (Free place, Advanced Rotation)");
            freePlaceAutoReturn = config.Bind("Motion Control",
                                                    "FreePlaceAutoReturn",
                                                    false,
                                                    "Automatically return to normal building mode after building a piece in Free place mode");
            advancedRotationUpWorld = config.Bind("Motion Control",
                                                    "AdvanceRotationUpWorld",
                                                    true,
                                                    "Always use rotate vertically up when using analog rotation while in advanced build mode");
            buildOnRelease = config.Bind("Motion Control",
                                         "BuildOnRelease",
                                         true,
                                         "If true, when building, objects will be placed when releasing the trigger isntead of on pressing it down.");
            buildAngleSnap = config.Bind("Motion Control",
                                         "BuildAngleSnap",
                                         "26, 22.5, 10, 5, 2.5, 1, 0.5, 0.1, 0.05, 0.01",
                                         "List of Build angle snap for advance rotation mode");
            
            #if DEBUG
            DebugPosX = config.Bind("Motion Control",
                "DebugPosX",
                0.0f,
                new ConfigDescription("DebugPosX",
                    new AcceptableValueRange<float>(-1.0f, 1.0f)));
            DebugPosY = config.Bind("Motion Control",
                "DebugPosY",
                0.0f,
                new ConfigDescription("DebugPosY",
                    new AcceptableValueRange<float>(-1.0f, 1.0f)));
            DebugPosZ = config.Bind("Motion Control",
                "DebugPosZ",
                0.0f,
                new ConfigDescription("DebugPosZ",
                    new AcceptableValueRange<float>(-1.0f, 1.0f)));
            DebugRotX = config.Bind("Motion Control",
                "DebugRotX",
                0.0f,
                new ConfigDescription("DebugRotX",
                    new AcceptableValueRange<float>(-1.0f, 1.0f)));
            DebugRotY = config.Bind("Motion Control",
                "DebugRotY",
                0.0f,
                new ConfigDescription("DebugRotY",
                    new AcceptableValueRange<float>(-1.0f, 1.0f)));
            DebugRotZ = config.Bind("Motion Control",
                "DebugRotZ",
                0.0f,
                new ConfigDescription("DebugRotZ",
                    new AcceptableValueRange<float>(-1.0f, 1.0f)));
            DebugScale = config.Bind("Motion Control",
                "DebugScale",
                1.0f,
                new ConfigDescription("DebugScale",
                    new AcceptableValueRange<float>(0.0f, 2.0f)));
            #endif
        }

        public static bool ModEnabled()
        {
            if (commandLineOverrides.ContainsKey(vrModEnabled.GetHashCode()))
            {
                return commandLineOverrides[vrModEnabled.GetHashCode()];
            }
            return vrModEnabled.Value;
        }

        public static string GetPreferredHand()
        {
            // TODO: rename this method to GetDominantHand.
            return dominantHand.Value == "Left" ? VRPlayer.LEFT_HAND : VRPlayer.RIGHT_HAND;
        }

        public static OpenVRSettings.MirrorViewModes GetMirrorViewMode()
        {
            string mode = mirrorMode.Value;
            if (mode == "Right")
            {
                return OpenVRSettings.MirrorViewModes.Right;
            } else if (mode == "Left")
            {
                return OpenVRSettings.MirrorViewModes.Left;
            } else if (mode == "OpenVR")
            {
                return OpenVRSettings.MirrorViewModes.OpenVR;
            } else if (mode == "None")
            {
                return OpenVRSettings.MirrorViewModes.None;
            } else
            {
                LogUtils.LogWarning("Invalid mirror mode setting. Defaulting to None");
                return OpenVRSettings.MirrorViewModes.None;
            }
        }

        public static bool GetUseOverlayGui()
        {
            bool useOverlayGuiValue = useOverlayGui.Value;
            if (commandLineOverrides.ContainsKey(useOverlayGui.GetHashCode()))
            {
                useOverlayGuiValue = commandLineOverrides[useOverlayGui.GetHashCode()];
            }
            // Force this to be off if UseVrControls is on
            return useOverlayGuiValue && !UseVrControls();
        }

        public static float GetOverlayWidth()
        {
            return overlayWidth.Value;
        }

        public static float GetOverlayDistance()
        {
            return overlayDistance.Value;
        }

        public static float GetOverlayVerticalOffset()
        {
            return overlayVerticalPosition.Value;
        }

        public static float GetOverlayCurvature()
        {
            return overlayCurvature.Value;
        }

        public static void UpdateOverlayCurvature(float value)
        {
            value = Mathf.Clamp(value, 0f, 1f);
            overlayCurvature.Value = value;
        }

        public static float GetUiPanelSize()
        {
            return uiPanelSize.Value;
        }

        public static Vector2 GetUiPanelResolution()
        {
            return uiPanelResolution.Value;
        }

        public static bool GetUiPanelResoCompatibility()
        {
            return uiPanelResolutionCompat.Value;
        }

        public static float GetUiPanelVerticalOffset()
        {
            return uiPanelVerticalOffset.Value;
        }

        public static float GetUiPanelDistance()
        {
            return uiPanelDistance.Value;
        }

        public static bool UseAmplifyOcclusion()
        {
            return useAmplifyOcclusion.Value;
        }

        public static bool EnableBowGlowParticle()
        {
            return bowGlow.Value == "Full";
        }

        public static bool EnableBowGlowLight()
        {
            return bowGlow.Value == "Full" || bowGlow.Value == "LightWithoutParticles";
        }

        public static float GetTaaSharpenAmmount()
        {
            return taaSharpenAmmount.Value;
        }

        public static Vector3 GetFirstPersonHeadOffset()
        {
            return new Vector3(headOffsetX.Value, headOffsetY.Value, headOffsetZ.Value);
        }

        public static Vector3 GetThirdPersonHeadOffset()
        {
            return new Vector3(headOffsetThirdPersonX.Value, headOffsetThirdPersonY.Value, headOffsetThirdPersonZ.Value);
        }

        public static bool AllowHeadRepositioning()
        {
            return enableHeadReposition.Value;
        }

        public static void UpdateFirstPersonHeadOffset(Vector3 offset) {
            headOffsetX.Value = Mathf.Clamp(offset.x, -2f, 2f);
            headOffsetY.Value = Mathf.Clamp(offset.y, -2f, 2f);
            headOffsetZ.Value = Mathf.Clamp(offset.z, -2f, 2f);
        }

        public static void UpdateThirdPersonHeadOffset(Vector3 offset)
        {
            headOffsetThirdPersonX.Value = Mathf.Clamp(offset.x, -2f, 2f);
            headOffsetThirdPersonY.Value = Mathf.Clamp(offset.y, -2f, 2f);
            headOffsetThirdPersonZ.Value = Mathf.Clamp(offset.z, -2f, 2f);
        }

        public static bool UseLookLocomotion()
        {
            return useLookLocomotion.Value;
        }

        public static bool ShowStaticCrosshair()
        {
            return showStaticCrosshair.Value;
        }

        public static float CrosshairScalar()
        {
            return crosshairScale.Value;
        }

        public static bool RecenterOnStart()
        {
            return recenterOnStart.Value;
        }

        public static bool RoomscaleFadeToBlack()
        {
            return roomscaleFadeToBlack.Value;
        }

        public static KeyCode GetRecenterKey()
        {
            return hmdRecenterKey.Value;
        }

        public static KeyCode GetHeadForwardKey()
        {
            return headReposFowardKey.Value;
        }

        public static KeyCode GetHeadBackwardKey()
        {
            return headReposBackwardKey.Value;
        }

        public static KeyCode GetHeadLeftKey()
        {
            return headReposLeftKey.Value;
        }

        public static KeyCode GetHeadRightKey()
        {
            return headReposRightKey.Value;
        }

        public static KeyCode GetHeadUpKey()
        {
            return headReposUpKey.Value;
        }

        public static KeyCode GetHeadDownKey()
        {
            return headReposDownKey.Value;
        }

        public static bool ShowRepairHammer()
        {
            return showRepairHammer.Value;
        }

        public static bool ShowEnemyHuds()
        {
            return showEnemyHuds.Value;
        }

        public static float EnemyHudScale()
        {
            return enemyHudScale.Value;
        }

        public static float MobileGuiRecenterAngle()
        {
            return mobileGuiRecenterAngle.Value;
        }

        public static float StationaryGuiRecenterAngle()
        {
            return stationaryGuiRecenterAngle.Value;
        }

        public static bool RecenterGuiOnMove()
        {
            return recenterGuiOnMove.Value;
        }

        public static float GuiRecenterSpeed()
        {
            return guiRecenterSpeed.Value;
        }

        public static bool UseVrControls()
        {
            bool useVrControlsValue = useVrControls.Value;
            if (commandLineOverrides.ContainsKey(useVrControls.GetHashCode()))
            {
                useVrControlsValue = commandLineOverrides[useVrControls.GetHashCode()];
            }
            return useVrControlsValue && !NonVrPlayer();
        }

        public static bool UseArrowPredictionGraphic()
        {
            return useArrowPredictionGraphic.Value;
        }

        public static float ArrowRestElevation()
        {
            return arrowRestElevation.Value;
        }

        public static float ArrowRestHorizontalOffsetMultiplier()
        {
            switch (arrowRestSide.Value) {
                case k_arrowRestAsiatic:
                    return LeftHanded() ? -1 : 1;
                case k_arrowRestMediterranean:
                    return LeftHanded() ? 1 : -1;
                default:
                    return 0;
            }
        }

        public static String RestrictBowDrawSpeed()
        {
            return bowDrawRestrictType.Value;
        }

        public static bool NonVrPlayer()
        {
            if (commandLineOverrides.ContainsKey(nonVrPlayer.GetHashCode()))
            {
                return commandLineOverrides[nonVrPlayer.GetHashCode()];
            }
            return nonVrPlayer.Value;
        }

#if DEBUG
        public static Vector3 getDebugPos()
        {
            return new Vector3(DebugPosX.Value, DebugPosY.Value, DebugPosZ.Value);
        }

        public static Vector3 getDebugRot()
        {
            return new Vector3(DebugRotX.Value, DebugRotY.Value, DebugRotZ.Value);
        }

        public static float getDebugScale() {
            return DebugScale.Value;
        }
#endif

        public static bool UnlockDesktopCursor()
        {
            return unlockDesktopCursor.Value;
        }

        public static string getQuickMenuType() {
            return QuickMenuType.Value;
        }
        public static int getQuickMenuVerticalAngle()
        {
            return QuickMenuVerticalAngle.Value;
        }

        public static bool GetQuickMenuIsSeperate()
        {
            return QuickMenuClassicSeperate.Value;
        }

        public static float GetSnapTurnAngle()
        {
            return snapTurnAngle.Value;
        }

        public static bool SnapTurnEnabled()
        {
            return snapTurnEnabled.Value;
        }

        public static bool SmoothSnapTurn()
        {
            return smoothSnapTurn.Value;
        }

        public static float SmoothSnapSpeed()
        {
            return Mathf.Abs(smoothSnapSpeed.Value);
        }

        public static bool WeaponNeedsSpeed()
        {
            return weaponNeedsSpeed.Value;
        }

        public static bool RoomScaleSneakEnabled() {
            return roomScaleSneaking.Value;
        }

        public static float RoomScaleSneakHeight() {
            return roomScaleSneakHeight.Value;
        }

        public static bool ExlusiveRoomScaleSneak()
        {
            return exclusiveRoomScaleSneak.Value;
        }

        public static bool GesturedLocomotion()
        {
            return gesturedLocomotion.Value;
        }

        public static float GetNearClipPlane()
        {
            return nearClipPlane.Value;
        }

        public static float GetEnemyRenderDistanceValue()
        {
            return enemyRenderDistance.Value;
        }

        public static float AltPieceRotationDelay()
        {
            return altPieceRotationDelay.Value;
        }

        public static bool ToggleRun()
        {
            return runIsToggled.Value;
        }

        public static bool LeftHanded()
        {
            return GetPreferredHand() == VRPlayer.LEFT_HAND;
        }

        public static bool ViewTurnWithMountedAnimal()
        {
            return viewTurnWithMountedAnimal.Value;
        }

        public static float ArrowParticleSize()
        {
            return arrowParticleSize.Value;
        }
        public static bool SpearThrowSpeedDynamic()
        {
            return spearThrowSpeedDynamic.Value;
        }
        public static bool SpearInverseWield()
        {
            return spearInverseWield.Value;
        }
        public static string SpearThrowType()
        {
            return spearThrowingType.Value;
        }
        public static bool TwoHandedWield()
        {
            return twoHandedWield.Value;
        }
        public static bool TwoHandedWithShield()
        {
            return twoHandedWithShield.Value;
        }
        public static bool UseSpearDirectionGraphic()
        {
            return useSpearDirectionGraphic.Value;
        }

        public static string CrossbowSaggitalRotationSource()
        {
            return crossbowSaggitalRotationSource.Value;
        }

        public static bool CrossbowManualReload()
        {
            return crossbowManualReload.Value;
        }

        public static string BlockingType()
        {
            return blockingType.Value;
        }

        public static bool UseLegacyHud()
        {
            return useLegacyHud.Value;
        }

        public static float CameraHudX()
        {
            return cameraHudX.Value;
        }

        public static float CameraHudY()
        {
            return cameraHudY.Value;
        }

        public static float CameraHudScale()
        {
            return cameraHudScale.Value;
        }

        public static Vector3 LeftWristPos()
        {
            return leftWristPos.Value;
        }

        public static Vector3 DefaultLeftWristPos()
        {
            return (Vector3)leftWristPos.DefaultValue;
        }

        public static Quaternion LeftWristRot()
        {
            return leftWristRot.Value;
        }

        public static Quaternion DefaultLeftWristRot()
        {
            return (Quaternion)leftWristRot.DefaultValue;
        }

        public static Vector3 RightWristPos()
        {
            return rightWristPos.Value;
        }

        public static Vector3 DefaultRightWristPos()
        {
            return (Vector3)rightWristPos.DefaultValue;
        }

        public static Quaternion RightWristRot()
        {
            return rightWristRot.Value;
        }

        public static Quaternion DefaultRightWristRot()
        {
            return (Quaternion)rightWristRot.DefaultValue;
        }

        public static string HealthPanelPlacement()
        {
            return healthPanelPlacement.Value;
        }

        public static string StaminaPanelPlacement()
        {
            return staminaPanelPlacement.Value;
        }

        public static string EitrPanelPlacement()
        {
            return eitrPanelPlacement.Value;
        }

        public static string StaggerPanelPlacement()
        {
            return staggerPanelPlacement.Value;
        }

        public static string MinimapPanelPlacement()
        {
            return minimapPanelPlacement.Value;
        }

        public static bool AllowHudFade()
        {
            return allowHudFade.Value;
        }

        public static bool HideHotbar()
        {
            return hideHotbar.Value && UseVrControls();
        }
        public static bool AlwaysShowStamina()
        {
            return alwaysShowStamina.Value;
        }

        public static Vector3 LeftWristQuickBarPos()
        {
            return leftWristQuickBarPos.Value;
        }
        public static Quaternion LeftWristQuickBarRot()
        {
            return leftWristQuickBarRot.Value;
        }
        public static Vector3 RightWristQuickBarPos()
        {
            return rightWristQuickBarPos.Value;
        }
        public static Quaternion RightWristQuickBarRot()
        {
            return rightWristQuickBarRot.Value;
        }
        public static Vector3 DominantHandWristQuickBarPos()
        {
            return LeftHanded() ? leftWristQuickBarPos.Value : rightWristQuickBarPos.Value;
        }
        public static Quaternion DominantHandWristQuickBarRot()
        {
            return LeftHanded() ? leftWristQuickBarRot.Value : rightWristQuickBarRot.Value;
        }
        public static Vector3 NonDominantHandWristQuickBarPos()
        {
            return LeftHanded() ? rightWristQuickBarPos.Value : leftWristQuickBarPos.Value;
        }
        public static Quaternion NonDominantHandWristQuickBarRot()
        {
            return LeftHanded() ? rightWristQuickBarRot.Value : leftWristQuickBarRot.Value;
        }
        public static Vector3 DefaultRightWristQuickBarPos()
        {
            return (Vector3)rightWristQuickBarPos.DefaultValue;
        }
        public static Quaternion DefaultRightWristQuickBarRot()
        {
            return (Quaternion)rightWristQuickBarRot.DefaultValue;
        }
        public static Vector3 DefaultLeftWristQuickBarPos()
        {
            return (Vector3)leftWristQuickBarPos.DefaultValue;
        }
        public static Quaternion DefaultLeftWristQuickBarRot()
        {
            return (Quaternion)leftWristQuickBarRot.DefaultValue;
        }
        public static bool QuickActionOnLeftHand()
        {
            return quickActionOnLeftHand.Value;
        }

        public static bool LockGuiWhileMenuOpen()
        {
            return lockGuiWhileInventoryOpen.Value;
        }

        public static bool AutoOpenKeyboardOnInteract()
        {
            return autoOpenKeyboardOnInteract.Value;
        }

        public static bool DisableRecenterPose()
        {
            return disableRecenterPose.Value;
        }

        public static bool AdvancedBuildingMode()
        {
            return advancedBuildMode.Value;
        }
        public static bool FreePlaceAutoReturn()
        {
            return freePlaceAutoReturn.Value;
        }
        public static bool AdvancedRotationUpWorld()
        {
            return advancedRotationUpWorld.Value;
        }
        public static bool BuildOnRelease()
        {
            return buildOnRelease.Value;
        }

        public static float[] BuildAngleSnap()
        {
            //"22.5, 15, 10, 5, 2.5, 1, 0.5"
            float[] snapList = Array.ConvertAll(buildAngleSnap.Value.Split(','), float.Parse);
            return snapList;
        }

        public static bool BhapticsEnabled()
        {
            bool bhapticsEnabledValue = bhapticsEnabled.Value;
            if (commandLineOverrides.ContainsKey(bhapticsEnabled.GetHashCode()))
            {
                bhapticsEnabledValue = commandLineOverrides[bhapticsEnabled.GetHashCode()];
            }
            return bhapticsEnabledValue && !NonVrPlayer();
        }

        public static bool BowAccuracyIgnoresDrawLength()
        {
            return bowAccuracyIgnoresDrawLength.Value;
        }
        public static float GetBowFullDrawLength()
        {
            return bowFullDrawLength.Value;
        }
        public static float GetBowStaminaScalar()
        {
            return bowStaminaAdjust.Value;
        }

        public static bool IsShipImmersiveCamera()
        {
            return immersiveShipCameraSitting.Value;
        }

        public static bool isShipImmersiveCameraStanding()
        {
            return immersiveShipCameraStanding.Value != "None";
        }

        public static string ShipImmersiveCameraType()
        {
            return immersiveShipCameraStanding.Value;
        }

        public static bool ImmersiveDodgeRoll()
        {
            return immersiveDodgeRoll.Value;
        }

        public static bool AllowMovementWhenInMenu()
        {
            return allowMovementWhenInMenu.Value;
        }

        public static float SmoothTurnSpeed()
        {
            return smoothTurnSpeed.Value;
        }

    }
}
