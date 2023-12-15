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
using Valve.VR;

namespace ValheimVRMod.Patches {
    // These patches are used to inject the VR inputs into the game's control system

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown))]
    class ZInput_GetButtonDown_Patch {
        private static HashSet<string> pendingButtons = new HashSet<string>();

        static public void EmulateButtonDown(string name)
        {
            pendingButtons.Add(name);
        }

        static bool Prefix(string name, ref bool __result) {
            if (pendingButtons.Remove(name)) {
                __result = true;
                return false;
            }

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

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetKeyDown))]
    class ZInput_GetKeyDown_Patch
    {
        private static HashSet<KeyCode> pendingKeys = new HashSet<KeyCode>();

        static public void EmulateKeyDown(KeyCode key)
        {
            pendingKeys.Add(key);
        }

        static bool Prefix(KeyCode key, ref bool __result)
        {
            if (pendingKeys.Remove(key))
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyLeftStickX))]
    class ZInput_GetJoyLeftStickX_Patch {
        static void Postfix(ref float __result) {
            if (VRControls.mainControlsActive) {
                var joystick = VRControls.instance.GetJoyLeftStickX();

                if (Player.m_localPlayer.IsAttached())
                {
                    if (joystick > -0.3f && joystick < 0.3f)
                    {
                        __result = 0f;
                        return;
                    }
                }
                __result = __result + VRControls.instance.GetJoyLeftStickX() + (VRPlayer.gesturedLocomotionManager?.stickOutputX ?? 0);
            }
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetJoyLeftStickY))]
    class ZInput_GetJoyLeftStickY_Patch {
        static void Postfix(ref float __result) {
            if (VRControls.mainControlsActive) {

                var joystick = VRControls.instance.GetJoyLeftStickY();

                //add deadzone to ship control for forward and backward so its harder to accidentally change speed
                if (Player.m_localPlayer?.GetControlledShip())
                {
                    if(joystick > -0.9f && joystick < 0.9f)
                    {
                        __result = 0f;
                        return;
                    }
                }
                __result = __result + joystick + (VRPlayer.gesturedLocomotionManager?.stickOutputY?? 0);
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

                // When toggling running, disable turning left or right if the the x-rotation amount of the stick is less than the y-rotation amount.
                // This prevents unwanted accidental turning when moving the stick forward or backward.
                // TODO: examine whether this check should be enabled for smooth-turn mode as well.
                if (VHVRConfig.SnapTurnEnabled() && VHVRConfig.ToggleRun() && Mathf.Abs(VRControls.instance.GetJoyRightStickX()) < Mathf.Abs(VRControls.instance.GetJoyRightStickY()))
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
        private const float TOGGLE_RUN_SENSITIVITY = -0.85f;
        private const float CROUCH_SENSITIVITY = 0.85f;

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

    [HarmonyPatch(typeof(PlayerController), "LateUpdate")]
    class PlayerController_LateUpdate_Patch
    {
        private static MethodInfo IsGamepadActive =
             AccessTools.Method(typeof(ZInput), nameof(ZInput.IsGamepadActive));

        private static bool IsGamepadActivePatched()
        {
            if (VHVRConfig.NonVrPlayer() || !VHVRConfig.UseVrControls())
            {
                return ZInput.IsGamepadActive();
            }

            // Make the vanilla game believe that the gamepad is active so that it will use ZInput.GetJoyRightStickX() which we patch to turn the player left/right.
            return true;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            var patched = new List<CodeInstruction>();
            foreach (var instruction in original)
            {
                if (instruction.Calls(IsGamepadActive))
                {
                    patched.Add(
                        CodeInstruction.Call(typeof(PlayerController_LateUpdate_Patch), nameof(IsGamepadActivePatched)));
                }
                else
                {
                    patched.Add(instruction);
                }
            }

            return patched;
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
        private static bool Prefix(Player __instance, GameObject ___m_placementGhost, GameObject ___m_placementMarkerInstance)
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
        private static void Postfix(Player __instance, GameObject ___m_placementGhost)
        {
            if (!VRControls.mainControlsActive || __instance != Player.m_localPlayer || !___m_placementGhost || !___m_placementGhost.transform ||
                !__instance.InPlaceMode() || !BuildingManager.instance)
            {
                return;
            }

            if (BuildingManager.instance.isSnapMode() && !VRPlayer.IsClickableGuiOpen)
            {
                BuildingManager.instance.UpdateSelectedSnapPoints(___m_placementGhost);
            }

            BuildingManager.instance.UpdateRotationAdvanced(___m_placementGhost);

            if (BuildingManager.instance.isSnapMode())
            {
                return;
            }

            BuildingManager.instance.ValidateBuildingPiece(___m_placementGhost);
        }
    }
    [HarmonyPatch(typeof(Player), "PieceRayTest")]
    class Player_PieceRayTest
    {
        static void Postfix(Player __instance, Piece piece)
        {
            if (!VRControls.mainControlsActive || __instance != Player.m_localPlayer ||
               !__instance.InPlaceMode() || !BuildingManager.instance)
            {
                return;
            }

            if (piece)
            {
                BuildingManager.instance.rayTracedPiece = piece;
            }
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
                run = run || ZInput_GetJoyRightStickY_Patch.isRunning || (VRPlayer.gesturedLocomotionManager?.isRunning?? false);
            }
        }

        private static void handleRunToggle(ref bool run)
        {
            bool runIsTriggered = ZInput_GetJoyRightStickY_Patch.isRunning && !lastUpdateRunInput;
            bool crouchApplied = ZInput_GetJoyRightStickY_Patch.isCrouching;
            if (crouchApplied || !VRPlayer.isMoving || Player.m_localPlayer.m_stamina < 1)
            {
                // If the player presses crouch or stops moving, then always stop running.
                runToggledOn = false;
            }
            else if (runIsTriggered)
            {
                // If the player applies sprint input this update, toggle the sprint.
                runToggledOn = !runToggledOn;
            }
            run = runToggledOn || (VRPlayer.gesturedLocomotionManager?.isRunning?? false);
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
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls() || VRPlayer.ShouldPauseMovement)
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
        protected static float timer = 2f;
        protected static float timeEnd = 2f;
        static void Prefix(Player __instance, ref bool attack, ref bool attackHold, ref bool block, ref bool blockHold,
            ref bool secondaryAttack) {
            if (!VHVRConfig.UseVrControls() || __instance != Player.m_localPlayer) {
                return;
            }
            timer = timer <= timeEnd ? timer + Time.deltaTime : timeEnd;

            if (EquipScript.getLeft() == EquipType.Bow) {
                if (BowLocalManager.aborting) {
                    block = true;
                    blockHold = true;
                    BowLocalManager.aborting = false;
                }
                else if (BowLocalManager.startedPulling) {
                    if (Player.m_localPlayer.GetLeftItem().m_shared.m_attack.m_bowDraw)
                        attack = true;
                    BowLocalManager.startedPulling = false;
                }
                else {
                    if (Player.m_localPlayer.GetLeftItem().m_shared.m_attack.m_bowDraw)
                    {
                        attackHold = BowLocalManager.isPulling;
                    }
                    else
                    {
                        
                        if (BowLocalManager.isPulling && SteamVR_Actions.valheim_Use.state && timer >= timeEnd)
                        {
                            timeEnd = 2f;
                            timer = 0f;
                            attack = true;
                            attackHold = true;
                            VRPlayer.rightHand.hapticAction.Execute(0, 0.1f, 75, 0.3f, SteamVR_Input_Sources.RightHand);
                        }
                        else
                        {
                            attack = false;
                            attackHold = false ;
                        }
                        var currentAnimatorClip = Player.m_localPlayer.m_animator.GetCurrentAnimatorClipInfo(0)?[0].clip;
                        if (currentAnimatorClip?.name == "Bow Aim Recoil")
                        {
                            timeEnd = currentAnimatorClip.length / PatchFixedUpdate.lastSpeedUp;
                        }
                    }
                    
                }
                return;
            }

            if (EquipScript.getLeft() == EquipType.Shield) {
                blockHold = ShieldBlock.instance?.isBlocking() ?? false;
            }

            if (EquipScript.getLeft() == EquipType.Magic && MagicWeaponManager.AttemptingAttack)
            {
                attack = true;
                attackHold = true;
            }

            if (EquipScript.getLeft() == EquipType.Crossbow && CrossbowManager.IsPullingTrigger())
            {
                attack = true;
                attackHold = true;
                CrossbowMorphManager.instance.destroyBolt();
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
                    if (ThrowableManager.isThrowing) {
                        secondaryAttack = true;
                        ThrowableManager.isThrowing = false;
                    }
                    
                    break;
                
                case EquipType.SpearChitin:
                case EquipType.ThrowObject:
                    if (ThrowableManager.isThrowing) {
                        attack = true;
                        ThrowableManager.isThrowing = false;
                    }

                    break;
                
                case EquipType.Tankard:
                    if (WeaponCollision.isDrinking) {
                        attack = true;
                        WeaponCollision.isDrinking = false;
                    }

                    break;
                case EquipType.Magic:
                    if (MagicWeaponManager.AttemptingAttack)
                    {
                        attack = true;
                        attackHold = true;
                        SwingLaunchManager.isThrowing = false;
                    }
                    break;


                case EquipType.RuneSkyheim:
                    if (SteamVR_Actions.valheim_Use.state && SteamVR_Actions.valheim_Grab.state && timer >= timeEnd)
                    {
                        timeEnd = 2f;
                        timer = 0f;
                        attack = true;
                    }
                    var currentAnimatorClip = Player.m_localPlayer.m_animator.GetCurrentAnimatorClipInfo(0)?[0].clip;
                    if (currentAnimatorClip?.name == "spear_throw")
                    {
                        timeEnd = currentAnimatorClip.length / PatchFixedUpdate.lastSpeedUp;
                    }
                    break;
            }

            if (EquipScript.isThrowable(__instance.GetRightItem()) && ThrowableManager.isThrowing)
            {
                secondaryAttack = true;
                ThrowableManager.isThrowing = false;
            }
        }
    }


    // Used to make stack splitting easier
    [HarmonyPatch(typeof(InventoryGui), "Awake")]
    class InventoryGui_Awake_Patch {
        static void Prefix(InventoryGui __instance)
        {
            if (VHVRConfig.NonVrPlayer())
            {
                return;
            }
            __instance.m_splitSlider.gameObject.AddComponent<SliderSelector>();
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
    class InventoryGui_Update_Patch
    {
        static bool allowQuickStackAll = true;
        static void Prefix(InventoryGui __instance)
        {
            if (VHVRConfig.NonVrPlayer() || !VHVRConfig.UseVrControls())
            {
                return;
            }
            if (!__instance.IsContainerOpen())
            {
                // When a container is no longer open, reset this flag so that
                // quick-stack-all can be used next time the player interacts with a container.
                allowQuickStackAll = true;
            }
            else if (SteamVR_Actions.laserPointers_LeftClick.GetStateUp(SteamVR_Input_Sources.Any))
            {
                // When a container is open, the GUI is open so laser pointers take priority over valheim_Use.
                // As the player releases the trigger when the container is open, the button-up state of vaheim_Use is therefore not detected.
                // The game will mistakenly think that the use button is still being pressed and hold, triggering quick-stack-all inadvertently
                // so we must patch to prevent that from happening. 
                // Note: this flag will stay false for the rest of the entire duration when the current container is open
                // so that dragging item spliiter will not trigger quick-stack-all either.
                // TODO: try find a way to fix the wrong state of valheim_Use instead of using this ad hoc patch.
                allowQuickStackAll = false;
            }
            if (!allowQuickStackAll || !SteamVR_Actions.laserPointers_LeftClick.GetState(SteamVR_Input_Sources.Any)) {
                // Quick-stack-all is triggered by holding the use button and resetting this timer disables quick-stack-all.
                __instance.m_containerHoldTime = 0;
            }
        }
    }

    // Used to enable split and move in inventory
    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    static class InventoryGui_OnSelectedItem_Patch
    {
        static void Prefix(InventoryGui __instance, ref InventoryGrid.Modifier mod)
        {
            if (!VHVRConfig.UseVrControls())
                return;

            if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand))
                mod = InventoryGrid.Modifier.Split;
            else if (SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand))
                mod = InventoryGrid.Modifier.Move;
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.GetHoveredElement))]
    static class InventoryGrid_GetHoveredElement_Patch
    {
        static bool Prefix(InventoryGrid __instance, ref InventoryGrid.Element __result)
        {
            if (VHVRConfig.NonVrPlayer())
            {
                return true;
            }
            foreach (InventoryGrid.Element element in __instance.m_elements)
            {
                RectTransform rectTransform = element.m_go.transform as RectTransform;
                // Use SoftwareCursor.ScaledMouseVector() instead of the vanilla Input.mousePosition to support VR GUI.
                Vector2 point = rectTransform.InverseTransformPoint(SoftwareCursor.ScaledMouseVector());
                if (rectTransform.rect.Contains(point))
                {
                    __result = element;
                    return false;
                }
            }
            __result = null;
            return false;
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
    //Add vibration to the controller when fish is biting the bait
    [HarmonyPatch(typeof(FishingFloat), "RPC_Nibble")]
    class FishGetCatch
    {
        static void Postfix(FishingFloat __instance)
        {
            if (!VRControls.mainControlsActive || !__instance || !FishingManager.instance)
            {
                return ;
            }
            FishingManager.instance.TriggerVibrateFish(__instance);
        }
    }
    // ENABLE DODGE
    [HarmonyPatch(typeof(Player), "Update")]
    class Player_UpdateDodge_Patch
    {
        public static bool wasDodging = false;
        static void Postfix(Player __instance)
        {
            if (VHVRConfig.NonVrPlayer())
                return;

            Vector3 dir = __instance.GetMoveDir();
            if (dir == Vector3.zero)
                return;

            if (SteamVR_Actions.valheim_UseLeft.state && SteamVR_Actions.valheim_Jump.stateDown)
            {
                if (__instance.m_stamina < __instance.m_dodgeStaminaUsage)
                {
                    // FIXME: Mystlands probably changed this from StaminaBarNoStaminaFlash
                    Hud.instance.StaminaBarEmptyFlash();
                    return;
                }
                __instance.Dodge(dir);
                wasDodging = true;
            }
        }
    }

    [HarmonyPatch(typeof(Player), "UpdateDodge")]
    class UpdateDodgeVr
    {
        public static float currdodgetimer { get; private set; } = 0f;
        static Vector3 currDodgeDir;
        static bool Prefix(Player __instance, float dt)
        {
            if (__instance != Player.m_localPlayer || !VHVRConfig.UseVrControls())
            {
                return true;
            }

            __instance.m_queuedDodgeTimer -= dt;
            currdodgetimer -= dt;

            if (__instance.m_queuedDodgeTimer > 0f && __instance.IsOnGround() && !__instance.IsDead() && !__instance.InAttack() && !__instance.IsEncumbered() && !__instance.InDodge() && !__instance.IsStaggering())
            {
                float num = __instance.m_dodgeStaminaUsage - __instance.m_dodgeStaminaUsage * __instance.m_equipmentMovementModifier;
                if (__instance.HaveStamina(num))
                {
                    __instance.ClearActionQueue();
                    __instance.m_queuedDodgeTimer = 0f;
                    currdodgetimer = 0.8f;
                    currDodgeDir = __instance.transform.forward;
                    __instance.m_dodgeInvincible = true;
                    __instance.m_zanim.SetTrigger("dodge");
                    __instance.AddNoise(5f);
                    __instance.UseStamina(num);
                    __instance.m_dodgeEffects.Create(__instance.transform.position, Quaternion.identity, __instance.transform, 2f, -1);
                }
            }

            AnimatorStateInfo currentAnimatorStateInfo = __instance.m_animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo nextAnimatorStateInfo = __instance.m_animator.GetNextAnimatorStateInfo(0);
            bool flag = __instance.m_animator.IsInTransition(0);
            bool flag2 = __instance.m_animator.GetBool("dodge") || (currentAnimatorStateInfo.tagHash == Player.s_animatorTagDodge && !flag) || (flag && nextAnimatorStateInfo.tagHash == Player.s_animatorTagDodge);
            bool value = flag2 && __instance.m_dodgeInvincible;
            __instance.m_nview.GetZDO().Set("dodgeinv", value);
            __instance.m_inDodge = flag2;
            if (currdodgetimer > 0)
            {
                __instance.m_rootMotion = (__instance.m_queuedDodgeDir.normalized / 11) - (currDodgeDir / 15);
            }
            else
            {
                Player_UpdateDodge_Patch.wasDodging = false;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.SetMouseLook))]
    class Player_SmoothTurnSpeed_Patch
    {
        static void Prefix(ref Vector2 mouseLook)
        {
            if (!VHVRConfig.UseVrControls() || VHVRConfig.SnapTurnEnabled())
            {
                return;
            }
            mouseLook.x *= VHVRConfig.SmoothTurnSpeed();
        }
    }
}
