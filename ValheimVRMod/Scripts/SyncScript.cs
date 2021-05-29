using UnityEngine;

namespace ValheimVRMod.Scripts {
    public class SyncScript : MonoBehaviour{
        
        private void FixedUpdate()
        {
            GetComponent<ZSyncTransform>().SyncNow();
        }
    }
}