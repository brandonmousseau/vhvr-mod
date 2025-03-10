using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts.Block
{
    public class WeaponBlock : Block
    {
        public LocalWeaponWield weaponWield;
        public static WeaponBlock instance;

        private const float MIN_PARRY_SPEED = 1.5f;
        private readonly Vector3 handUp = new Vector3(0, -0.15f, -0.85f);

        private void OnDisable()
        {
            instance = null;
        }

        protected override void Awake()
        {
            base.Awake();
            _meshCooldown = gameObject.AddComponent<MeshCooldown>();
            instance = this;
            hand = VHVRConfig.LeftHanded() ? VRPlayer.leftHand.transform : VRPlayer.rightHand.transform;
            offhand = VHVRConfig.LeftHanded() ? VRPlayer.rightHand.transform : VRPlayer.leftHand.transform;
        }

        public override void setBlocking(HitData hitData)
        {
            var angle = Vector3.Angle(hitData.m_dir, LocalWeaponWield.weaponForward);

            // The weaponWield.transform outside its OnRenderObject() might be invalid, therefore we use weaponWield.physicsEstimator.transform intead.
            Vector3 hitPointAlongWeapon = weaponWield.physicsEstimator.transform.position + Vector3.Project(hitData.m_point - weaponWield.physicsEstimator.transform.position, LocalWeaponWield.weaponForward);
            Vector3 weaponVelocity = weaponWield.physicsEstimator.GetVelocityOfPoint(hitPointAlongWeapon);

            if (VHVRConfig.UseRealisticBlock())
            {
                bool blockWithAngle = 15 < angle && angle < 165;
                bool blockWithSpeed = weaponVelocity.magnitude > MIN_PARRY_SPEED;
                _blocking = (blockWithAngle || blockWithSpeed) && hitIntersectsBlockBox(hitData) && SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource);
            }
            else if (LocalWeaponWield.nonDominantHandHasWeapon() && EquipScript.getLeft() != EquipType.Crossbow)
            {
                var leftAngle = Vector3.Dot(hitData.m_dir, offhand.TransformDirection(handUp));
                var rightAngle = Vector3.Dot(hitData.m_dir, hand.TransformDirection(handUp));
                var leftHandBlock = (leftAngle > 60 && leftAngle < 120f);
                var rightHandBlock = (rightAngle > 60 && rightAngle < 120f);
                _blocking = leftHandBlock && rightHandBlock;
            }
            else if (VHVRConfig.UseGrabButtonBlock())
            {
                bool isShieldorWeaponBlock = (EquipScript.getLeft() != EquipType.Shield) || (EquipScript.getLeft() == EquipType.Shield && weaponWield.allowBlocking());
                _blocking = isShieldorWeaponBlock && angle > 60 && angle < 120;
            }
            else
            {
                _blocking = weaponWield.allowBlocking() && angle > 60 && angle < 120;
            }

            CheckParryMotion(weaponVelocity, hitData.m_dir);
        }

        private void CheckParryMotion(Vector3 weaponVelocity, Vector3 hitDir)
        {
            // Only consider the component of the velocity perpendicular to the hit direction as parrying speed.
            float parrySpeed = Vector3.ProjectOnPlane(weaponVelocity, hitDir).magnitude;
            blockTimer = parrySpeed > MIN_PARRY_SPEED ? blockTimerParry : blockTimer = blockTimerNonParry;
        }
    }
}
