using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts {
    public class ShieldManager : MonoBehaviour {
        
        public string _rightItemName;
        public static bool rightIsWeapon;
        private static WeaponWield weaponWieldCheck;
        private static MeshCooldown _rightMeshCooldown;

        public string _leftItemName;
        public static bool leftIsShield;
        private static MeshCooldown _leftMeshCooldown;

        private const float cooldown = 1;
        private const float blockTimerParry = 0.1f;
        public const float blockTimerTolerance = blockTimerParry + 0.2f;
        private const float blockTimerNonParry = 9999f;
        private const float minDist = 0.4f;
        private const float maxParryAngle = 45f;
        private static bool _blocking;
        public static float blockTimer;
        private static ShieldManager instance;
        private static Player player;
        

        private const int MAX_SNAPSHOTS = 7;
        private int tickCounter;
        private List<Vector3> snapshots = new List<Vector3>();
        private static float scaling = 1f;
        private static Vector3 posRef;
        private static Vector3 scaleRef;

        private Transform shieldHand;

        private void Awake() {
            instance = this;
            player = this.GetComponent<Player>();
        }

        public void SetRight(string name,GameObject mesh, WeaponWield wield)
        {
            _rightItemName = name;
            rightIsWeapon = true;
            weaponWieldCheck = wield;
            _rightMeshCooldown = mesh.AddComponent<MeshCooldown>();
            InitShield();
        }
        public void SetLeft(string name, GameObject mesh)
        {
            _leftItemName = name;
            leftIsShield = true;
            _leftMeshCooldown = mesh.AddComponent<MeshCooldown>();
            InitShield();
        }

        public void ClearRight()
        {
            _rightItemName = null;
            rightIsWeapon = false;
            _rightMeshCooldown = null;
            InitShield();
        }
        public void ClearLeft()
        {
            _leftItemName = null;
            leftIsShield = false;
            _leftMeshCooldown = null;
            InitShield();
        }

        private void InitShield()
        {
            if (leftIsShield)
            {
                posRef = _leftMeshCooldown.transform.localPosition;
                scaleRef = _leftMeshCooldown.transform.localScale;
                shieldHand = VHVRConfig.LeftHanded() ? VRPlayer.rightHand.transform : VRPlayer.leftHand.transform;
                return;
            }
            shieldHand = VHVRConfig.LeftHanded() ? VRPlayer.leftHand.transform : VRPlayer.rightHand.transform;
        }

        public static void setBlocking(Vector3 hitDir) {
            var angle = Vector3.Dot(hitDir, instance.getForward());
            LogUtils.LogDebug("Block Angle : " + angle);
            if (leftIsShield&&_leftMeshCooldown)
            {
                LogUtils.LogDebug("Currently Left");
                _blocking = angle > 0.5f;
            }
            else if (rightIsWeapon&&_rightMeshCooldown)
            {
                LogUtils.LogDebug("Currently Right");
                if (weaponWieldCheck.isCurrentlyTwoHanded())
                _blocking = true;
            }
            else
            {
                LogUtils.LogDebug("Currently None");
                _blocking = false;
            }
        }

        public static void resetBlocking() {
            _blocking = false;
            blockTimer = blockTimerNonParry;
        }

        public static bool isBlocking() {
            if (leftIsShield && _leftMeshCooldown)
                return _blocking && !_leftMeshCooldown.inCoolDown();
            else if (rightIsWeapon && _rightMeshCooldown)
                return _blocking && !_rightMeshCooldown.inCoolDown();
            else
                return false;
        }
        
        public static void block() {
            if (_leftMeshCooldown)
                _leftMeshCooldown.tryTrigger(cooldown);
            else if (_rightMeshCooldown)
                _rightMeshCooldown.tryTrigger(cooldown);
        }
        
        private Vector3 getForward() {

            if (leftIsShield && _leftMeshCooldown)
            {
                switch (_rightItemName)
                {
                    case "ShieldWood":
                    case "ShieldBanded":
                        return StaticObjects.shieldObj().transform.forward;
                    case "ShieldKnight":
                        return -StaticObjects.shieldObj().transform.right;
                    case "ShieldBronzeBuckler":
                        return -StaticObjects.shieldObj().transform.up;
                }
            }else if (rightIsWeapon && _rightMeshCooldown)
            {
                return _rightMeshCooldown.transform.forward;
            }

            return -StaticObjects.shieldObj().transform.forward;
        }
        private void ParryCheck() {
            var dist = 0.0f;
            Vector3 posEnd = Player.m_localPlayer.transform.InverseTransformPoint(shieldHand.position);
            Vector3 posStart = Player.m_localPlayer.transform.InverseTransformPoint(shieldHand.position);

            foreach (Vector3 snapshot in snapshots) {
                var curDist = Vector3.Distance(snapshot, posEnd);
                if (curDist > dist) {
                    dist = curDist;
                    posStart = snapshot;
                }
            }

            Vector3 shieldPos = (snapshots[snapshots.Count - 1] + (Player.m_localPlayer.transform.InverseTransformDirection(-shieldHand.right) / 2) );

            if (Vector3.Distance(posEnd, posStart) > minDist) {
                if (rightIsWeapon)
                {
                    blockTimer = blockTimerParry;
                }
                else
                {
                    if (Vector3.Angle(shieldPos - snapshots[0], snapshots[snapshots.Count - 1] - snapshots[0]) < maxParryAngle)
                    {
                        blockTimer = blockTimerParry;
                    }
                }
                
            } else {
                blockTimer = blockTimerNonParry;
            }
        }
        private void FixedUpdate() {
            tickCounter++;
            if (tickCounter < 5) {
                return;
            }
            if (_leftMeshCooldown || _rightMeshCooldown)
                snapshots.Add(Player.m_localPlayer.transform.InverseTransformPoint(shieldHand.position));

            if (snapshots.Count > MAX_SNAPSHOTS) {
                snapshots.RemoveAt(0);
            }

            tickCounter = 0;

            if (_leftMeshCooldown || _rightMeshCooldown)
                ParryCheck();
        }

        private void OnRenderObject() {
            if (leftIsShield&&_leftMeshCooldown)
            {
                var shieldTransform = _leftMeshCooldown.transform;
                if (scaling != 1f)
                {
                    shieldTransform.localScale = scaleRef * scaling;
                    shieldTransform.localPosition = CalculatePos();
                }
                else if (shieldTransform.localPosition != posRef || shieldTransform.localScale != scaleRef)
                {
                    shieldTransform.localScale = scaleRef;
                    shieldTransform.localPosition = posRef;
                }
                StaticObjects.shieldObj().transform.rotation = shieldTransform.rotation;
            }
        }
        public static void ScaleShieldSize(float scale)
        {
            scaling = scale;
        }
        private Vector3 CalculatePos()
        {
            return VRPlayer.leftHand.transform.InverseTransformDirection(shieldHand.TransformDirection(posRef) *(scaleRef * scaling).x);
        }
    }
}