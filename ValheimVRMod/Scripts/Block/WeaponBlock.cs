using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts.Block
{
    public class WeaponBlock : Block
    {
        public WeaponWield weaponWield;
        public static WeaponBlock instance;

        private const float MIN_PARRY_SPEED = 2.5f;
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
            var angle = Vector3.Angle(hitData.m_dir, WeaponWield.weaponForward);

            // The weaponWield.transform outside its OnRenderObject() might be invalid, therefore we use weaponWield.physicsEstimator.transform intead.
            Vector3 hitPointAlongWeapon = weaponWield.physicsEstimator.transform.position + Vector3.Project(hitData.m_point - weaponWield.physicsEstimator.transform.position, WeaponWield.weaponForward);

            if (VHVRConfig.BlockingType() == "Realistic")
            {
                Vector3 parryVector = weaponWield.physicsEstimator.GetVelocityOfPoint(hitPointAlongWeapon);
                bool blockWithAngle = 15 < angle && angle < 165;
                bool blockWithSpeed = parryVector.magnitude > MIN_PARRY_SPEED;
                _blocking = (blockWithAngle || blockWithSpeed) && hitIntersectsBlockBox(hitData) && SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource);
            }
            else if (weaponWield.isLeftHandWeapon() && EquipScript.getLeft() != EquipType.Crossbow)
            {
                var leftAngle = Vector3.Dot(hitData.m_dir, offhand.TransformDirection(handUp));
                var rightAngle = Vector3.Dot(hitData.m_dir, hand.TransformDirection(handUp));
                var leftHandBlock = (leftAngle > 60 && leftAngle < 120f);
                var rightHandBlock = (rightAngle > 60 && rightAngle < 120f);
                _blocking = leftHandBlock && rightHandBlock;
            }
            else if (VHVRConfig.BlockingType() == "GrabButton")
            {
                bool isShieldorWeaponBlock = (EquipScript.getLeft() != EquipType.Shield) || (EquipScript.getLeft() == EquipType.Shield && weaponWield.allowBlocking());
                _blocking = isShieldorWeaponBlock && angle > 60 && angle < 120;
            }
            else
            {
                _blocking = weaponWield.allowBlocking() && angle > 60 && angle < 120;
            }

            CheckParryMotion(hitPointAlongWeapon, hitData.m_dir);
        }

        private void CheckParryMotion(Vector3 hitPointAlongWeapon, Vector3 hitDir)
        {
            Vector3 v = weaponWield.physicsEstimator.GetVelocityOfPoint(hitPointAlongWeapon);
            // Only consider the component of the velocity perpendicular to the hit direction as parrying speed.
            float parrySpeed = Vector3.ProjectOnPlane(v, hitDir).magnitude;
            blockTimer = parrySpeed > MIN_PARRY_SPEED ? blockTimerParry : blockTimer = blockTimerNonParry;
        }
    }
}
