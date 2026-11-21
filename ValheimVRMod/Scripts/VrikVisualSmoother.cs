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
        // Smoothing only applies beyond this distance from the world origin. Within it the jitter is negligible.
        private const float MIN_DISTANCE_FROM_ORIGIN = 500f;
        // The spacing of floats around 1, which the spacing around any value is roughly that value times.
        private const float FLOAT_RELATIVE_PRECISION = 1.1920929e-7f;
        // How many float steps at the body's distance from the origin the solved pose jitters by, as its noise scale.
        private const float NOISE_IN_FLOAT_STEPS = 4f;
        // Turns the noise scale of positions into that of rotations: roughly the length of the bones whose ends the
        // solve places.
        private const float NOISE_LEVER = 0.25f;
        // Time constant, in seconds, of following a difference as big as the noise scale. The follow speed grows with
        // the square of the difference: a difference within the noise scale, which is mostly jitter, is followed
        // slowly, and one a few times bigger, which is real movement, within a frame or two.
        private const float NOISE_FOLLOW_TIME = 0.1f;

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
            float distanceFromOrigin = vrik != null ? vrik.references.root.position.magnitude : 0;
            if (vrik == null || vrik.solver.LOD >= 2 || distanceFromOrigin < MIN_DISTANCE_FROM_ORIGIN)
            {
                lastSmoothedFrame = -1;
                return;
            }

            // Start over from the solved pose after any frame not smoothed, e. g. while VRIK was disabled.
            bool isContinuing = lastSmoothedFrame == Time.frameCount - 1;
            lastSmoothedFrame = Time.frameCount;
            float noiseDistance = distanceFromOrigin * FLOAT_RELATIVE_PRECISION * NOISE_IN_FLOAT_STEPS;
            float noiseAngle = noiseDistance / NOISE_LEVER;
            float deltaTime = Time.unscaledDeltaTime;

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
                    GetBlend(AngleBetween(smoothedLocalRotations[i], solvedRotation) / noiseAngle, deltaTime);
                bone.localRotation =
                    smoothedLocalRotations[i] = Quaternion.Slerp(smoothedLocalRotations[i], solvedRotation, rotationBlend);

                if (HasSolvedLocalPosition(i))
                {
                    Vector3 solvedPosition = bone.localPosition;
                    float positionBlend =
                        GetBlend(Vector3.Distance(smoothedLocalPositions[i], solvedPosition) / noiseDistance, deltaTime);
                    bone.localPosition =
                        smoothedLocalPositions[i] = Vector3.Lerp(smoothedLocalPositions[i], solvedPosition, positionBlend);
                }
            }
        }

        // The fraction of the way to the solved pose to move this frame, given the difference from it in noise scales.
        private static float GetBlend(float differenceInNoise, float deltaTime)
        {
            return 1 - Mathf.Exp(-deltaTime / NOISE_FOLLOW_TIME * differenceInNoise * differenceInNoise);
        }

        // In radians. Unlike Quaternion.Angle, which reads any angle below about 0.16 degrees as 0, it measures the
        // small differences that jitter is made of.
        private static float AngleBetween(Quaternion a, Quaternion b)
        {
            Quaternion difference = Quaternion.Inverse(a) * b;
            float sine = new Vector3(difference.x, difference.y, difference.z).magnitude;
            return 2 * Mathf.Atan2(sine, Mathf.Abs(difference.w));
        }
    }
}
