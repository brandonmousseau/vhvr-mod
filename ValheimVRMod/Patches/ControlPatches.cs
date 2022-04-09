using System.Collections.Generic;
using ValheimVRMod.VRCore;
using ValheimVRMod.VRCore.UI;
using HarmonyLib;
using System.Reflection;
using ValheimVRMod.Scripts;
using ValheimVRMod.Utilities;
using System.Reflection.Emit;
using UnityEngine;
using ValheimVRMod.Scripts.Block;

namespace ValheimVRMod.Patches {
    // These patches are used to inject the VR inputs into the game's control system

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown))]
    class ZInput_GetButtonDown_Patch {
        static bool Prefix(string name, ref bool __result) {
            // Need to bypass original function for any required ZInputs that begin
            // with "Joy" to ensure the VR Controls still work when
            // Gamepad is disabled.
            if (VRControls.mainControlsActive && !ZInput.IsGamepadEnabled() && isUsedJoyZinput(name)) {
                __result = VRControls.instance.GetButtonDown(name);
                return false;
            }

            return true;
        }

        private static bool isUsedJoyZinput(string name) {
            return name == "JoyMenu" ||
                   name == "JoyPlace" ||
                   name == "JoyPlace" ||
                   name == "JoyRemove";
        }

        static void Postfix(string name, ref bool __result) {
            if (VRControls.mainControlsActive) {
                __result = __result || VRControls.instance.GetButtonDown(name);
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonUp))]
    class ZInput_GetButtonUp_Patch {
        static void Postfix(string name, ref bool __result) {
            if (VRControls.mainControlsActive) {
                __result = __result || VRControls.instance.GetButtonUp(name);
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButton))]
    class ZInput_GetButton_Patch {
        static void Postfix(string name, ref bool __result) {
            if (VRControls.mainControlsActive) {
                __result = __result || VRControls.instance.GetButton(name);
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyLeftStickX))]
    class ZInput_GetJoyLeftStickX_Patch {
        static void Postfix(ref float __result) {
            if (VRControls.mainControlsActive) {
                __result = __result + VRControls.instance.GetJoyLeftStickX();
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyLeftStickY))]
    class ZInput_GetJoyLeftStickY_Patch {
        static void Postfix(ref float __result) {
            if (VRControls.mainControlsActive) {
                __result = __result + VRControls.instance.GetJoyLeftStickY();
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyRightStickX))]
    class ZInput_GetJoyRightStickX_Patch {
        static void Postfix(ref float __result) {
            if (VRControls.mainControlsActive) {
                
                if (ZInput_GetJoyRightStickY_Patch.isRunning 
                    && VRControls.instance.GetJoyRightStickX() > -0.5f 
                    && VRControls.instance.GetJoyRightStickX() < 0.5f)
                {
                    return;
                }
                
                __result = __result + VRControls.instance.GetJoyRightStickX();
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyRightStickY))]
    class ZInput_GetJoyRightStickY_Patch {

        private const float NON_TOGGLE_RUN_SENSITIVITY = -0.3f;
        private const float TOGGLE_RUN_SENSITIVITY = -0.8f;
        private const float CROUCH_SENSITIVITY = 0.7f;

        public static bool isCrouching;
        public static bool isRunning;

        static void Postfix(ref float __result) {
            if (VRControls.mainControlsActive) {
                var joystick = VRControls.instance.GetJoyRightStickY();

                isRunning = joystick < (VHVRConfig.ToggleRun() ? TOGGLE_RUN_SENSITIVITY : NON_TOGGLE_RUN_SENSITIVITY);
                isCrouching = joystick > CROUCH_SENSITIVITY;

                __result = __result + joystick;
            }
        }
    }

    // Patch to enable rotation of pieces using VR control actions
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    class Player_Update_Placement_PieceRotationPatch {
        static void Postfix(Player __instance, bool takeInput, ref int ___m_placeRotation) {
            if (!VRControls.mainControlsActive || __instance != Player.m_localPlayer || !takeInput ||
                !__instance.InPlaceMode() || Hud.IsPieceSelectionVisible()) {
                return;
            }

            ___m_placeRotation += VRControls.instance.getPieceRotation();
            var directRotate = VRControls.instance.getDirectPieceRotation();
            if (directRotate != 999)
            {
                ___m_placeRotation = directRotate;
            }
        }
    }

    //Force position nearby snap points
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    class PlacementSnapPoint
    {
        private static bool Prefix(Player __instance, GameObject ___m_placementGhost, GameObject ___m_placementMarkerInstance, Player.PlacementStatus ___m_placementStatus, int ___m_placeRotation)
        {
            if (!VRControls.mainControlsActive || __instance != Player.m_localPlayer || !___m_placementGhost || !___m_placementGhost.transform ||
               !__instance.InPlaceMode() || !BuildingManager.instance)
            {
                return true;
            }

            if (BuildingManager.instance.isCurrentlyFreeMode())
            {
                BuildingManager.instance.PrecisionUpdate(___m_placementGhost);
                if (___m_placementMarkerInstance)
                {
                    ___m_placementMarkerInstance.SetActive(false);
                }
                BuildingManager.instance.ValidateBuildingPiece(___m_placementGhost);
                return false;
            }

            return true;
        }
        private static void Postfix(Player __instance, GameObject ___m_placementGhost, GameObject ___m_placementMarkerInstance, Player.PlacementStatus ___m_placementStatus, int ___m_placeRotation)
        {
            if (!VRControls.mainControlsActive || __instance != Player.m_localPlayer || !___m_placementGhost || !___m_placementGhost.transform ||
                !__instance.InPlaceMode() || !BuildingManager.instance)
            {
                return;
            }
            if (BuildingManager.instance.isSnapMode() && !BuildingManager.instance.CheckMenuIsOpen())
            {
                var checkPlacement = BuildingManager.instance.UpdateSelectedSnapPoints(___m_placementGhost);
                Quaternion rotation = Quaternion.Euler(0f, 22.5f * (float)___m_placeRotation, 0f);

                ___m_placementMarkerInstance.transform.position = checkPlacement;
                ___m_placementMarkerInstance.transform.rotation = Quaternion.LookRotation(Vector3.up, rotation * Vector3.forward);
                ___m_placementGhost.transform.position = checkPlacement;
                ___m_placementGhost.transform.rotation = rotation;
            }

            BuildingManager.instance.UpdateRotationAdvanced(___m_placementGhost);
            BuildingManager.instance.ValidateBuildingPiece(___m_placementGhost);
        }
    }

    [HarmonyPatch(typeof(Player), "FindClosestSnappoint")]
    class ChangeSnapPointMaxDistance
    {
        private static void Prefix(Player __instance, ref float maxDistance)
        {
            if (!VRControls.mainControlsActive || __instance != Player.m_localPlayer ||
                !__instance.InPlaceMode() || !BuildingManager.instance)
            {
                return;
            }

            if (BuildingManager.instance.IsReferenceMode())
            {
                maxDistance = 10f;
            }
        }
    }

    // If using VR controls, disable the joystick for the purposes
    // of moving the map around since that will be done with
    // simulated mouse cursor click and drag via laser pointer.
    [HarmonyPatch(typeof(Minimap), "UpdateMap")]
    class Minimap_UpdateMap_MapTranslationPatch {
        private static MethodInfo getJoyLeftStickX =
            AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyLeftStickX), new [] { typeof(bool) });

        private static MethodInfo getJoyLeftStickY =
            AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyLeftStickY), new[] { typeof(bool) });

        private static float getJoyLeftStickXPatched(bool smooth) {
            if (VRControls.mainControlsActive) {
                return 0.0f;
            }

            return ZInput.GetJoyLeftStickX(smooth: true);
        }

        private static float getJoyLeftStickYPatched(bool smooth) {
            if (VRControls.mainControlsActive) {
                return 0.0f;
            }

            return ZInput.GetJoyLeftStickY();
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            foreach (var instruction in original) {
                if (instruction.Calls(getJoyLeftStickX)) {
                    patched.Add(CodeInstruction.Call(typeof(Minimap_UpdateMap_MapTranslationPatch),
                        nameof(getJoyLeftStickXPatched), new[] { typeof(bool) }));
                }
                else if (instruction.Calls(getJoyLeftStickY)) {
                    patched.Add(CodeInstruction.Call(typeof(Minimap_UpdateMap_MapTranslationPatch),
                        nameof(getJoyLeftStickYPatched), new[] { typeof(bool) }));
                }
                else {
                    patched.Add(instruction);
                }
            }
        
            return patched;
        }
    }

    // This patch allows for optional use of releasing the trigger to build things
    class BuildOnReleasePatches
    {

        // We need to track when the piece selection menu was just hidden
        // as a result of the user clicking on it so that we don't
        // cause a placement right after the menu is closed and the
        // trigger is released.
        [HarmonyPatch(typeof(Hud), nameof(Hud.HidePieceSelection))]
        class BuildHudTracker
        {

            public static bool buildHudJustToggledOff = false;

            static void Postfix()
            {
                buildHudJustToggledOff = true;
            }

        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacement))]
        class Player_UpdatePlacement_BuildInputPatch
        {
            private static MethodInfo getButtonDownMethod =
                AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButtonDown), new[] { typeof(string) });

            private static readonly string placementInput = "JoyPlace";

            private static bool ShouldTriggerBuildPlacement(string inputName)
            {
                if (!BuildingManager.instance)
                {
                    return ZInput.GetButtonDown(inputName);
                }
                if (VHVRConfig.BuildOnRelease())
                {
                    bool inputReceived = ZInput.GetButtonUp(inputName);
                    if (BuildHudTracker.buildHudJustToggledOff && inputReceived)
                    {
                        // Since the build hud was just toggled off and the input was receieved,
                        // we won't trigger the placement. Instead just reset the "buildHudJustToggledOff" flag
                        LogUtils.LogDebug("Resetting buildHudToggledFlag");
                        BuildHudTracker.buildHudJustToggledOff = false;
                        return false;
                    } else
                    {
                        if (inputReceived && !BuildingManager.instance.isCurrentlyMoving() && VHVRConfig.FreePlaceAutoReturn())
                        {
                            BuildingManager.instance.ExitPreciseMode();
                        }
                        if (BuildingManager.instance.isHoldingJump())
                            return false;
                        return inputReceived;
                    }
                }
                else
                {
                    if (ZInput.GetButtonDown(inputName) && !BuildingManager.instance.isCurrentlyMoving() && VHVRConfig.FreePlaceAutoReturn())
                    {
                        BuildingManager.instance.ExitPreciseMode();
                    }
                    return ZInput.GetButtonDown(inputName);
                }
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var original = new List<CodeInstruction>(instructions);
                var patched = new List<CodeInstruction>();
                if (!VHVRConfig.UseVrControls())
                {
                    return original;
                }
                for (int i = 0; i < original.Count; i++)
                {
                    var instruction = original[i];
                    patched.Add(instruction);
                    if (instruction.opcode == OpCodes.Ldstr && placementInput.Equals(instruction.operand) && original[i + 1].Calls(getButtonDownMethod))
                    {
                        i++; // skip the next instruction cause we are replacing it
                        patched.Add(CodeInstruction.Call(typeof(Player_UpdatePlacement_BuildInputPatch),
                            nameof(Player_UpdatePlacement_BuildInputPatch.ShouldTriggerBuildPlacement)));
                    }
                }
                return patched;
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    class Player_SetControls_RunPatch
    {

        private static bool lastUpdateRunInput = false;
        private static bool runToggledOn = false;

        static void Prefix(Player __instance, ref bool run)
        {
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return;
            }
            if (VHVRConfig.ToggleRun())
            {
                handleRunToggle(ref run);
            }
            else
            {
                run = run || ZInput_GetJoyRightStickY_Patch.isRunning;
            }
        }

        private static void handleRunToggle(ref bool run)
        {
            bool runIsTriggered = ZInput_GetJoyRightStickY_Patch.isRunning && !lastUpdateRunInput;
            bool crouchApplied = ZInput_GetJoyRightStickY_Patch.isCrouching;
            if (crouchApplied || !VRPlayer.isMoving)
            {
                // If the player presses crouch or stops moving, then always stop running.
                runToggledOn = false;
            }
            else if (runIsTriggered)
            {
                // If the player applies sprint input this update, toggle the sprint.
                runToggledOn = !runToggledOn;
            }
            run = runToggledOn;
            lastUpdateRunInput = ZInput_GetJoyRightStickY_Patch.isRunning;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    public class Player_SetControls_SneakPatch
    {

        public static bool isJoystickSneaking { get { return _isJoystickSneaking; } }
        private static bool _isJoystickSneaking = false;

        // Used for joystick crouch to make sure the player
        // returns the joystick out of "crouch" position to reset
        // it so it doesn't toggle continuously while held down
        private static bool lastUpdateCrouchInput = false;

        // bool crouch is a toggle
        static void Prefix(Player __instance, ref bool crouch)
        {
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return;
            }
            // Indicates whether the crouch is toggled on or not
            //bool isCrouchToggled = AccessTools.FieldRefAccess<Player, bool>(__instance, "m_crouchToggled");
            bool isCrouchToggled = __instance.m_crouchToggled;
            if (VHVRConfig.RoomScaleSneakEnabled())
            {
                handleRoomscaleSneak(__instance, ref crouch, isCrouchToggled);
            }
            else
            {
                handleControllerOnlySneak(__instance, ref crouch, isCrouchToggled);
            }
        }

        static void handleRoomscaleSneak(Player player, ref bool crouch, bool isCrouchToggled)
        {
            if (VRPlayer.isRoomscaleSneaking)
            {
                // First check if player is crouching in real life and
                // use that as highest priority input.
                if (!isCrouchToggled)
                {
                    crouch = true;
                }
                _isJoystickSneaking = false;
                lastUpdateCrouchInput = false;
                // Return immediately since we want to treat
                // physical crouching as higher priority
                return;
            } else if (isCrouchToggled && !_isJoystickSneaking)
            {
                // Player is not crouching physically, but game character is
                // in crouch mode, so toggle it off
                lastUpdateCrouchInput = false;
                _isJoystickSneaking = false;
                crouch = true;
            } else
            {
                // Don't do any toggling.
                crouch = false;
            }
            if (!VHVRConfig.ExlusiveRoomScaleSneak())
            {
                // Player is physically standing, but may still want to crouch using joystick
                handleControllerOnlySneak(player, ref crouch, isCrouchToggled);
            }
        }

        static void handleControllerOnlySneak(Player player, ref bool crouch, bool isCrouchToggled)
        {
            bool crouchToggleTriggered = ZInput_GetJoyRightStickY_Patch.isCrouching && !lastUpdateCrouchInput;
            bool standupTriggered = ZInput_GetJoyRightStickY_Patch.isRunning;
            if (crouchToggleTriggered)
            {
                crouch = true;
                if (isCrouchToggled)
                {
                    // Player is currently crouching, but we just set the crouch trigger to "true"
                    // which means the player will about to be not crouching anymore.
                    _isJoystickSneaking = false;
                } else
                {
                    // Player is about to be toggled to crouch position;
                    _isJoystickSneaking = true;
                }
            } else if (standupTriggered)
            {
                if (isCrouchToggled) {
                    // The standup input was applied and crouch is currently triggered on,
                    // so trigger the toggle to make it turn off
                    crouch = true;
                    _isJoystickSneaking = false;
                }
            }
            // Save for next update
            lastUpdateCrouchInput = ZInput_GetJoyRightStickY_Patch.isCrouching;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    class Player_SetControls_EquipPatch {
      
        static void Prefix(Player __instance, ref bool attack, ref bool attackHold, ref bool block, ref bool blockHold,
            ref bool secondaryAttack) {
            if (!VHVRConfig.UseVrControls() || __instance != Player.m_localPlayer) {
                return;
            }

            if (EquipScript.getLeft() == EquipType.Bow) {
                if (BowLocalManager.aborting) {
                    block = true;
                    blockHold = true;
                    BowLocalManager.aborting = false;
                }
                else if (BowLocalManager.startedPulling) {
                    attack = true;
                    BowLocalManager.startedPulling = false;
                }
                else {
                    attackHold = BowLocalManager.isPulling;
                }
                return;
            }

            if (EquipScript.getLeft() == EquipType.Shield) {
                blockHold = ShieldBlock.instance?.isBlocking() ?? false;
            }

            switch (EquipScript.getRight()) {
                case EquipType.Fishing:
                    if (FishingManager.isThrowing) {
                        attack = true;
                        attackHold = true;
                        FishingManager.isThrowing = false;
                    }
                    
                    blockHold = FishingManager.isPulling;
                    break;

                case EquipType.Spear:
                    if (SpearManager.isThrowing) {
                        secondaryAttack = true;
                        SpearManager.isThrowing = false;
                    }
                    
                    break;
                
                case EquipType.SpearChitin:
                case EquipType.ThrowObject:
                    if (SpearManager.isThrowing) {
                        attack = true;
                        SpearManager.isThrowing = false;
                    }

                    break;
                
                case EquipType.Tankard:
                    if (WeaponCollision.isDrinking) {
                        attack = true;
                        WeaponCollision.isDrinking = false;
                    }

                    break;
            }
        }
    }

    
    // Used to make stack splitting easier
    [HarmonyPatch(typeof(InventoryGui), "Awake")]
    class InventoryGui_Awake_Patch {
        static void Prefix(InventoryGui __instance)
        {
            __instance.m_splitSlider.gameObject.AddComponent<SliderSelector>();
        }
    }

    // Used to enable stack splitting in inventory
    [HarmonyPatch(typeof(InventoryGrid), "OnLeftClick")]
    class InventoryGrid_OnLeftClick_Patch {

        static bool getClickModifier()
        {
            return VRControls.laserControlsActive && VRControls.instance.getClickModifier();
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            if (!VHVRConfig.UseVrControls())
            {
                return original;
            }
            bool addedInstruction = false;
            for (int i = 0; i < original.Count; i++)
            {
                var instruction = original[i];
                if (!addedInstruction && instruction.opcode == OpCodes.Ldc_I4)
                {
                    int operand = (int)instruction.operand;
                    if (operand == (int)KeyCode.LeftShift)
                    {
                        // Add our custom check
                        patched.Add(CodeInstruction.Call(typeof(InventoryGrid_OnLeftClick_Patch), nameof(getClickModifier)));
                        addedInstruction = true;
                        // Skip over the next instruction too since it will be the keycode comparison
                        i++;
                    }
                }
                else
                {
                    patched.Add(instruction);
                }
            }
            return patched;
        }
    }

    // This patch enables adding map pins without needing to "Double Click".
    // Instead it is triggered using the "click modifier" plus a single left click.
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapLeftClick))]
    class MinimapAddPinPatch
    {
        static void Postfix(Minimap __instance)
        {
            if (!VHVRConfig.UseVrControls())
            {
                return;
            }
            if (VRControls.instance.getClickModifier())
            {
                __instance.OnMapDblClick();
            }
        }
    }

    // This patch hijacks the right click input on minimap to enable
    // adding map pings. With a normal right click, the default behavior
    // exists where a map pin will be removed. If the click modifier
    // is held down, then instead of removing a pin, a map ping will
    // be sent. (Alternative may be to just add a "middle click" button
    // to laser pointer controls, but since there are overlapping controls
    // between laser pointers and normal controls, things can end up being
    // extra complex when we need to use a new button. Since we already have
    // the modifier, this is simpler).
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapRightClick))]
    class MinimapPingPatch
    {
        static bool Prefix(Minimap __instance)
        {
            if (!VHVRConfig.UseVrControls() || !VRControls.instance.getClickModifier())
            {
                return true;
            }
            Chat.instance.SendPing(__instance.ScreenToWorldPoint(Input.mousePosition));
            return false;
        }
    }

    class SnapTurnPatches
    {
        [HarmonyPatch(typeof(Player), nameof(Player.SetMouseLook))]
        class Player_SetMouseLook_Patch
        {

            private static readonly float MINIMUM_SNAP_SENSITIVITY = 1f;
            private static readonly float SMOOTH_SNAP_INCREMENT_TIME_DELTA = 0.01f;
            private static bool snapTriggered = false;
            private static bool isSmoothSnapping = false;

            private static float currentSmoothSnapAmount = 0f;
            private static float currentDt = 0f;
            private static int smoothSnapDirection = 1;

            static void Prefix(Player __instance, ref Vector2 mouseLook)
            {
                if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls() || !VHVRConfig.SnapTurnEnabled())
                {
                    return;
                }
                if (snapTriggered && !isSmoothSnapping)
                {
                    if (turnInputApplied(mouseLook.x))
                    {
                        mouseLook.x = 0f;
                        return;
                    }
                    snapTriggered = false;
                }
                if (VHVRConfig.SmoothSnapTurn())
                {
                    handleSmoothSnap(ref mouseLook);
                } else
                {
                    handleImmediateSnap(ref mouseLook);
                }
            }

            private static void handleSmoothSnap(ref Vector2 mouseLook)
            {
                if (!isSmoothSnapping)
                {
                    // On this update, we are not currently smooth snapping.
                    // Check if the turnInput is applied and trigger the snap if it is
                    // otherwise set turn angle to zero.
                    if (turnInputApplied(mouseLook.x))
                    {
                        isSmoothSnapping = true;
                        snapTriggered = true;
                        // reset the current smooth snapped amount/dt
                        currentSmoothSnapAmount = 0f;
                        currentDt = 0f;
                        smoothSnapDirection = (mouseLook.x > 0) ? 1 : -1;
                        // Determine how much the current update should snap by
                        float snapIncrementAmount = calculateSmoothSnapAngle(mouseLook.x);
                        currentSmoothSnapAmount += snapIncrementAmount;
                        if (Mathf.Abs(currentSmoothSnapAmount) >= VHVRConfig.GetSnapTurnAngle())
                        {
                            // Immediately hit the snap target ? Config is probably weirdly set.
                            // Handle this case anyways.
                            isSmoothSnapping = false;
                        }
                        mouseLook.x = snapIncrementAmount;
                    } else
                    {
                        snapTriggered = false;
                        mouseLook.x = 0f;
                    }
                } else
                {
                    // We are in the middle of a smooth snap
                    float snapIncrementAmount = calculateSmoothSnapAngle(mouseLook.x);
                    currentSmoothSnapAmount += snapIncrementAmount;
                    if (Mathf.Abs(currentSmoothSnapAmount) >= VHVRConfig.GetSnapTurnAngle())
                    {
                        // We've exceeded our target, so disable smooth snapping
                        isSmoothSnapping = false;
                    }
                    mouseLook.x = snapIncrementAmount;
                }
            }

            private static void handleImmediateSnap(ref Vector2 mouseLook)
            {
                if (turnInputApplied(mouseLook.x))
                {
                    // The player triggered a turn this update, so incremement
                    // by the full snap angle.
                    snapTriggered = true;
                    mouseLook.x = (mouseLook.x > 0 ? VHVRConfig.GetSnapTurnAngle() : -VHVRConfig.GetSnapTurnAngle());
                    return;
                }
                else
                {
                    snapTriggered = false;
                    mouseLook.x = 0f;
                }
            }

            private static float calculateSmoothSnapAngle(float mouseX)
            {
                float dt = Time.unscaledDeltaTime;
                currentDt += dt;
                if (currentDt < SMOOTH_SNAP_INCREMENT_TIME_DELTA)
                {
                    return 0f;
                } else
                {
                    // We've hit our deltaT target, so reset it and continue
                    // with calculating the next increment.
                    currentDt = 0f;
                }
                float finalSnapTarget = VHVRConfig.GetSnapTurnAngle() * smoothSnapDirection;
                float smoothSnapIncrement = VHVRConfig.SmoothSnapSpeed() * smoothSnapDirection;
                if (Mathf.Abs(finalSnapTarget) > Mathf.Abs(currentSmoothSnapAmount + smoothSnapIncrement))
                {
                    // We can still increment by the full "smoothSnapIncrement" and 
                    // be below our final target.
                    return smoothSnapIncrement;
                } else
                {
                    // If we increment by the full amount, we'll exceed our target, so
                    // we should only return the difference
                    return (Mathf.Abs(finalSnapTarget) - Mathf.Abs(currentSmoothSnapAmount)) * smoothSnapDirection;
                }
            }

            private static bool turnInputApplied(float angle)
            {
                return Mathf.Abs(angle) > MINIMUM_SNAP_SENSITIVITY;
            }
        }
    }
}