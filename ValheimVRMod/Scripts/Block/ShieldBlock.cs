using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts.Block {
    public class ShieldBlock : Block {

        public string itemName;
        private const float maxParryAngle = 45f;

        private float scaling = 1f;
        private Vector3 posRef;
        private Vector3 scaleRef;

        public static ShieldBlock instance;

        private void OnDisable() {
            instance = null;
        }
        
        protected override void Awake() {
            base.Awake();
            _meshCooldown = gameObject.AddComponent<MeshCooldown>();
            instance = this;
            InitShield();
        }
        
        private void InitShield()
        {
            posRef = _meshCooldown.transform.localPosition;
            scaleRef = _meshCooldown.transform.localScale;
            hand = VHVRConfig.LeftHanded() ? VRPlayer.rightHand.transform : VRPlayer.leftHand.transform;
            offhand = VHVRConfig.LeftHanded() ? VRPlayer.leftHand.transform : VRPlayer.rightHand.transform;
        }

        public override void setBlocking(HitData hitData) {
            if (VHVRConfig.BlockingType() == "GrabButton")
            {
                _blocking = SteamVR_Actions.valheim_Grab.GetState(VRPlayer.nonDominantHandInputSource);
            }
            else if (VHVRConfig.BlockingType() == "Realistic")
            {
                _blocking = Vector3.Dot(hitData.m_dir, getForward()) > 0.3f && hitIntersectsBlockBox(hitData);
            }
            else {
                _blocking = Vector3.Dot(hitData.m_dir, getForward()) > 0.5;
            }
        }

        private Vector3 getForward() {
            switch (itemName)
            {
                case "ShieldWood":
                case "ShieldBanded":
                    return StaticObjects.shieldObj().transform.forward;
                case "ShieldKnight":
                    return -StaticObjects.shieldObj().transform.right;
                case "ShieldBronzeBuckler":
                case "ShieldIronBuckler":
                    return VHVRConfig.LeftHanded() ? StaticObjects.shieldObj().transform.up : -StaticObjects.shieldObj().transform.up;
            }
            return -StaticObjects.shieldObj().transform.forward;
        }

        protected override void ParryCheck(Vector3 posStart, Vector3 posEnd, Vector3 posStart2, Vector3 posEnd2) {
            var shieldSnapshot = VHVRConfig.LeftHanded() ? snapshotsLeft : snapshots;
            if (Vector3.Distance(posEnd, posStart) > minDist) {

                Vector3 shieldPos = shieldSnapshot[shieldSnapshot.Count - 1] + Player.m_localPlayer.transform.InverseTransformDirection(-hand.right) / 2;
                if (Vector3.Angle(shieldPos - shieldSnapshot[0] , shieldSnapshot[shieldSnapshot.Count - 1] - shieldSnapshot[0]) < maxParryAngle) {
                    blockTimer = blockTimerParry;
                }
            } else {
                blockTimer = blockTimerNonParry;
            }
        }

        protected override void OnRenderObject() {
            base.OnRenderObject();
            if (scaling != 1f)
            {
                transform.localScale = scaleRef * scaling;
                transform.localPosition = CalculatePos();
            }
            else if (transform.localPosition != posRef || transform.localScale != scaleRef)
            {
                transform.localScale = scaleRef;
                transform.localPosition = posRef;
            }
            StaticObjects.shieldObj().transform.position = transform.position;
            StaticObjects.shieldObj().transform.rotation = transform.rotation;
        }

        public void ScaleShieldSize(float scale)
        {
            scaling = scale;
        }
        private Vector3 CalculatePos()
        {
            return VRPlayer.leftHand.transform.InverseTransformDirection(hand.TransformDirection(posRef) *(scaleRef * scaling).x);
        }
    }
}
