using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.Extras;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts
{
    // Manages summoning items (dead raiser, spirit caller), which summon either by pressing the trigger of the hand
    // holding them or by raising the other hand. It is deliberately not a magic staff manager: summoners have no
    // secondary attack, and they are not necessarily main hand weapons, so everything here is relative to the hand
    // actually holding the item.
    public class SummonerManager : MonoBehaviour
    {
        public static readonly HashSet<string> ITEM_NAMES =
            new HashSet<string>(new string[] { "$item_staffskeleton", "$item_staff_spiritcaller" });

        private const float MIN_SUMMONING_HAND_SPEED = 0.25f;
        private const float SUMMON_TIME = 1;

        public static SummonerManager instance;

        // Whether the item is held in the main weapon hand rather than the off hand (e.g. the dead raiser).
        public bool isHeldInMainHand;

        private float currentMaxHandHeight = Mathf.NegativeInfinity;
        private float summonTimer = 0;
        private bool hasSummonedInCurrentMotion = false;
        private bool pendingSummon = false;

        private bool IsItemInRightHand { get { return isHeldInMainHand == VRPlayer.isRightHandMainWeaponHand; } }

        private SteamVR_LaserPointer ItemHandPointer
        {
            get { return IsItemInRightHand ? VRPlayer.rightPointer : VRPlayer.leftPointer; }
        }

        // The trigger of the hand actually holding the item.
        private SteamVR_Action_Boolean ItemHandTriggerAction
        {
            get { return IsItemInRightHand ? SteamVR_Actions.valheim_Use : SteamVR_Actions.valheim_UseLeft; }
        }

        // The hand raised to summon is the one not holding the item.
        private bool IsGestureHandRight { get { return !IsItemInRightHand; } }
        private Hand GestureHand { get { return IsGestureHandRight ? VRPlayer.rightHand : VRPlayer.leftHand; } }
        private SteamVR_Input_Sources GestureHandInputSource
        {
            get { return IsGestureHandRight ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand; }
        }
        private SteamVR_Action_Boolean GestureHandTriggerAction
        {
            get { return IsGestureHandRight ? SteamVR_Actions.valheim_Use : SteamVR_Actions.valheim_UseLeft; }
        }

        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void FixedUpdate()
        {
            var inputSource = GestureHandInputSource;
            if (GestureHandTriggerAction.GetState(inputSource))
            {
                if (hasSummonedInCurrentMotion)
                {
                    return;
                }

                float handHeight = GestureHand.transform.position.y;
                if (handHeight < currentMaxHandHeight)
                {
                    // Pause summoning unless the hand is moving upward.
                    return;
                }
                currentMaxHandHeight = handHeight;

                var physicsEstimator =
                    IsGestureHandRight ?
                    VRPlayer.rightHandPhysicsEstimator :
                    VRPlayer.leftHandPhysicsEstimator;
                if (physicsEstimator.GetVelocity().y > MIN_SUMMONING_HAND_SPEED)
                {
                    summonTimer += Time.fixedDeltaTime;
                    GestureHand.hapticAction.Execute(0, 0.1f, 50, 0.3f, inputSource);
                }

                if (summonTimer > SUMMON_TIME)
                {
                    hasSummonedInCurrentMotion = true;
                    pendingSummon = true;
                }
            }
            else
            {
                pendingSummon = hasSummonedInCurrentMotion = false;
                summonTimer = 0;
                currentMaxHandHeight = float.NegativeInfinity;
            }
        }

        // Returns whether the summoner should attack, consuming the pending summon if one caused it.
        // Only the call site that actually initiates the attack may call this, since a second caller
        // would swallow the summon before the attack is triggered.
        public bool ConsumeAttemptingAttack()
        {
            // Pressing the trigger of the hand holding the summoner attacks directly, without a gesture.
            if (ItemHandTriggerAction.state)
            {
                return true;
            }

            if (!pendingSummon)
            {
                return false;
            }
            pendingSummon = false;
            return true;
        }

        public Vector3 AimDir
        {
            get { return IsGestureHandRight ? VRPlayer.rightHandBone.up : VRPlayer.leftHandBone.up; }
        }

        public Vector3 GetProjectileSpawnPoint(Attack attack)
        {
            return MagicStaffUtils.GetProjectileSpawnPoint(attack, AimDir, ItemHandPointer);
        }
    }
}
