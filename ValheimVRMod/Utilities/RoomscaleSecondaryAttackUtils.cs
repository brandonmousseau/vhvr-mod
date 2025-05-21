using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Utilities
{
    public static class RoomscaleSecondaryAttackUtils
    {
        public static bool IsSecondaryAttack(PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator handPhysicsEstimator)
        {
            var rightEquipType = EquipScript.getRight();
            switch (rightEquipType)
            {
                case EquipType.Axe:
                case EquipType.Club:
                    return !IsStab(handPhysicsEstimator) && IsStrongSwing(collisionPhysicsEstimator, handPhysicsEstimator);
                case EquipType.BattleAxe:
                case EquipType.Polearms:
                    if (!LocalWeaponWield.isCurrentlyTwoHanded() || LocalWeaponWield.IsDominantHandBehind || VRPlayer.vrCam == null)
                    {
                        return false;
                    }

                    var velocity = VHVRConfig.LeftHanded() ? VRPlayer.leftHandPhysicsEstimator.GetVelocity() : VRPlayer.rightHandPhysicsEstimator.GetVelocity();
                    Vector3 swipeAxis = Vector3.Cross(LocalWeaponWield.weaponForward, VRPlayer.vrCam.transform.parent.up);
                    float angle = Vector3.Angle(velocity, swipeAxis);

                    return LocalWeaponWield.CurrentTwoHandedWieldStartedWithLongGrip ?
                        (angle < 45 || angle > 135) :
                        Mathf.Abs(Vector3.Dot(velocity, swipeAxis)) > GetMinSecondarySwingSpeed();
                case EquipType.Claws:
                case EquipType.None:
                    return IsHook(handPhysicsEstimator);
                case EquipType.DualAxes:
                    return IsCleaving();
                case EquipType.DualKnives:
                    return IsCleaving() || IsHook(handPhysicsEstimator);
                case EquipType.Knife:
                    if (LocalWeaponWield.isCurrentlyTwoHanded())
                    {
                        return IsStrongStab(handPhysicsEstimator) || IsStrongSwing(collisionPhysicsEstimator, handPhysicsEstimator);
                    }
                    if (LocalWeaponWield.IsDominantHandHoldInversed)
                    {
                        return IsStrongStab(handPhysicsEstimator) || IsHook(handPhysicsEstimator);
                    }
                    return false;
                case EquipType.Sledge:
                    return false;
                case EquipType.Sword:
                    return IsStrongStab(handPhysicsEstimator);
                default:
                    return false;
            }
        }

        private static Vector3 GetHandVelocitySum()
        {
            return VRPlayer.leftHandPhysicsEstimator.GetVelocity() + VRPlayer.rightHandPhysicsEstimator.GetVelocity();
        }

        private static bool IsStab(PhysicsEstimator physicsEstimator)
        {
            return WeaponUtils.IsStab(
                physicsEstimator.GetAverageVelocityInSnapshots(),
                LocalWeaponWield.weaponForward,
                LocalWeaponWield.isCurrentlyTwoHanded());
        }

        private static bool IsStrongStab(PhysicsEstimator mainHandPhysicsEstimator)
        {
            if (!IsStab(mainHandPhysicsEstimator))
            {
                return false;
            }

            var minStabSpeed = Mathf.Max(VHVRConfig.SwingSpeedRequirement(), 3);
            if (LocalWeaponWield.isCurrentlyTwoHanded() && Vector3.Dot(GetHandVelocitySum(), LocalWeaponWield.weaponForward) > minStabSpeed)
            {
                return true;
            }

            const float MIN_THRUST_DISTANCE = 1f;
            return Vector3.Dot(mainHandPhysicsEstimator.GetVelocity(), LocalWeaponWield.weaponForward) > minStabSpeed &&
                Vector3.Dot(mainHandPhysicsEstimator.GetLongestLocomotion(1f), LocalWeaponWield.weaponForward) >= MIN_THRUST_DISTANCE;
        }

        private static bool IsStrongSwing(PhysicsEstimator collisionPhysicsEstimator, PhysicsEstimator mainHandPhysicsEstimator)
        {
            // When wielding with both hands, use the sum of both hands' velocities to make secondary attack easier to trigger.
            Vector3 handVelocity =
                LocalWeaponWield.isCurrentlyTwoHanded() ? GetHandVelocitySum() : mainHandPhysicsEstimator.GetVelocity();

            var weaponOffsetFromPlayer = mainHandPhysicsEstimator.transform.position - Player.m_localPlayer.transform.position;
            var saggitalSpeed = GetSagittalComponent(handVelocity, weaponOffsetFromPlayer).magnitude;
            var minSpeed = GetMinSecondarySwingSpeed();
            if (LocalWeaponWield.isCurrentlyTwoHanded() &&
                GetSagittalComponent(GetHandVelocitySum(), weaponOffsetFromPlayer).magnitude > minSpeed)
            {
                return true;
            }

            if (GetSagittalComponent(mainHandPhysicsEstimator.GetVelocity(), weaponOffsetFromPlayer).magnitude < minSpeed)
            {
                return false;
            }

            const float MIN_THRUST_DISTANCE = 1.5f;
            Vector3 thrust = collisionPhysicsEstimator.GetLongestLocomotion(0.5f);
            return GetSagittalComponent(thrust, weaponOffsetFromPlayer).magnitude >= MIN_THRUST_DISTANCE;
        }

        public static bool IsHook(PhysicsEstimator physicsEstimator)
        {
            const float MIN_HOOK_DISTANCE = 1f;
            const float MAX_HOOK_ALIGNMENT_ANGLE = 30f;
            var minHookSpeed = Mathf.Clamp(VHVRConfig.SwingSpeedRequirement() * 1.25f, 3, 9);
            var velocity = physicsEstimator.GetAverageVelocityInSnapshots();
            var distance = physicsEstimator.GetLongestLocomotion(1f);
            if (velocity.magnitude < minHookSpeed)
            {
                return false;
            }
            return Vector3.Dot(distance, velocity.normalized) >= MIN_HOOK_DISTANCE && Vector3.Angle(distance, velocity) <= MAX_HOOK_ALIGNMENT_ANGLE;
        }

        private static bool IsCleaving()
        {
            const float MIN_HEW_SPEED = 1f;
            const float MIN_TOTAL_HEW_SPEED = 4f;
            const float MAX_BLADE_DISTANCE = 0.75f;

            var leftHandHewSpeed =
                Vector3.Dot(VRPlayer.leftHandPhysicsEstimator.GetAverageVelocityInSnapshots(), VRPlayer.leftHand.transform.forward);
            if (leftHandHewSpeed < MIN_HEW_SPEED)
            {
                return false;
            }

            var rightHandHewSpeed =
                Vector3.Dot(VRPlayer.rightHandPhysicsEstimator.GetAverageVelocityInSnapshots(), VRPlayer.rightHand.transform.forward);
            if (rightHandHewSpeed < MIN_HEW_SPEED)
            {
                return false;
            }

            if (leftHandHewSpeed + rightHandHewSpeed < MIN_TOTAL_HEW_SPEED)
            {
                return false;
            }

            return Vector3.Distance(StaticObjects.leftFist().transform.position, StaticObjects.rightFist().transform.position) < MAX_BLADE_DISTANCE;
        }

        private static Vector3 GetSagittalComponent(Vector3 v, Vector3 weaponOffsetFromPlayer)
        {
            var lateral = Vector3.Cross(VRPlayer.instance.transform.up, weaponOffsetFromPlayer);
            return Vector3.ProjectOnPlane(v, lateral);
        }

        private static float GetMinSecondarySwingSpeed()
        {
            return Mathf.Clamp(VHVRConfig.SwingSpeedRequirement() * 1.5f, 3, 9);
        }
    }
}
