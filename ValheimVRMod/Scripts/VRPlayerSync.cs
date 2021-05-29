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
            else {
                receiveVrData();
            }
        }

        private void sendVrData() {
            GetComponent<ZNetView>().GetZDO().Set("vr_cam_pos", camera.transform.position);
            GetComponent<ZNetView>().GetZDO().Set("vr_cam_rot", camera.transform.rotation);
            GetComponent<ZNetView>().GetZDO().Set("vr_rh_pos", rightHand.transform.position);
            GetComponent<ZNetView>().GetZDO().Set("vr_rh_rot", rightHand.transform.rotation);
            GetComponent<ZNetView>().GetZDO().Set("vr_lh_pos", leftHand.transform.position);
            GetComponent<ZNetView>().GetZDO().Set("vr_lh_rot", leftHand.transform.rotation);
        }
        
        private void receiveVrData() {
             camera.transform.position = GetComponent<ZNetView>().GetZDO().GetVec3("vr_cam_pos", Vector3.zero);
             camera.transform.rotation = GetComponent<ZNetView>().GetZDO().GetQuaternion("vr_cam_rot", Quaternion.identity);
             rightHand.transform.position = GetComponent<ZNetView>().GetZDO().GetVec3("vr_rh_pos", Vector3.zero);
             rightHand.transform.rotation = GetComponent<ZNetView>().GetZDO().GetQuaternion("vr_rh_rot", Quaternion.identity);
             leftHand.transform.position = GetComponent<ZNetView>().GetZDO().GetVec3("vr_lh_pos", Vector3.zero);
             leftHand.transform.rotation = GetComponent<ZNetView>().GetZDO().GetQuaternion("vr_lh_rot", Quaternion.identity);
             maybeAddVrik();
        }

        private void maybeAddVrik() {
            if (vrikInitialized || camera.transform.position == Vector3.zero) {
                return;
            }

            VrikCreator.initialize(GetComponent<Player>().gameObject, leftHand.transform,
                rightHand.transform, camera.transform);
            
            vrikInitialized = true;
        }
    }
}