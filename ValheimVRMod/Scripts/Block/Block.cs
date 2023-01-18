using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using Valve.VR;

namespace ValheimVRMod.Scripts.Block {

    public abstract class Block : MonoBehaviour {
        // CONST
        private const float BlockDistanceTolerance = 0.05f;
        private const float cooldown = 1;
        private const int maxSnapshots = 7;
        protected const float blockTimerParry = 0.1f;
        protected const float minDist = 0.4f;
        public const float blockTimerTolerance = blockTimerParry + 0.2f;
        public const float blockTimerNonParry = 9999f;

        // VARIABLE
        private int tickCounter;
        protected bool _blocking;
        protected List<Vector3> snapshots = new List<Vector3>();
        protected List<Vector3> snapshotsLeft = new List<Vector3>();
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
        private Transform lastRenderedTransform;
        protected PhysicsEstimator physicsEstimator;

        protected virtual void Awake()
        {
            lastRenderedTransform = new GameObject().transform;
            physicsEstimator = lastRenderedTransform.gameObject.AddComponent<PhysicsEstimator>();
            physicsEstimator.refTransform = gameObject.GetComponentInParent<Player>().transform;
        }

        protected virtual void OnRenderObject()
        {
            // The transform of the shield may not be valid outside OnRenderObject(), therefore we need to record its state for later use.
            lastRenderedTransform.parent = transform;
            lastRenderedTransform.SetPositionAndRotation(transform.position, transform.rotation);
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
            
            Vector3 posStart = Player.m_localPlayer.transform.InverseTransformPoint(hand.position);
            Vector3 posEnd = posStart;
            snapshots.Add(posStart);
            Vector3 posStart2 = Player.m_localPlayer.transform.InverseTransformPoint(offhand.position);
            Vector3 posEnd2 = posStart2;
            snapshotsLeft.Add(posStart2);

            if (snapshots.Count > maxSnapshots) {
                snapshots.RemoveAt(0);
                snapshotsLeft.RemoveAt(0);
            }

            tickCounter = 0;
            var dist = 0.0f;
            var dist2 = 0.0f;

            foreach (Vector3 snapshot in snapshots) {
                var curDist = Vector3.Distance(snapshot, posEnd);
                if (curDist > dist) {
                    dist = curDist;
                    posStart = snapshot;
                }
            }
            foreach (Vector3 snapshot in snapshotsLeft)
            {
                var curDist = Vector3.Distance(snapshot, posEnd2);
                if (curDist > dist2)
                {
                    dist2 = curDist;
                    posStart2 = snapshot;
                }
            }

            if (VHVRConfig.BlockingType() != "GrabButton")
                ParryCheck(posStart, posEnd , posStart2, posEnd2);

            if(wasGetHit && !SteamVR_Actions.valheim_Grab.GetState(currhand))
            {
                _meshCooldown.tryTrigger(cooldown);
                wasGetHit = false;
            }
        }
        public abstract void setBlocking(HitData hitData);
        protected abstract void ParryCheck(Vector3 posStart, Vector3 posEnd, Vector3 posStart2, Vector3 posEnd2);

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
            if (Player.m_localPlayer.IsStaggering())
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
            Mesh mesh = gameObject.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null)
            {
                // Cannot find mesh bounds, abort calculation.
                return true;
            }

            Bounds blockBounds = new Bounds(mesh.bounds.center, mesh.bounds.size);
            blockBounds.Expand(GetBlockTolerance(hitData.m_damage, hitData.m_pushForce));

            return WeaponUtils.LineIntersectsWithBounds(
                blockBounds,
                lastRenderedTransform.InverseTransformPoint(hitData.m_point),
                lastRenderedTransform.InverseTransformDirection(hitData.m_dir));
        }

        protected float GetBlockTolerance(HitData.DamageTypes damageTypes, float pushForce) {
            // Adjust for block tolerance radius: blunt damage and push force increases tolerance raidus whereas pierce damage decreases tolerance radius.
            return 0.5f * Mathf.Log(1 + damageTypes.m_blunt * 0.1f + pushForce * 0.01f) / Mathf.Log(3 + damageTypes.m_pierce * 0.1f);
        }
    }
}
