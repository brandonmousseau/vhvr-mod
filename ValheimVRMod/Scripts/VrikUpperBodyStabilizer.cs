using RootMotion.FinalIK;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRMod.Scripts
{
    // VRIK solves on top of the animated pose and keeps much of it in the upper body: its shoulder solving only adds
    // to the animated shoulder rotations, its spine follows the head only by neckStiffness (0.2 by default), keeping
    // the rest of the animated chest, and it does not solve torso bones besides its spine and chest at all. So e. g. the walk cycle would keep swaying the chest and the shoulders of an
    // otherwise tracked upper body. Restoring these bones to their rest pose before each solve leaves them to VRIK
    // alone. Whenever VRIK is off, it does not solve, and the animation keeps them.
    class VrikUpperBodyStabilizer : MonoBehaviour
    {
        private VRIK vrik;
        private Transform[] bones;
        private Quaternion[] restRotations;

        // Calling it again for the same VRIK, e. g. on unpausing, keeps the rest pose taken the first time.
        public static void Attach(VRIK vrik)
        {
            var stabilizer = vrik.gameObject.GetComponent<VrikUpperBodyStabilizer>();
            if (stabilizer == null)
            {
                stabilizer = vrik.gameObject.AddComponent<VrikUpperBodyStabilizer>();
            }
            else if (stabilizer.vrik == vrik)
            {
                return;
            }
            stabilizer.initialize(vrik);
        }

        private void initialize(VRIK newVrik)
        {
            unsubscribe();
            vrik = newVrik;
            bones = getUpperBodyBones(vrik.references);
            restRotations = new Quaternion[bones.Length];
            Avatar avatar = vrik.GetComponentInChildren<Animator>()?.avatar;
            for (int i = 0; i < bones.Length; i++)
            {
                restRotations[i] = getRestRotation(bones[i], avatar);
            }
            vrik.solver.OnPreUpdate += restoreRestPose;
        }

        // Every bone between the pelvis and the head, rather than only the spine and the chest VRIK knows of: VRIK
        // leaves any other one, e. g. an upper chest, to the animation. And the shoulders.
        private static Transform[] getUpperBodyBones(VRIK.References references)
        {
            var upperBodyBones = new List<Transform>();
            if (references.head != null && references.pelvis != null && references.head.IsChildOf(references.pelvis))
            {
                for (Transform bone = references.head.parent; bone != references.pelvis; bone = bone.parent)
                {
                    upperBodyBones.Add(bone);
                }
            }
            else
            {
                upperBodyBones.Add(references.spine);
                upperBodyBones.Add(references.chest);
                upperBodyBones.Add(references.neck);
            }
            upperBodyBones.Add(references.leftShoulder);
            upperBodyBones.Add(references.rightShoulder);
            return upperBodyBones.ToArray();
        }

        // The rotation in the avatar's own skeleton, rather than whatever the animation happens to pose when VRIK is
        // created, e. g. waking up after spawning.
        private static Quaternion getRestRotation(Transform bone, Avatar avatar)
        {
            if (bone == null)
            {
                return Quaternion.identity;
            }
            if (avatar != null && avatar.isHuman)
            {
                foreach (SkeletonBone skeletonBone in avatar.humanDescription.skeleton)
                {
                    if (skeletonBone.name == bone.name)
                    {
                        return skeletonBone.rotation;
                    }
                }
            }
            return bone.localRotation;
        }

        private void restoreRestPose()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null)
                {
                    bones[i].localRotation = restRotations[i];
                }
            }
        }

        private void unsubscribe()
        {
            if (vrik != null)
            {
                vrik.solver.OnPreUpdate -= restoreRestPose;
            }
        }

        private void OnDestroy()
        {
            unsubscribe();
        }
    }
}
