using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    // Manages Staff of the Dead, which is summoned by raising the hand opposite the one holding the staff.
    public class DeadRaiserManager : MonoBehaviour, IMagicStaffManager
    {
        public static readonly HashSet<string> STAFF_NAMES = new HashSet<string>(new string[] { "$item_staffskeleton" });

        private const float MIN_SUMMONING_HAND_SPEED = 0.25f;
        private const float SUMMON_TIME = 1;

        public static DeadRaiserManager instance;

        private float currentMaxHandHeight = Mathf.NegativeInfinity;
        private float summonTimer = 0;
        private bool hasSummonedInCurrentMotion = false;
        private bool pendingSummon = false;

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

        public bool AttemptingAttack
        {
            get
            {
                if (!pendingSummon)
                {
                    return false;
                }
                pendingSummon = false;
                return true;
            }
        }

        public bool IsSecondaryAttack
        {
            get { return MagicStaffUtils.IsSecondaryAttack(isDominantHandWeapon: false); }
        }

        public bool TrySecondaryAttack
        {
            get { return MagicStaffUtils.TrySecondaryAttack(isDominantHandWeapon: false); }
        }

        public Vector3 AimDir
        {
            get { return VRPlayer.isRightHandMainWeaponHand ? VRPlayer.rightHandBone.up : VRPlayer.leftHandBone.up; }
        }

        public Vector3 GetProjectileSpawnPoint(Attack attack)
        {
            return MagicStaffUtils.GetProjectileSpawnPoint(attack, AimDir, MagicStaffUtils.WeaponHandPointer(isDominantHandWeapon: false));
        }
    }
}
