using RootMotion.FinalIK;
using UnityEngine;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.Scripts
{
    // Far from the world origin, world positions lose enough float precision that VRIK's solve jitters from frame to
    // frame and the body visibly shakes. This smooths the solved pose there, for rendering only: it filters exactly the
    // bone local transforms that IKSolverVR#FixTransforms() restores at the start of every frame, so neither the solver
    // nor anything feeding it ever sees the smoothed pose.
    public class VrikVisualSmoother : MonoBehaviour
    {
        // Smoothing only applies beyond this distance from the world origin.
        private const float MIN_DISTANCE_FROM_ORIGIN = 500f;
        // A difference between the smoothed and the solved pose within these is mostly jitter and is smoothed heavily.
        // Beyond them it is real movement and followed more and more closely, which keeps the added lag small.
        private const float JITTER_ANGLE = 2f;
        private const float JITTER_DISTANCE = 0.005f;
        // Time constant of the smoothing of jitter, in seconds.
        private const float JITTER_SMOOTHING_TIME = 0.05f;

        private VRIK vrik;
        private Transform[] bones;
        private Vector3[] smoothedLocalPositions;
        private Quaternion[] smoothedLocalRotations;
        private int lastSmoothedFrame = -1;

        public static void Attach(VRIK vrik)
        {
            vrik.gameObject.GetOrAddComponent<VrikVisualSmoother>().Initialize(vrik);
        }

        private void Initialize(VRIK newVrik)
        {
            Unsubscribe();
            vrik = newVrik;
            bones = vrik.references.GetTransforms();
            smoothedLocalPositions = new Vector3[bones.Length];
            smoothedLocalRotations = new Quaternion[bones.Length];
            lastSmoothedFrame = -1;
            vrik.solver.OnPostUpdate += OnPostSolve;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (vrik != null)
            {
                vrik.solver.OnPostUpdate -= OnPostSolve;
            }
        }

        // Same as the bones whose local position IKSolverVR#FixTransforms() restores: the pelvis and the stretchable limb bones.
        private static bool HasSolvedLocalPosition(int boneIndex)
        {
            return boneIndex == 1 ||
                boneIndex == 8 || boneIndex == 9 || boneIndex == 12 || boneIndex == 13 ||
                (boneIndex >= 15 && boneIndex <= 17) || (boneIndex >= 19 && boneIndex <= 21);
        }

        private void OnPostSolve()
        {
            // Without FixTransforms() (LOD 2), smoothed values would feed back into the next solve.
            if (vrik == null || vrik.solver.LOD >= 2 ||
                vrik.references.root.position.magnitude < MIN_DISTANCE_FROM_ORIGIN)
            {
                lastSmoothedFrame = -1;
                return;
            }

            // Start over from the solved pose after any frame not smoothed, e. g. while VRIK was disabled.
            bool isContinuing = lastSmoothedFrame == Time.frameCount - 1;
            lastSmoothedFrame = Time.frameCount;
            float baseBlend = 1 - Mathf.Exp(-Time.deltaTime / JITTER_SMOOTHING_TIME);

            // The root (index 0) is not restored by FixTransforms(), so it is left alone.
            for (int i = 1; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone == null)
                {
                    continue;
                }

                if (!isContinuing)
                {
                    smoothedLocalRotations[i] = bone.localRotation;
                    smoothedLocalPositions[i] = bone.localPosition;
                    continue;
                }

                Quaternion solvedRotation = bone.localRotation;
                float rotationBlend =
                    Mathf.Lerp(baseBlend, 1, Quaternion.Angle(smoothedLocalRotations[i], solvedRotation) / JITTER_ANGLE);
                bone.localRotation =
                    smoothedLocalRotations[i] = Quaternion.Slerp(smoothedLocalRotations[i], solvedRotation, rotationBlend);

                if (HasSolvedLocalPosition(i))
                {
                    Vector3 solvedPosition = bone.localPosition;
                    float positionBlend =
                        Mathf.Lerp(baseBlend, 1, Vector3.Distance(smoothedLocalPositions[i], solvedPosition) / JITTER_DISTANCE);
                    bone.localPosition =
                        smoothedLocalPositions[i] = Vector3.Lerp(smoothedLocalPositions[i], solvedPosition, positionBlend);
                }
            }
        }
    }
}
