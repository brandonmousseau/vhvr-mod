using UnityEngine;

namespace ValheimVRMod.Scripts {
    public class VRPlayerSync : MonoBehaviour {

        private bool vrikInitialized;
        
        public GameObject camera = new GameObject();
        public GameObject rightHand = new GameObject();
        public GameObject leftHand = new GameObject();

        private void FixedUpdate() {

            Player player = GetComponent<Player>();

            if (player == Player.m_localPlayer) {
                sendVrData();
            }
            else if (GetComponent<ZNetView>() != null) {
                receiveVrData();
            }
        }

        private void sendVrData() {
            
            ZPackage pkg = new ZPackage();
            pkg.Write(camera.transform.position);
            pkg.Write(camera.transform.rotation);
            pkg.Write(leftHand.transform.position);
            pkg.Write(leftHand.transform.rotation);
            pkg.Write(rightHand.transform.position);
            pkg.Write(rightHand.transform.rotation);
            
            GetComponent<ZNetView>().GetZDO().Set("vr_data", pkg.GetArray());
        }
        
        private void receiveVrData() {
            
            var vr_data = GetComponent<ZNetView>().GetZDO().GetByteArray("vr_data");
            
            if (vr_data == null) {
                return;
            }

            ZPackage pkg = new ZPackage(vr_data);
            
            camera.transform.position = pkg.ReadVector3();
            camera.transform.rotation = pkg.ReadQuaternion();
            leftHand.transform.position = pkg.ReadVector3();
            leftHand.transform.rotation = pkg.ReadQuaternion();
            rightHand.transform.position = pkg.ReadVector3();
            rightHand.transform.rotation = pkg.ReadQuaternion();
            
            maybeAddVrik();
        }

        private void maybeAddVrik() {
            if (vrikInitialized) {
                return;
            }

            VrikCreator.initialize(gameObject, leftHand.transform,
                rightHand.transform, camera.transform);
            
            vrikInitialized = true;
        }
    }
}