using System.Collections.Generic;
using static ValheimVRMod.Utilities.LogUtils;
using System.ComponentModel;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    public class CollisionDetection : MonoBehaviour
    {
        
        private const float MIN_DISTANCE = 0.75f;
        private const int MAX_SNAPSHOTS = 7;    
        
        private bool scriptActive;
        private GameObject colliderParent = new GameObject();
        private int tickCounter;
        private List<Vector3> snapshots = new List<Vector3>();

        public Vector3 lastHitPoint;
        public Collider lastHitCollider;

        private void OnTriggerEnter(Collider collider)
        {
            
            if (Player.m_localPlayer == null)
            {
                return;
            }
            
            var item = Player.m_localPlayer.GetRightItem();

            if (item == null)
            {
                return;
            }

            if (hasMomentum())
            {
                lastHitPoint = transform.position;
                lastHitCollider = collider;

                var attack = Player.m_localPlayer.GetRightItem().m_shared.m_attack.Clone();
                attack.Start(Player.m_localPlayer, null,null,
                    AccessTools.FieldRefAccess<Player, CharacterAnimEvent>(Player.m_localPlayer, "m_animEvent"), 
                    null,  Player.m_localPlayer.GetRightItem(), null, 0.0f, 0.0f);
                
                snapshots.Clear();

            }
            
            Debug.Log("ITEM SHARED NAME: " + Player.m_localPlayer.GetRightItem().m_shared.m_name);
            Debug.Log("Collision Detected. OWN: " + gameObject.layer + " - " + name +
                      " OTHER: " + collider.gameObject.layer + " - " + collider.gameObject.name);

        }

        private void OnRenderObject()
        {

            if (!isCollisionAllowed())
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
                setScriptActive(true);

            }
            catch (InvalidEnumArgumentException)
            {
                setScriptActive(false);
                LogDebug("Invalid Weapon Data for: " + name);
            }
        }

        private bool isCollisionAllowed()
        {
            return scriptActive && VRPlayer.inFirstPerson;
        }

        private void setScriptActive(bool active)
        {

            scriptActive = active;
            
            if (!active)
            {
                snapshots.Clear();
            }
        }
        
        private void FixedUpdate() 
        {

            if (!isCollisionAllowed())
            {
                return;
            }
            
            tickCounter++;
            if (tickCounter < 10)
            {
                return;
            }

            snapshots.Add(transform.position);
        
            if (snapshots.Count > MAX_SNAPSHOTS)
            {
                snapshots.RemoveAt(0);   
            }
            tickCounter = 0;
        }
        
        public bool hasMomentum()
        {
            foreach (Vector3 snapshot in snapshots)
            {
                if (Vector3.Distance(snapshot, transform.position) > MIN_DISTANCE)
                {
                    return true;
                }
            }

            return false;
        }
        
    }
}