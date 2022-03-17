using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRMod.Scripts.Block {
    public abstract class Block : MonoBehaviour {
        
        // CONST
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
        protected Transform hand;
        protected MeshCooldown _meshCooldown;
        public float blockTimer = blockTimerNonParry;

        private void FixedUpdate() {
            tickCounter++;
            if (tickCounter < 5) {
                return;
            }
            
            Vector3 posStart = Player.m_localPlayer.transform.InverseTransformPoint(hand.position);
            Vector3 posEnd = posStart;
            snapshots.Add(posStart);

            if (snapshots.Count > maxSnapshots) {
                snapshots.RemoveAt(0);
            }

            tickCounter = 0;
            var dist = 0.0f;

            foreach (Vector3 snapshot in snapshots) {
                var curDist = Vector3.Distance(snapshot, posEnd);
                if (curDist > dist) {
                    dist = curDist;
                    posStart = snapshot;
                }
            }
            
            ParryCheck(posStart, posEnd);
        }
        
        public abstract void setBlocking(Vector3 hitDir);
        protected abstract void ParryCheck(Vector3 posStart, Vector3 posEnd);

        public void resetBlocking() {
            _blocking = false;
            blockTimer = blockTimerNonParry;
        }

        public bool isBlocking() {
            return _blocking && !_meshCooldown.inCoolDown();
        }
        
        public void block() {
            _meshCooldown.tryTrigger(cooldown);
        }
    }
}