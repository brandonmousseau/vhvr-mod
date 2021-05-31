using UnityEngine;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Scripts {
    public class VRPlayerSync : MonoBehaviour {

        private bool vrikInitialized;

        static readonly float MIN_CHANGE = 0.001f;
        
        public GameObject camera = new GameObject();
        public GameObject rightHand = new GameObject();
        public GameObject leftHand = new GameObject();

        private Vector3 ownerLastPositionCamera = Vector3.zero;
        private Vector3 ownerVelocityCamera = Vector3.zero;
        private Vector3 ownerLastPositionLeft = Vector3.zero;
        private Vector3 ownerVelocityLeft = Vector3.zero;
        private Vector3 ownerLastPositionRight = Vector3.zero;
        private Vector3 ownerVelocityRight = Vector3.zero;

        private uint lastDataRevision = 0;
        private float deltaTimeCounter = 0f;

        void Start()
        {
            if (isOwner())
            {
                updateOwnerLastPositions();
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.deltaTime;
            if (isOwner())
            {
                calculateOwnerVelocities(dt);
            }
            if (isValid() && !isOwner()) {
                clientSync(dt);
            }
        }

        private void calculateOwnerVelocities(float dt)
        {
            ownerVelocityCamera = (camera.transform.position - ownerLastPositionCamera) / dt;
            ownerVelocityLeft = (leftHand.transform.position - ownerLastPositionLeft) / dt;
            ownerVelocityRight = (rightHand.transform.position - ownerLastPositionRight) / dt;
            // Update "last" position for next cycle velocity calculation
            updateOwnerLastPositions();
        }

        private void updateOwnerLastPositions()
        {
            ownerLastPositionCamera = camera.transform.position;
            ownerLastPositionLeft = leftHand.transform.position;
            ownerLastPositionRight = rightHand.transform.position;
        }

        private void LateUpdate()
        {
            if (isOwner())
            {
                ownerSync();
            }
        }

        // Transmit position, rotation, and velocity information to server
        private void ownerSync()
        {
            ZPackage pkg = new ZPackage();
            writeData(pkg, camera, ownerVelocityCamera);
            writeData(pkg, leftHand, ownerVelocityLeft);
            writeData(pkg, rightHand, ownerVelocityRight);
            // TODO Fingers
            
            GetComponent<ZNetView>().GetZDO().Set("vr_data", pkg.GetArray());
        }

        private void writeData(ZPackage pkg, GameObject obj, Vector3 ownerVelocity) 
        {
            pkg.Write(obj.transform.localPosition);
            pkg.Write(obj.transform.localRotation);
            pkg.Write(ownerVelocity);
        }
        
        private void clientSync(float dt)
        {
            syncPositionAndRotation(GetComponent<ZNetView>().GetZDO(), dt);
            maybeAddVrik();
        }

        private void syncPositionAndRotation(ZDO zdo, float dt)
        {
            if (zdo == null)
            {
                LogError("Null ZDO in syncPosition");
                return;
            }
            var vr_data = zdo.GetByteArray("vr_data");
            if (vr_data == null)
            {
                LogDebug("Null VR Data in syncPosition");
                return;
            }
            ZPackage pkg = new ZPackage(vr_data);
            var currentDataRevision = zdo.m_dataRevision;
            if (currentDataRevision != lastDataRevision)
            {
                // New data revision since last sync so we reset our deltaT counter
                deltaTimeCounter = 0f;
                // Save the current data revision so we can detect the next new data package
                lastDataRevision = currentDataRevision;
            }
            deltaTimeCounter += dt;

            extractAndUpdate(pkg, ref camera);
            extractAndUpdate(pkg, ref leftHand);
            extractAndUpdate(pkg, ref rightHand);
            // TODO Fingers
            
        }

        private void extractAndUpdate(ZPackage pkg, ref GameObject obj) 
        {
            // Extract package data
            var position = pkg.ReadVector3();
            var rotation = pkg.ReadQuaternion();
            var velocity = pkg.ReadVector3();
            
            // Update position based on last written position, velocity, and elapsed time since last data revision
            position += velocity * deltaTimeCounter;
            
            // Update the object position with new calculated position
            updatePosition(obj, position);
            
            // Update the rotation
            updateRotation(obj, rotation);
        }

        private static void updatePosition(GameObject obj, Vector3 position)
        {
            if (Vector3.Distance(obj.transform.localPosition, position) > MIN_CHANGE)
            {
                obj.transform.localPosition = position;
            }
        }

        private static void updateRotation(GameObject obj, Quaternion rotation)
        {
            if (Quaternion.Angle(obj.transform.localRotation, rotation) > MIN_CHANGE)
            {
                obj.transform.localRotation = Quaternion.Slerp(obj.transform.localRotation, rotation, 0.2f);
            }
        }

        private void maybeAddVrik() {
            if (vrikInitialized)
            {
                return;
            }
            VrikCreator.initialize(gameObject, leftHand.transform,
                rightHand.transform, camera.transform);
            vrikInitialized = true;
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
    }
}