using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.Extras;

namespace ValheimVRMod.Scripts
{
    // Manages the dead raiser, an off-hand item summoned either by pressing the trigger of the hand
    // holding it or by raising the opposite (main weapon) hand. It is deliberately not a magic staff
    // manager: unlike staves it is not a main hand weapon and it has no secondary attack.
    public class DeadRaiserManager : MonoBehaviour
    {
        public static readonly HashSet<string> ITEM_NAMES = new HashSet<string>(new string[] { "$item_staffskeleton" });

        private const float MIN_SUMMONING_HAND_SPEED = 0.25f;
        private const float SUMMON_TIME = 1;

        public static DeadRaiserManager instance;

        private float currentMaxHandHeight = Mathf.NegativeInfinity;
        private float summonTimer = 0;
        private bool hasSummonedInCurrentMotion = false;
        private bool pendingSummon = false;

        // The dead raiser is held in the off hand since it is not a main hand weapon.
        private static SteamVR_LaserPointer ItemHandPointer
        {
            get { return VRPlayer.isRightHandMainWeaponHand ? VRPlayer.leftPointer : VRPlayer.rightPointer; }
        }

        // The trigger of the hand actually holding the dead raiser.
        private static SteamVR_Action_Boolean ItemHandTriggerAction
        {
            get { return VRPlayer.isRightHandMainWeaponHand ? SteamVR_Actions.valheim_UseLeft : SteamVR_Actions.valheim_Use; }
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
            var inputSource = VRPlayer.mainWeaponHandInputSource;
            if (SteamVR_Actions.valheim_Use.GetState(inputSource))
            {
                if (hasSummonedInCurrentMotion)
                {
                    return;
                }

                float handHeight = VRPlayer.mainWeaponHand.transform.position.y;
                if (handHeight < currentMaxHandHeight)
                {
                    // Pause summoning unless the hand is moving upward.
                    return;
                }
                currentMaxHandHeight = handHeight;

                var physicsEstimator =
                    VRPlayer.isRightHandMainWeaponHand ?
                    VRPlayer.rightHandPhysicsEstimator :
                    VRPlayer.leftHandPhysicsEstimator;
                if (physicsEstimator.GetVelocity().y > MIN_SUMMONING_HAND_SPEED)
                {
                    summonTimer += Time.fixedDeltaTime;
                    VRPlayer.mainWeaponHand.hapticAction.Execute(0, 0.1f, 50, 0.3f, inputSource);
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

        // Returns whether the dead raiser should attack, consuming the pending summon if one caused it.
        // Only the call site that actually initiates the attack may call this, since a second caller
        // would swallow the summon before the attack is triggered.
        public bool ConsumeAttemptingAttack()
        {
            // Pressing the trigger of the hand holding the dead raiser attacks directly, without a gesture.
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
            get { return VRPlayer.isRightHandMainWeaponHand ? VRPlayer.rightHandBone.up : VRPlayer.leftHandBone.up; }
        }

        public Vector3 GetProjectileSpawnPoint(Attack attack)
        {
            return MagicStaffUtils.GetProjectileSpawnPoint(attack, AimDir, ItemHandPointer);
        }
    }
}
