using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts.Block {
    public class FistBlock : Block {

        private const float maxParryAngle = 45f;
        private readonly Vector3 handUp = new Vector3(0, -0.15f, -0.85f);

        private GameObject leftHandBlockBox;
        private GameObject rightHandBlockBox;

        public static FistBlock instance;

        private void OnDisable() {
            instance = null;
        }
        
        protected override void Awake() {
            base.Awake();
            _meshCooldown = gameObject.AddComponent<MeshCooldown>();
            instance = this;
            hand = VHVRConfig.LeftHanded() ? VRPlayer.leftHand.transform : VRPlayer.rightHand.transform;
            offhand = VHVRConfig.LeftHanded() ? VRPlayer.rightHand.transform : VRPlayer.leftHand.transform;
            CreateBlockBoxes();
        }

        public override void setBlocking(Vector3 hitPoint, Vector3 hitDir) {
            if (FistCollision.instance.usingDualKnives() || FistCollision.instance.usingFistWeapon())
            {
                if (WeaponUtils.LineIntersectsWithBounds(leftHandBlockBox.GetComponent<MeshFilter>().sharedMesh.bounds, leftHandBlockBox.transform.InverseTransformPoint(hitPoint), leftHandBlockBox.transform.InverseTransformDirection(hitDir)))
                {
                    _blocking = true;
                }
                else if (WeaponUtils.LineIntersectsWithBounds(rightHandBlockBox.GetComponent<MeshFilter>().sharedMesh.bounds, rightHandBlockBox.transform.InverseTransformPoint(hitPoint), rightHandBlockBox.transform.InverseTransformPoint(hitDir)))
                {   
                    _blocking = true;
                } else
                {
                    _blocking = false;
                }
            }
            else
            {
                _blocking = false;
            }
        }

        protected override void ParryCheck(Vector3 posStart, Vector3 posEnd, Vector3 posStart2, Vector3 posEnd2) {
            if (FistCollision.instance.usingFistWeapon())
            {
                if (Vector3.Distance(posEnd, posStart) > minDist)
                {
                    blockTimer = blockTimerParry;
                }
                else if (Vector3.Distance(posEnd2, posStart2) > minDist)
                {
                    blockTimer = blockTimerParry;
                }
                else
                {
                    blockTimer = blockTimerNonParry;
                }
            }
        }
        
        private void CreateBlockBoxes()
        {
            leftHandBlockBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftHandBlockBox.transform.parent = VRPlayer.leftHand.transform;
            leftHandBlockBox.transform.localRotation = Quaternion.Euler(45, 0, 0);
            leftHandBlockBox.transform.localPosition = new Vector3(0, 0.15f, -0.2f);
            leftHandBlockBox.transform.localScale = new Vector3(0.25f, 0.25f, 0.8f);
            leftHandBlockBox.GetComponent<MeshRenderer>().enabled = false;
            Destroy(leftHandBlockBox.GetComponent<Collider>());

            rightHandBlockBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightHandBlockBox.transform.parent = VRPlayer.rightHand.transform;
            rightHandBlockBox.transform.localRotation = Quaternion.Euler(45, 0, 0);
            rightHandBlockBox.transform.localPosition = new Vector3(0, 0.15f, -0.2f);
            rightHandBlockBox.transform.localScale = new Vector3(0.25f, 0.25f, 0.8f);
            rightHandBlockBox.GetComponent<MeshRenderer>().enabled = false;
            Destroy(rightHandBlockBox.GetComponent<Collider>());
        }        
    }
}
