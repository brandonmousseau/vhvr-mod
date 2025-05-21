using System.Linq;
using RootMotion.FinalIK;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Scripts {
    public class VRPlayerSync : MonoBehaviour, WeaponWieldSync.TwoHandedStateProvider {

        private VRIK vrikSync;
        
        const float MIN_CHANGE = 0.001f;
        
        public GameObject camera = null;
        public GameObject rightHand = null;
        public GameObject leftHand = null;
        public GameObject pelvis = null;

        private WeaponWield.TwoHandedState twoHandedState = WeaponWield.TwoHandedState.SingleHanded;
        private bool isLeftHanded = false;
        private bool inverseHold = false;

        private Player player;
        private Vector3 ownerLastPositionCamera = Vector3.zero;
        private Vector3 ownerVelocityCamera = Vector3.zero;
        private Vector3 ownerLastPositionLeft = Vector3.zero;
        private Vector3 ownerVelocityLeft = Vector3.zero;
        private Vector3 ownerLastPositionRight = Vector3.zero;
        private Vector3 ownerVelocityRight = Vector3.zero;

        private bool hasTempRelPos = false;
        private Vector3 clientTempRelPosCamera = Vector3.zero;
        private Vector3 clientTempRelPosLeft = Vector3.zero;
        private Vector3 clientTempRelPosRight = Vector3.zero;
        private Vector3 clientTempRelPosPelvis = Vector3.zero;

        private uint lastDataRevision = 0;
        private float deltaTimeCounter = 0f;

        private static readonly string[] FINGERS = { 
            "LeftHandThumb1","LeftHandIndex1","LeftHandMiddle1","LeftHandRing1","LeftHandPinky1",
            "RightHandThumb1","RightHandIndex1","RightHandMiddle1","RightHandRing1","RightHandPinky1"
        };

        private Quaternion[] leftFingerRotations = new Quaternion[20];
        private Quaternion[] rightFingerRotations = new Quaternion[20];

        private bool fingersUpdated;

        public BowManager bowManager;
        public GameObject currentLeftWeapon;
        public GameObject currentRightWeapon;
        public GameObject currentDualWieldWeapon;

        public int remotePlayerNonDominantHandItemHash;
        public int remotePlayerDominantHandItemHash;
        public bool hasReceivedData { get; private set; }

        private void Awake() {
            camera = new GameObject();
            rightHand = new GameObject();
            leftHand = new GameObject();
            pelvis = new GameObject();
            player = GetComponent<Player>();
        }

        void Start()
        {
            if (isOwner())
            {
                updateOwnerLastPositions();
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (isOwner())
            {
                calculateOwnerVelocities(dt);
            }
            if (isValid() && !isOwner()) {
                clientSync(dt);
            }
        }

        public void DestroyVrik()
        {
            if (vrikSync == null)
            {
                return;
            }

            Destroy(vrikSync);
            vrikSync = null;
        }

        public WeaponWield.TwoHandedState GetTwoHandedState()
        {
            return twoHandedState;
        }

        public bool IsLeftHanded()
        {
            return isLeftHanded;
        }

        public bool InverseHold()
        {
            if (isOwner())
            {
                if (EquipScript.getRight() == EquipType.Knife)
                {
                    inverseHold = LocalWeaponWield.IsDominantHandHoldInversed;
                }
                else if (EquipScript.isSpearEquipped())
                {
                    inverseHold = LocalWeaponWield.IsDominantHandHoldInversed || LocalWeaponWield.isCurrentlyTwoHanded();
                }
                else
                {
                    inverseHold = LocalWeaponWield.IsDominantHandHoldInversed && !LocalWeaponWield.isCurrentlyTwoHanded();
                }
            }
            return inverseHold;
        }
        
        public bool IsVrEnabled()
        {
            return vrikSync != null;
        }

        private void calculateOwnerVelocities(float dt)
        {
            ownerVelocityCamera = (camera.transform.position - player.transform.position - ownerLastPositionCamera) / dt;
            ownerVelocityLeft = (leftHand.transform.position - player.transform.position - ownerLastPositionLeft) / dt;
            ownerVelocityRight = (rightHand.transform.position - player.transform.position - ownerLastPositionRight) / dt;
            // Update "last" position for next cycle velocity calculation
            updateOwnerLastPositions();
        }

        private void updateOwnerLastPositions()
        {
            ownerLastPositionCamera = camera.transform.position - player.transform.position;
            ownerLastPositionLeft = leftHand.transform.position - player.transform.position;
            ownerLastPositionRight = rightHand.transform.position - player.transform.position;
        }

        private void LateUpdate()
        {
            if (isOwner())
            {
                ownerSync();
                return;
            }

            if (vrikSync == null)
            {
                return;
            }

            if (!fingersUpdated) {
                return;
            }
            
            applyFingers(vrikSync.references.leftHand, leftFingerRotations);
            applyFingers(vrikSync.references.rightHand, rightFingerRotations);
            fingersUpdated = false;
        }

        // Transmit position, rotation, and velocity information to server
        private void ownerSync()
        {
            if (!VHVRConfig.UseVrControls() || VRPlayer.ShouldPauseMovement || VRPlayer.vrikRef == null) {
                return;
            }

            ZPackage pkg = new ZPackage();
            writeData(pkg, camera, ownerVelocityCamera);
            writeData(pkg, leftHand, ownerVelocityLeft);
            writeData(pkg, rightHand, ownerVelocityRight);
            writeData(pkg, pelvis, ownerVelocityCamera);
            writeFingers(pkg, VRPlayer.vrikRef.references.leftHand);
            writeFingers(pkg, VRPlayer.vrikRef.references.rightHand);
            pkg.Write(BowLocalManager.instance != null && BowLocalManager.instance.pulling);
            pkg.Write(isLeftHanded = VHVRConfig.LeftHanded());
            pkg.Write((byte) (twoHandedState = LocalWeaponWield.LocalPlayerTwoHandedState));
            pkg.Write(InverseHold());

            GetComponent<ZNetView>().GetZDO().Set("vr_data", pkg.GetArray());
        }

        private static readonly Quaternion DUNDR_SINGLE_HAND_ADDITIONAL_ROTATION = Quaternion.Euler(55, 0, 0);
        private void writeData(ZPackage pkg, GameObject obj, Vector3 ownerVelocity) 
        {
            var rotation = obj.transform.rotation;
            if (obj == rightHand ^ isLeftHanded)
            {
                if (!LocalWeaponWield.isCurrentlyTwoHanded() && EquipScript.isDundrEquipped())
                {
                    rotation *= DUNDR_SINGLE_HAND_ADDITIONAL_ROTATION;
                }
            }
            pkg.Write(obj.transform.position - player.transform.position);
            pkg.Write(rotation);
            pkg.Write(ownerVelocity);
        }

        private void clientSync(float dt) {
            syncPositionAndRotation(GetComponent<ZNetView>().GetZDO(), dt);
        }

        private void syncPositionAndRotation(ZDO zdo, float dt)
        {
            if (zdo == null)
            {
                return;
            }
            var vr_data = zdo.GetByteArray("vr_data");
            if (vr_data == null)
            {
                return;
            }
            hasReceivedData = true;
            ZPackage pkg = new ZPackage(vr_data);
            var currentDataRevision = zdo.DataRevision;
            if (currentDataRevision != lastDataRevision)
            {
                // New data revision since last sync so we reset our deltaT counter
                deltaTimeCounter = 0f;
                // Save the current data revision so we can detect the next new data package
                lastDataRevision = currentDataRevision;
            }
            deltaTimeCounter += dt;
            deltaTimeCounter = Mathf.Min(deltaTimeCounter, 2f);

            extractAndUpdate(pkg, ref camera, ref clientTempRelPosCamera, hasTempRelPos);
            extractAndUpdate(pkg, ref leftHand, ref clientTempRelPosLeft, hasTempRelPos);
            extractAndUpdate(pkg, ref rightHand, ref clientTempRelPosRight, hasTempRelPos);
            extractAndUpdate(pkg, ref pelvis, ref clientTempRelPosPelvis, hasTempRelPos);

            maybeAddVrik();
            if (vrikSync != null)
            {
                // TODO: Consider creating a method that does this check and can be used both here
                // and in VRPlayer.
                vrikSync.enabled = !player.InDodge() && !player.IsStaggering() && !player.IsSleeping();
            }
            hasTempRelPos = true;
            readFingers(pkg);
            maybePullBow(pkg.ReadBool());
            isLeftHanded = pkg.ReadBool();
            twoHandedState = (WeaponWield.TwoHandedState) pkg.ReadByte();
            inverseHold = pkg.ReadBool();
        }

        private void maybePullBow(bool pulling) {
            if (bowManager == null) {
                if (!pulling || currentLeftWeapon == null) {
                    return;
                }                
                bowManager = currentLeftWeapon.AddComponent<BowManager>();
                bowManager.mainHand = isLeftHanded ? leftHand.transform : rightHand.transform;
            }
            bowManager.pulling = pulling;
        }

        private void extractAndUpdate(ZPackage pkg, ref GameObject obj, ref Vector3 tempRelPos, bool hasTempRelPos)
        {
            // Extract package data
            var position = pkg.ReadVector3();
            var rotation = pkg.ReadQuaternion();
            var velocity = pkg.ReadVector3();

            if (!hasTempRelPos)
            {
                tempRelPos = position;
            }

            if (Vector3.Distance(tempRelPos, position) > MIN_CHANGE)
            {
                tempRelPos = Vector3.Lerp(tempRelPos, position, 0.2f);
                position = tempRelPos;
            }

            // Update the object position with new calculated position
            updatePosition(obj, position + player.transform.position);
            
            // Update the rotation
            updateRotation(obj, rotation);
        }

        private static void updatePosition(GameObject obj, Vector3 position)
        {
            if (Vector3.Distance(obj.transform.position, position) > MIN_CHANGE)
            {
                obj.transform.position = position;
            }
        }

        private static void updateRotation(GameObject obj, Quaternion rotation)
        {
            if (Quaternion.Angle(obj.transform.rotation, rotation) > MIN_CHANGE)
            {
                obj.transform.rotation = Quaternion.Slerp(obj.transform.rotation, rotation, 0.2f);
            }
        }

        private void maybeAddVrik() {
            if (vrikSync != null)
            {
                return;
            }
            vrikSync =
                VrikCreator.initialize(
                    gameObject, leftHand.transform, rightHand.transform, camera.transform, pelvis.transform);
            VrikCreator.resetVrikHandTransform(player);
        }

        private bool isOwner()
        {
            if (!isValid())
            {
                return false;
            }
            var zdo = GetComponent<ZNetView>().GetZDO();
            if (zdo == null)
            {
                LogError("Null ZDO during isOwner check.");
                return false;
            }
            return zdo.IsOwner();
        }

        private bool isValid()
        {
            var netview = GetComponent<ZNetView>();
            return netview != null && netview.IsValid();
        }
        
        private void writeFingers(ZPackage pkg, Transform hand) {

            for (int i = 0; i < hand.childCount; i++) {

                var child = hand.GetChild(i);

                if (FINGERS.Contains(child.name)) {
                    writeFinger(pkg, child);
                }
            }
        }

        private void writeFinger(ZPackage pkg, Transform finger) {
            pkg.Write(finger.localRotation);
            if (finger.childCount > 0) {
                writeFinger(pkg, finger.GetChild(0));
            } 
        }
        
        private void readFingers(ZPackage pkg) {

            for (int i = 0; i < 20; i++) {
                leftFingerRotations[i] = pkg.ReadQuaternion();
            }
            for (int i = 0; i < 20; i++) {
                rightFingerRotations[i] = pkg.ReadQuaternion();
            }

            fingersUpdated = true;
        }

        private void applyFingers(Transform hand, Quaternion[] fingerRotations) {

            int fingerCounter = 0;
            
            for (int i = 0; i < hand.childCount; i++) {

                var child = hand.GetChild(i);

                if (FINGERS.Contains(child.name)) {
                    applyFinger(child, fingerRotations, ref fingerCounter);
                }
            }
        }

        private void applyFinger(Transform finger, Quaternion[] fingerRotations, ref int fingerCounter) {
            
            finger.localRotation = fingerRotations[fingerCounter];
            fingerCounter++;
            if (finger.childCount > 0) {
                applyFinger(finger.GetChild(0), fingerRotations, ref fingerCounter);
            }
        }
    }
}
