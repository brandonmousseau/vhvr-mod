using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using Valve.VR;

namespace ValheimVRMod.Scripts.Block {

    public abstract class Block : MonoBehaviour {
        // CONST
        private const float cooldown = 1;
        protected const float blockTimerParry = 0.1f;
        public const float blockTimerTolerance = blockTimerParry + 0.2f;
        public const float blockTimerNonParry = 9999f;

        // VARIABLE
        private int tickCounter;
        protected bool _blocking;
        protected Transform hand;
        protected Transform offhand;
        protected MeshCooldown _meshCooldown;
        public float blockTimer = blockTimerNonParry;
        protected SteamVR_Input_Sources mainHandSource = VHVRConfig.LeftHanded() ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;
        protected SteamVR_Input_Sources offHandSource = VHVRConfig.LeftHanded() ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;
        protected SteamVR_Input_Sources currhand = VHVRConfig.LeftHanded() ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;
        protected bool wasParryStart = false;
        public bool wasResetTimer = false;
        public bool wasGetHit = false;
        private MeshFilter meshFilter;
        private Transform lastRenderedTransform;
        private Collider blockCollider;
        protected PhysicsEstimator physicsEstimator;

        protected virtual void Awake()
        {
            lastRenderedTransform = new GameObject().transform;
            physicsEstimator = lastRenderedTransform.gameObject.AddComponent<PhysicsEstimator>();
            physicsEstimator.refTransform = gameObject.GetComponentInParent<Player>().transform;
            meshFilter = gameObject.GetComponentInChildren<MeshFilter>();
        }

        protected virtual void OnRenderObject()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponentInChildren<MeshFilter>(includeInactive: true);
                if (meshFilter == null)
                {
                    return;
                }
            }


            // The transform of the shield may not be valid outside OnRenderObject(), therefore we need to record its state for later use.
            lastRenderedTransform.parent = meshFilter.transform;
            lastRenderedTransform.SetPositionAndRotation(meshFilter.transform.position, meshFilter.transform.rotation);
            lastRenderedTransform.localScale = Vector3.one;
            lastRenderedTransform.SetParent(null, true);
        }

        //Currently there's 2 Blocking type 
        //"MotionControl" and "GrabButton"
        protected virtual void FixedUpdate() {
            tickCounter++;
            if (tickCounter < 5) {
                return;
            }
            
            tickCounter = 0;

            if(wasGetHit && !SteamVR_Actions.valheim_Grab.GetState(currhand))
            {
                _meshCooldown.tryTrigger(cooldown);
                wasGetHit = false;
            }
        }

        protected virtual void OnDestroy()
        {
            if (blockCollider?.gameObject != null)
            {
                Destroy(blockCollider.gameObject);
            }

            if (lastRenderedTransform != null)
            {
                Destroy(lastRenderedTransform.gameObject);
            }
        }

        public abstract void setBlocking(HitData hitData);
        public void resetBlocking() {
            if (VHVRConfig.BlockingType() == "GrabButton")
            {
                _blocking = true;
            }
            else
            {
                _blocking = false;
                blockTimer = blockTimerNonParry;
            }
        }

        public bool isBlocking() {
            if (Player.m_localPlayer.IsStaggering() || ButtonSecondaryAttackManager.isSecondaryAttackStarted)
            {
                return false;
            }
            if (VHVRConfig.BlockingType() == "GrabButton")
            {
                return SteamVR_Actions.valheim_Grab.GetState(currhand) && !_meshCooldown.inCoolDown() && _blocking;
            }
            else
            {
                return _blocking && !_meshCooldown.inCoolDown();
            }
        }

        public void block() {
            if (VHVRConfig.BlockingType() != "GrabButton")
            {
                if (SteamVR_Actions.valheim_Grab.GetState(currhand))
                {
                    wasGetHit = true;
                }   
                else
                {
                    _meshCooldown.tryTrigger(cooldown);
                }
            }
        }

        public void UpdateGrabParry()
        {
            currhand = offHandSource;
            if (EquipScript.getLeft() != EquipType.Shield)
            {
                currhand = mainHandSource;
            }
            if (SteamVR_Actions.valheim_Grab.GetState(currhand) && !_meshCooldown.inCoolDown() && !wasParryStart)
            {
                wasParryStart = true;
                wasResetTimer = true;
            }
            else if (!SteamVR_Actions.valheim_Grab.GetState(currhand) && wasParryStart)
            {
                _meshCooldown.tryTrigger(0.4f);
                wasParryStart = false;
            }
        }
        public void resetTimer()
        {
            wasResetTimer = false;
        }

        protected bool hitIntersectsBlockBox(HitData hitData) {
            EnsureBlockCollider();
            blockCollider.enabled = true;
            var intersects = hitIntersectsBlockBox(hitData, EnsureBlockCollider());
            // Disable the collider so projectiles do not get stuck in weapon.
            blockCollider.enabled = false;
            return intersects;
        }

        protected static bool hitIntersectsBlockBox(HitData hitData, Collider blockCollider)
        {
            if (blockCollider == null)
            {
                return true;
            }

            var hitStart = hitData.m_point;
            var hitEnd =
                hitStart +
                Vector3.Project(
                    Player.m_localPlayer.transform.position - hitStart,
                    hitData.m_dir);
            var hitSource = hitData.GetAttacker()?.transform?.position ?? hitStart;
            var hitVector = Vector3.Project(hitEnd - hitSource, hitData.m_dir);
            var hitRadius = GetBlockTolerance(hitData.m_damage, hitData.m_pushForce);
            var hits =
                Physics.SphereCastAll(
                    hitEnd - hitVector.normalized * hitRadius,
                    hitRadius,
                    -hitVector,
                    maxDistance: hitVector.magnitude * 2);
            
            foreach (var hit in hits)
            {
                if (hit.collider == blockCollider)
                {
                    return true;
                }
            }

            return false;
        }

        protected static float GetBlockTolerance(HitData.DamageTypes damageTypes, float pushForce) {
            // Adjust for block tolerance radius: blunt damage and push force increases tolerance raidus whereas pierce damage decreases tolerance radius.
            return 0.5f * Mathf.Log(1 + damageTypes.m_blunt * 0.1f + pushForce * 0.01f) * (0.5f + 1f / Mathf.Log(8 + damageTypes.m_pierce * 0.1f));
        }

        private Collider EnsureBlockCollider()
        {
            if (blockCollider != null)
            {
                return blockCollider;
            }

            var mesh = meshFilter?.sharedMesh;
            if (mesh == null)
            {
                // Cannot find mesh bounds, abort calculation.
                return null;
            }

            blockCollider = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Collider>();
            var renderer = blockCollider.GetComponent<MeshRenderer>();
            if (VHVRConfig.ShowDebugColliders())
            {
                renderer.material = Object.Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
                renderer.material.color = new Vector4(0.5f, 0.25f, 0, 0.5f);
            }
            else
            {
                Destroy(renderer);
            }

            blockCollider.isTrigger = true;
            blockCollider.gameObject.layer = LayerUtils.CHARACTER;
            blockCollider.transform.parent = lastRenderedTransform;
            blockCollider.transform.localPosition = mesh.bounds.center;
            blockCollider.transform.localRotation = Quaternion.identity;
            blockCollider.transform.localScale = mesh.bounds.size;
            blockCollider.enabled = false;

            return blockCollider;
        }
    }
}
