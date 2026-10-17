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
        const float SMOOTHING_PER_FIXED_STEP = 0.2f;

        public GameObject camera = null;
        public GameObject rightHand = null;
        public GameObject leftHand = null;
        public GameObject pelvis = null;
        public GameObject leftFoot = null;
        public GameObject rightFoot = null;
        public Vector3 weaponSyncLocalPosition;
        public Quaternion weaponSyncLocalRotation;

        public bool isLeftHanded { get { return player == Player.m_localPlayer ? !VRPlayer.isRightHandMainWeaponHand : clientIsLeftHanded; } }
        public EquipType mainHandEquipType = EquipType.None;
        public EquipType offHandEquipType = EquipType.None;
        public EquipType leftHandEquipType { get { return isLeftHanded ? mainHandEquipType : offHandEquipType; } }
        public EquipType rightHandEquipType { get { return isLeftHanded ? offHandEquipType : mainHandEquipType; } }
        public bool hasReceivedData { get; private set; }
        public bool receivingFootData { get; private set; } = false;

        private bool hasReceivedWeaponSync = false;
        private bool clientIsLeftHanded = false; 
        private WeaponWield.TwoHandedState twoHandedState = WeaponWield.TwoHandedState.SingleHanded;
        private bool inverseHold = false;

        private Player player;
        private Vector3 ownerLastPositionCamera = Vector3.zero;
        private Vector3 ownerVelocityCamera = Vector3.zero;
        private Vector3 ownerLastPositionLeft = Vector3.zero;
        private Vector3 ownerVelocityLeft = Vector3.zero;
        private Vector3 ownerLastPositionRight = Vector3.zero;
        private Vector3 ownerVelocityRight = Vector3.zero;

        private bool hasTempRelPos = false;
        private RemoteTarget remoteCamera;
        private RemoteTarget remoteLeftHand;
        private RemoteTarget remoteRightHand;
        private RemoteTarget remotePelvis;
        private RemoteTarget remoteLeftFoot;
        private RemoteTarget remoteRightFoot;

        private uint lastDataRevision = 0;
        private float deltaTimeCounter = 0f;

        private static readonly string[] FINGERS = { 
            "LeftHandThumb1","LeftHandIndex1","LeftHandMiddle1","LeftHandRing1","LeftHandPinky1",
            "RightHandThumb1","RightHandIndex1","RightHandMiddle1","RightHandRing1","RightHandPinky1"
        };

        private Quaternion[] leftFingerRotations = new Quaternion[20];
        private Quaternion[] rightFingerRotations = new Quaternion[20];

        private bool fingersUpdated;
        // TODO: remove this once weapon sync is fully supported
        

        private void Awake() {
            camera = new GameObject();
            leftHand = new GameObject();
            rightHand = new GameObject();
            pelvis = new GameObject();
            leftFoot = new GameObject();
            rightFoot = new GameObject();
            player = GetComponent<Player>();
            remoteCamera = new RemoteTarget(camera);
            remoteLeftHand = new RemoteTarget(leftHand);
            remoteRightHand = new RemoteTarget(rightHand);
            remotePelvis = new RemoteTarget(pelvis);
            remoteLeftFoot = new RemoteTarget(leftFoot);
            remoteRightFoot = new RemoteTarget(rightFoot);
        }

        // The IK targets of a remote player are placed here rather than as the data is read in FixedUpdate,
        // against where the player is drawn this frame. The body of the player moves every frame (interpolated
        // between physics steps), so targets placed only on physics steps would lag behind it and then catch up
        // again, shaking the spine to and fro at the rate the steps and frames beat against each other, which
        // the cloth of a cape turns into a violent flapping. VRIK solves in LateUpdate, after this.
        private void Update()
        {
            if (!hasReceivedData || isOwner() || !isValid())
            {
                return;
            }
            // The smoothing that used to be applied once per physics step, as its share of this frame.
            float t = 1 - Mathf.Pow(1 - SMOOTHING_PER_FIXED_STEP, Time.deltaTime / Time.fixedDeltaTime);
            Vector3 playerPosition = player.transform.position;
            remoteCamera.Apply(playerPosition, t);
            remoteLeftHand.Apply(playerPosition, t);
            remoteRightHand.Apply(playerPosition, t);
            remotePelvis.Apply(playerPosition, t);
            if (receivingFootData)
            {
                remoteLeftFoot.Apply(playerPosition, t);
                remoteRightFoot.Apply(playerPosition, t);
            }
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
                return;
            }

            if (!isValid()) {
                return;
            }

            clientSync(dt);
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
                if (EquipScript.CurrentMainHandEquipType() == EquipType.Knife)
                {
                    inverseHold = LocalWeaponWield.IsDominantHandHoldInversed;
                }
                else if (EquipScript.IsSpearEquipped())
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

        public bool MaybeAddClientWeaponSync(GameObject item)
        {
            // Check hasReceivedData instead once weapon sync is fully supported
            if (hasReceivedWeaponSync)
            {
                item.AddComponent<ClientWeaponSync>();
            }
            return hasReceivedWeaponSync;
        }

        public void UpdateWeaponTransform(Vector3 localPosition, Quaternion localRotation)
        {
            weaponSyncLocalPosition = localPosition;
            weaponSyncLocalRotation = localRotation;
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
            if (!VRPlayer.vrikRef.enabled)
            {
                // Stati that should temporarily disable VRIK such as sleeping/staggering/dodging are not sent over network,
                // so we send a vr_data package short of further data to signal remote players that VRIK is temporarily disabled
                GetComponent<ZNetView>().GetZDO().Set("vr_data", pkg.GetArray());
                return;
            }

            writeData(pkg, leftHand, ownerVelocityLeft);
            writeData(pkg, rightHand, ownerVelocityRight);
            pelvis.transform.SetPositionAndRotation(
                VRPlayer.vrikRef.solver.spine.pelvis.solverPosition,
                VRPlayer.vrikRef.solver.spine.pelvis.solverRotation);
            writeData(pkg, pelvis, ownerVelocityCamera);
            writeFingers(pkg, VRPlayer.vrikRef.references.leftHand);
            writeFingers(pkg, VRPlayer.vrikRef.references.rightHand);
            pkg.Write(BowLocalManager.instance != null && BowLocalManager.instance.pulling);
            pkg.Write(isLeftHanded);
            pkg.Write((byte)(twoHandedState = LocalWeaponWield.LocalPlayerTwoHandedState));
            pkg.Write(InverseHold());
            pkg.Write(weaponSyncLocalPosition);
            pkg.Write(weaponSyncLocalRotation);
            if (VRPlayer.vrPlayerInstance != null && VRPlayer.vrPlayerInstance.shouldTrackFeet())
            {
                writeTransformRelativeToPlayer(pkg, VRPlayer.leftFoot);
                writeTransformRelativeToPlayer(pkg, VRPlayer.rightFoot);
            }

            GetComponent<ZNetView>().GetZDO().Set("vr_data", pkg.GetArray());
        }

        private void writeTransformRelativeToPlayer(ZPackage pkg, Transform transform) 
        {
            pkg.Write(transform.position - player.transform.position);
            pkg.Write(transform.rotation);
        }

        private void writeData(ZPackage pkg, GameObject obj, Vector3 ownerVelocity)
        {
            writeTransformRelativeToPlayer(pkg, obj.transform);
            pkg.Write(ownerVelocity);
        }

        private void clientSync(float dt) {
            ZDO zdo = GetComponent<ZNetView>().GetZDO();
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

            extractTarget(pkg, remoteCamera, hasTempRelPos);

            if (!hasMoreData(pkg))
            {
                // If the remote player sends a vr_data package with only camera data but nothing else,
                // they are trying to signal that VRIK should be temporarily disabled (e. g. due to sleeping/staggering/dodging).
                if (vrikSync != null)
                {
                    vrikSync.enabled = false;
                }
                return;
            }

            extractTarget(pkg, remoteLeftHand, hasTempRelPos);
            extractTarget(pkg, remoteRightHand, hasTempRelPos);
            extractTarget(pkg, remotePelvis, hasTempRelPos);

            maybeAddVrik();
            if (vrikSync != null)
            {
                vrikSync.enabled = true;
            }
            readFingers(pkg);
            maybePullBow(pkg.ReadBool());
            clientIsLeftHanded = pkg.ReadBool();
            twoHandedState = (WeaponWield.TwoHandedState) pkg.ReadByte();
            inverseHold = pkg.ReadBool();
            if (hasMoreData(pkg)) // TODO: remove this check once weapon sync is fully supported
            {
                ExtractAndSmoothUpdateVector(pkg, ref weaponSyncLocalPosition, hasTempRelPos);
                weaponSyncLocalRotation = pkg.ReadQuaternion();
                hasReceivedWeaponSync = true;
            }
            if (hasMoreData(pkg))
            {
                remoteLeftFoot.SetTarget(pkg.ReadVector3(), pkg.ReadQuaternion(), receivingFootData);
                remoteRightFoot.SetTarget(pkg.ReadVector3(), pkg.ReadQuaternion(), receivingFootData);
                receivingFootData = true;
                if (vrikSync != null)
                {
                    VrikCreator.EnableFootTracking(vrikSync);
                }
            }
            else
            {
                receivingFootData = false;
                if (vrikSync != null)
                {
                    VrikCreator.DisableFootTracking(vrikSync);
                }
            }
            hasTempRelPos = true;

            correctHandedness();
        }

        private void correctHandedness()
        {
            // Client isLeftHanded sync may happen after equipping.
            // Force re-equip to trigger a patch with the updated isLeftHanded.
            var mainHandItem = player.m_visEquipment.m_rightItemInstance;
            var mainHandItemHash = player.m_visEquipment.m_currentRightItemHash;
            var mainHandItemQuality = player.m_visEquipment.m_currentRightItemQuality;
            if (mainHandItem != null && mainHandItemHash != 0)
            {
                if (isLeftHanded ?
                    mainHandItem.transform.parent == player.m_visEquipment.m_rightHand :
                    mainHandItem.transform.parent == player.m_visEquipment.m_leftHand)
                {
                    LogUtils.LogDebug("Switching main hand item to the other hand");
                    player.m_visEquipment.SetRightHandEquipped(0, 0);
                    player.m_visEquipment.SetRightHandEquipped(mainHandItemHash, mainHandItemQuality);
                }
            }

            var offHandItem = player.m_visEquipment.m_leftItemInstance;
            var offHandItemHash = player.m_visEquipment.m_currentLeftItemHash;
            var offHandItemQuality = player.m_visEquipment.m_currentLeftItemQuality;
            if (offHandItem != null && offHandItemHash != 0)
            {
                if (isLeftHanded ?
                    offHandItem.transform.parent == player.m_visEquipment.m_leftHand :
                    offHandItem.transform.parent == player.m_visEquipment.m_rightHand)
                {
                    LogUtils.LogDebug("Switching secondary hand item to right hand");
                    var variant = player.m_visEquipment.m_currentLeftItemVariant;
                    player.m_visEquipment.SetLeftHandEquipped(0, 0, 0);
                    player.m_visEquipment.SetLeftHandEquipped(offHandItemHash, variant, offHandItemQuality);
                }
            }
        }

        private void maybePullBow(bool pulling) {
            GameObject bow = player.m_visEquipment.m_leftItemInstance;
            if (bow == null)
            {
                return;
            }

            var bowManager = bow.GetComponentInChildren<BowManager>();
            if (bowManager == null) {
                if (!pulling) {
                    return;
                }
                var bowMesh = bow.GetComponentInChildren<MeshFilter>();
                if (bowMesh == null)
                {
                    LogUtils.LogDebug("No bow mesh found for remote player despite pulling a bow");
                    return;
                }
                bowManager = bowMesh.gameObject.AddComponent<BowManager>();
            }
            bowManager.arrowHandTransform = isLeftHanded ? leftHand.transform : rightHand.transform;
            bowManager.pulling = pulling;
        }

        private void ExtractAndSmoothUpdateVector(ZPackage pkg, ref Vector3 v, bool hasRead)
        {
            var newValue = pkg.ReadVector3();
            if (!hasRead)
            {
                v = newValue;
            }
            else if (Vector3.Distance(v, newValue) > MIN_CHANGE)
            {
                v = Vector3.Lerp(v, newValue, 0.2f);
            }
        }

        private static void extractTarget(ZPackage pkg, RemoteTarget target, bool hasTarget)
        {
            target.SetTarget(pkg.ReadVector3(), pkg.ReadQuaternion(), hasTarget);
            var velocity = pkg.ReadVector3(); // Deprecated
        }

        private static bool hasMoreData(ZPackage pkg)
        {
            return pkg.m_reader.BaseStream.Position < pkg.GetArray().Length;
        }

        // An IK target of a remote player: its position relative to the player and its rotation, eased each frame
        // towards the latest that the owner sent.
        private class RemoteTarget
        {
            private readonly GameObject obj;
            private Vector3 targetRelativePosition;
            private Quaternion targetRotation = Quaternion.identity;
            private Vector3 relativePosition;

            public RemoteTarget(GameObject obj)
            {
                this.obj = obj;
            }

            // Without a previous target, the target is jumped to rather than eased towards.
            public void SetTarget(Vector3 relativePosition, Quaternion rotation, bool hasTarget)
            {
                targetRelativePosition = relativePosition;
                targetRotation = rotation;
                if (!hasTarget)
                {
                    this.relativePosition = relativePosition;
                    obj.transform.rotation = rotation;
                }
            }

            public void Apply(Vector3 playerPosition, float t)
            {
                relativePosition =
                    Vector3.Distance(relativePosition, targetRelativePosition) > MIN_CHANGE ?
                        Vector3.Lerp(relativePosition, targetRelativePosition, t) :
                        targetRelativePosition;
                obj.transform.position = playerPosition + relativePosition;
                if (Quaternion.Angle(obj.transform.rotation, targetRotation) > MIN_CHANGE)
                {
                    obj.transform.rotation = Quaternion.Slerp(obj.transform.rotation, targetRotation, t);
                }
            }
        }

        private void maybeAddVrik() {
            if (vrikSync != null)
            {
                return;
            }
            vrikSync =
                VrikCreator.initialize(
                    gameObject, leftHand.transform, rightHand.transform, camera.transform, pelvis.transform, leftFoot.transform, rightFoot.transform);
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

    public class ClientWeaponSync : MonoBehaviour
    {
        private VRPlayerSync playerSync { get { return _playerSync == null ? (_playerSync = GetComponentInParent<VRPlayerSync>()) : _playerSync; } }
        private VRPlayerSync _playerSync;

        protected void OnRenderObject()
        {
            if (playerSync == null)
            {
                return;
            }
            transform.localPosition = playerSync.weaponSyncLocalPosition;
            transform.localRotation = playerSync.weaponSyncLocalRotation;
        }
    }
}
