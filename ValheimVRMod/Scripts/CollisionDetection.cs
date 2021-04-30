using static ValheimVRMod.Utilities.LogUtils;
using System.ComponentModel;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    public class CollisionDetection : MonoBehaviour
    {
        
        private GameObject colliderParent = new GameObject();

        private void OnTriggerEnter(Collider collider)
        {
            
            if (Player.m_localPlayer == null)
            {
                return;
            }
            
            ItemDrop.ItemData item = Player.m_localPlayer.GetRightItem();

            if (item == null)
            {
                return;
            }

            Debug.Log("ITEM SHARED NAME: " + Player.m_localPlayer.GetRightItem().m_shared.m_name);
            Debug.Log("Collision Detected. OWN: " + gameObject.layer + " - " + name +
                      " OTHER: " + collider.gameObject.layer + " - " + collider.gameObject.name);

            
            IDestructible destructible = collider.transform.GetComponentInParent<IDestructible>();

            if (destructible == null)
            {
                Debug.Log("NO DESTRUCTIBLE FOUND");
            }
            else
            {
                Debug.Log("DESTRUCTIBLE FOUND: " + typeof(Destructible));
            }

        }

        private void OnRenderObject()
        {

            if (!GetComponent<MeshRenderer>().enabled)
            {
                return;
            }
            
            transform.SetParent(colliderParent.transform);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.SetParent(null, true);
            
        }

        public void setColliderParent(Transform obj, string name)
        {

            try
            {
                WeaponCollider colliderData = WeaponUtils.getForName(name);
                colliderParent.transform.parent = obj;
                colliderParent.transform.localPosition = colliderData.pos;
                colliderParent.transform.localRotation = Quaternion.Euler(colliderData.euler);
                colliderParent.transform.localScale = colliderData.scale;
                GetComponent<MeshRenderer>().enabled = true;
            }
            catch (InvalidEnumArgumentException)
            {
                GetComponent<MeshRenderer>().enabled = false;
                LogDebug("Invalid Weapon Data for: " + name);
            }
        }
        
    }
}