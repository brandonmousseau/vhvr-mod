using System;
using System.Threading;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    public class BowManager : MonoBehaviour
    {
        // Constant used for detecting string mesh in the bow mesh.
        private const float minStringSize = 0.965f;
        private static readonly Quaternion originalRotation = new Quaternion(0.4763422f, 0.420751f, 0.5951466f, 0.4918001f); // Euler Angles: 358.1498 78.91683 99.33968
        // A more realistic rotation of the bow relative to hand.
        private static readonly Quaternion adjustedRotation = Quaternion.Euler(0, 120, 90);

        // Vertices extracted from the bow mesh
        private Vector3[] verts;
        private int[] meshTriangles;
        private BoneWeight[] boneWeights;
        private Transform upperLimbBone;
        private Transform lowerLimbBone;
        private Transform stringTop;
        private Transform stringBottom;
        private Transform bowTransformUpdater;
        // The half width of the grip in the coordinate system of the bow object.
        private float gripLocalHalfWidth = 0;
        // Hard coded bow anatomy data including those used when bow anatomy cannot be computed from mesh data.
        private BowAnatomy bowAnatomy;
        private Vector3 handleTop = new Vector3(0, 0, 0);
        private Vector3 handleBottom = new Vector3(0, 0, 0);
        private bool canAccessMesh;
        private bool useCustomShader;

        private Vector3 handleTopInObjectSpace;
        private Vector3 handleBottomInObjectSpace;
        private Vector3 restingStringTopInObjectSpace = new Vector3(0, Mathf.NegativeInfinity, 0);
        private Vector3 restingStringBottomInObjectSpace = new Vector3(0, Mathf.Infinity, 0);
        private Vector3 bowUpInObjectSpace;
        private Vector3 bowRightInObjectSpace;
        private float stringLength;

        protected Transform pullStart;
        // A transform centered and the bow handle center with up vector parallel to the string and pointing upward and forward vector pointing toward the shooting direction.
        protected Transform bowOrientation;
        // An object placed at the nocking point that moves with the pulling hand.
        protected GameObject pullObj;
        // An object placed at the handle center with the direction of the push force as its forward direction.
        protected GameObject pushObj;
        protected bool initialized;
        protected bool wasInitialized;
        protected Outline outline;
        public Transform mainHand;

        private bool wasPulling;
        protected bool bowHandAiming = false;
        public float timeBasedChargePercentage;
        public bool pulling;

        void Awake()
        {
            Mesh mesh = GetComponent<MeshFilter>().mesh;
            verts = mesh.vertices;

            // Need to save this here on the main thread, as accessing mesh.triangles on the background thread
            // throws an exception.
            meshTriangles = mesh.triangles;

            bowOrientation = new GameObject().transform;
            bowOrientation.SetParent(transform.parent, false);
            bowOrientation.localPosition = new Vector3(0, 0, 0);
            bowOrientation.localRotation = originalRotation;

            bowTransformUpdater = new GameObject().transform;
            bowTransformUpdater.SetParent(bowOrientation, false);
            bowTransformUpdater.SetPositionAndRotation(transform.position, transform.rotation);

            // TODO: figure out away to get the correct bow anatomy for non-local players (GetLeftItem() returns null for non-local players).
            bowAnatomy = BowAnatomy.getBowAnatomy(GetComponentInParent<Player>()?.GetLeftItem()?.m_shared?.m_name ?? "");

            bowUpInObjectSpace = transform.InverseTransformDirection(bowOrientation.up);
            bowRightInObjectSpace = transform.InverseTransformDirection(bowOrientation.right);
            float handleTopLocalHeight = Vector3.Dot(transform.InverseTransformPoint(bowOrientation.TransformPoint(new Vector3(0, bowAnatomy.handleHeight * 0.5f, 0))), bowUpInObjectSpace);
            float handleBottomLocalHeight = Vector3.Dot(transform.InverseTransformPoint(bowOrientation.TransformPoint(new Vector3(0, -bowAnatomy.handleHeight * 0.5f, 0))), bowUpInObjectSpace);
            // we need to run this method in thread as it takes longer than a frame and freezes game for a moment
            var xScale = transform.localScale.x;
            Thread thread = new Thread(() => initializeRenderersAsync(handleTopLocalHeight, handleBottomLocalHeight, xScale));
            thread.Start();

            pullObj = new GameObject();
            pullObj.transform.SetParent(bowOrientation, false);
            pullObj.transform.forward *= -1;

            pushObj = new GameObject();
            pushObj.transform.SetParent(bowOrientation, false);
        }

        void Update()
        {
            if (outline == null && gameObject.GetComponent<SkinnedMeshRenderer>() != null)
            {
                createOutline();
            }
        }

        protected void OnDestroy()
        {
            Destroy(pullObj);
            Destroy(pushObj);
            Destroy(pullStart.gameObject);
            Destroy(bowOrientation.gameObject);
            Destroy(bowTransformUpdater.gameObject);
            Destroy(stringTop.gameObject);
            Destroy(stringBottom.gameObject);
            Destroy(upperLimbBone.gameObject);
            Destroy(lowerLimbBone.gameObject);
        }

        protected Vector3 getArrowRestPosition()
        {
            return bowOrientation.TransformPoint(new Vector3(gripLocalHalfWidth * VHVRConfig.ArrowRestHorizontalOffsetMultiplier(), VHVRConfig.ArrowRestElevation(), 0));
        }

        protected float GetBraceHeight()
        {
            return -(pullStart.localPosition.z);
        }

        protected virtual float getPullLengthRestriction(float? drawPercentage = null)
        {
            return 1;
        }

        protected virtual bool OnlyUseDominantHand()
        {
            return false;
        }


        private void initializeRenderersAsync(float handleTopLocalHeight, float handleBottomLocalHeight, float bowScale)
        {

            // Remove the old bow string, which is part of the bow mesh to later replace it with a linerenderer.
            // we are making use of the fact that string triangles are longer then all other triangles
            // so we simply iterate all triangles and compare their vertex distances to a certain minimum size
            // for the new triangle array, we just skip those with bigger vertex distance
            // but we save the top and bottom points of the deleted vertices so we can use them for our new string.

            float stringTopHeight = Mathf.NegativeInfinity;
            float stringBottomHeight = Mathf.Infinity;
            canAccessMesh = false;
            for (int i = 0; i < meshTriangles.Length / 3; i++)
            {
                Vector3 v1 = verts[meshTriangles[i * 3]];
                Vector3 v2 = verts[meshTriangles[i * 3 + 1]];
                Vector3 v3 = verts[meshTriangles[i * 3 + 2]];

                if (Vector3.Distance(v1, v2) < minStringSize &&
                    Vector3.Distance(v2, v3) < minStringSize &&
                    Vector3.Distance(v3, v1) < minStringSize)
                {
                    continue;
                }

                verts[meshTriangles[i * 3 + 1]] = verts[meshTriangles[i * 3]];
                verts[meshTriangles[i * 3 + 2]] = verts[meshTriangles[i * 3]];

                foreach (Vector3 v in new[] { v1, v2, v3 })
                {
                    float currentHeight = Vector3.Dot(v, bowUpInObjectSpace);
                    if (currentHeight > stringTopHeight)
                    {
                        canAccessMesh = true;
                        restingStringTopInObjectSpace = v;
                        stringTopHeight = currentHeight;
                    }

                    if (currentHeight < stringBottomHeight)
                    {
                        canAccessMesh = true;
                        restingStringBottomInObjectSpace = v;
                        stringBottomHeight = currentHeight;
                    }
                }
            }

            useCustomShader = bowAnatomy.bowBendingImpl == BowAnatomy.BowBendingImplType.Shader || (bowAnatomy.bowBendingImpl == BowAnatomy.BowBendingImplType.Auto && !canAccessMesh);

            // Calculate vertex bone weights, find the local z coordinates of the top and bottom of the bow handle, and calculate the grip width.
            boneWeights = new BoneWeight[verts.Length];

            float handleTopVertexHeight = Mathf.NegativeInfinity;
            float handleBottomVertexHeight = Mathf.Infinity;
            float localGripLocalHalfWidth = Mathf.NegativeInfinity;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                float currentHeight = Vector3.Dot(v, bowUpInObjectSpace);
                if (currentHeight > handleTopLocalHeight)
                {
                    // The vertex is in the upper limb.
                    boneWeights[i].boneIndex0 = 0;
                }
                else if (currentHeight >= handleBottomLocalHeight)
                {
                    // The vertex is in the handle.
                    boneWeights[i].boneIndex0 = 1;
                    if (currentHeight > handleTopVertexHeight)
                    {
                        handleTopInObjectSpace = v;
                        handleTopVertexHeight = currentHeight;
                    }
                    if (currentHeight < handleBottomVertexHeight)
                    {
                        handleBottomInObjectSpace = v;
                        handleBottomVertexHeight = currentHeight;
                    }
                }
                else
                {
                    // The vertex is in the lower limb.
                    boneWeights[i].boneIndex0 = 2;
                }
                if (0 <= currentHeight && currentHeight < VHVRConfig.ArrowRestElevation())
                {
                    localGripLocalHalfWidth = Math.Max(Math.Abs(Vector3.Dot(v, bowRightInObjectSpace)), localGripLocalHalfWidth);
                }
                boneWeights[i].weight0 = 1;
            }
            gripLocalHalfWidth = localGripLocalHalfWidth * bowScale;

            bowOrientation.localRotation = adjustedRotation;
            transform.SetPositionAndRotation(bowTransformUpdater.position, bowTransformUpdater.rotation);

            initialized = true;
        }

        private void createLimbBones()
        {
            // Create the bones for the limbs.
            upperLimbBone = new GameObject("BowUpperLimbBone").transform;
            lowerLimbBone = new GameObject("BowLowerLimbBone").transform;
            upperLimbBone.parent = lowerLimbBone.parent = bowOrientation;
            upperLimbBone.localPosition = new Vector3(0, bowAnatomy.handleHeight / 2, handleTop.z);
            lowerLimbBone.localPosition = new Vector3(0, -bowAnatomy.handleHeight / 2, handleBottom.z);
            upperLimbBone.localRotation = Quaternion.identity;
            lowerLimbBone.localRotation = Quaternion.identity;
        }

        private void initializeStringPosition()
        {
            // Initialize string position
            stringTop = new GameObject().transform;
            stringBottom = new GameObject().transform;
            stringTop.SetParent(upperLimbBone, false);
            stringBottom.SetParent(lowerLimbBone, false);
            stringTop.position = transform.TransformPoint(restingStringTopInObjectSpace);
            stringBottom.position = transform.TransformPoint(restingStringBottomInObjectSpace);
            stringLength = bowOrientation.InverseTransformVector(stringTop.position - stringBottom.position).magnitude;
            pullStart = new GameObject().transform;
            pullStart.parent = bowOrientation;
            pullStart.position = Vector3.Lerp(stringTop.position, stringBottom.position, 0.5f);
        }

        private void PostInit()
        {
            if (canAccessMesh)
            {
                handleTop = bowOrientation.InverseTransformPoint(transform.TransformPoint(handleTopInObjectSpace));
                handleBottom = bowOrientation.InverseTransformPoint(transform.TransformPoint(handleBottomInObjectSpace));
            }
            else
            {
                LogUtils.LogWarning("Cannot access bow mesh, falling back to hardcoded bow anatomy data");
                restingStringTopInObjectSpace = transform.InverseTransformPoint(bowOrientation.TransformPoint(bowAnatomy.fallbackStringTop));
                restingStringBottomInObjectSpace = transform.InverseTransformPoint(bowOrientation.TransformPoint(bowAnatomy.fallbackStringBottom));
                handleTop = bowAnatomy.fallbackHandleTop;
                handleBottom = bowAnatomy.fallbackHandleBottom;
                gripLocalHalfWidth = bowAnatomy.fallbackHandleWidth * 0.5f;
                handleTopInObjectSpace = transform.InverseTransformVector(bowOrientation.TransformPoint(handleTop) - transform.position);
                handleBottomInObjectSpace = transform.InverseTransformVector(bowOrientation.TransformPoint(handleBottom) - transform.position);
            }

            createLimbBones();
            initializeStringPosition();
            GetComponent<MeshFilter>().mesh.vertices = verts;
            skinBones();
            createNewString();
        }

        private Material GetBowBendingMaterial(Material vanillaMaterial)
        {
            Material bowBendingMaterial = Instantiate(VRAssetManager.GetAsset<Material>("BowBendingMaterial"));
            bowBendingMaterial.color = vanillaMaterial.color;
            bowBendingMaterial.mainTexture = vanillaMaterial.mainTexture;
            bowBendingMaterial.SetTexture("_BumpMap", vanillaMaterial.GetTexture("_BumpMap"));
            bowBendingMaterial.SetTexture("_MetallicGlossMap", vanillaMaterial.GetTexture("_MetallicGlossMap"));
            bowBendingMaterial.SetTexture("_EmissionMap", vanillaMaterial.GetTexture("_EmissionMap"));

            // A higher render queue is needed to to prevent certain shadow artifacts when using vertex and fragment shaders. It is not necesssary for surface shaders.
            // meshRenderer.material.renderQueue = 3000;

            bowBendingMaterial.SetVector("_HandleVector", bowUpInObjectSpace);
            bowBendingMaterial.SetFloat("_HandleTopHeight", Vector3.Dot(handleTopInObjectSpace, bowUpInObjectSpace));
            bowBendingMaterial.SetFloat("_HandleBottomHeight", Vector3.Dot(handleBottomInObjectSpace, bowUpInObjectSpace));
            bowBendingMaterial.SetFloat("_SoftLimbHeight", bowAnatomy.softLimbHeight / transform.localScale.y);

            Vector3 stringTopToBottomDirection = (restingStringBottomInObjectSpace - restingStringTopInObjectSpace).normalized;
            bowBendingMaterial.SetVector("_StringTop", new Vector4(restingStringTopInObjectSpace.x, restingStringTopInObjectSpace.y, restingStringTopInObjectSpace.z, 1));
            bowBendingMaterial.SetVector("_StringTopToBottomDirection", new Vector4(stringTopToBottomDirection.x, stringTopToBottomDirection.y, stringTopToBottomDirection.z, 0));
            bowBendingMaterial.SetFloat("_StringLength", (restingStringTopInObjectSpace - restingStringBottomInObjectSpace).magnitude);
            bowBendingMaterial.SetFloat("_StringRadius", bowAnatomy.stringRadius);
            bowBendingMaterial.SetFloat("_StringNormalTolerance", 1f / 512f);

            return bowBendingMaterial;
        }

        private void skinBones()
        {
            Transform handleBone = new GameObject("BowHandleBone").transform;
            handleBone.parent = bowOrientation;
            handleBone.localRotation = Quaternion.identity;
            handleBone.localPosition = Vector3.Lerp(upperLimbBone.localPosition, lowerLimbBone.localPosition, 0);

            Transform[] bones = new Transform[3] { upperLimbBone, handleBone, lowerLimbBone };

            Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bindPoses[i] = bones[i].worldToLocalMatrix * transform.localToWorldMatrix;
            }

            MeshRenderer originalMeshRenderer = gameObject.GetComponent<MeshRenderer>();
            Material vanillaMaterial = originalMeshRenderer.material;
            if (useCustomShader)
            {
                try
                {
                    // Use custom shader to animate bow bending.
                    originalMeshRenderer.material = GetBowBendingMaterial(vanillaMaterial);
                }
                catch (Exception e)
                {
                    LogUtils.LogError("Bow bending material not found!");
                    useCustomShader = false;
                }
            }
            if (!useCustomShader && canAccessMesh)
            {
                // Use skinned mesh to animate bow bending.
                SkinnedMeshRenderer skinnedMeshRenderer = gameObject.AddComponent<SkinnedMeshRenderer>();
                Mesh mesh = gameObject.GetComponent<MeshFilter>().mesh;
                mesh.boneWeights = boneWeights;
                mesh.bindposes = bindPoses;
                skinnedMeshRenderer.bones = bones;
                skinnedMeshRenderer.sharedMesh = mesh;
                skinnedMeshRenderer.material = vanillaMaterial;
                skinnedMeshRenderer.forceMatrixRecalculationPerRender = true;
                // Destroy the original renderer since we will be using SkinnedMeshRenderer only.
                Destroy(originalMeshRenderer);
            }
        }

        /**
     * now we create a new string out of a linerenderer with 3 points, using the saved top and bottom points
     * and a new third one in the middle.
     */
        private void createNewString()
        {
            var lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.widthMultiplier = 0.006f;
            lineRenderer.positionCount = 3;
            updateStringRenderer();
            lineRenderer.material = Instantiate(VRAssetManager.GetAsset<Material>("StandardClone"));
            lineRenderer.material.color = new Color(0.703125f, 0.48828125f, 0.28515625f); // just a random brown color
        }

        private void createOutline()
        {
            outline = gameObject.AddComponent<Outline>();
            outline.OutlineColor = Color.red;
            outline.OutlineWidth = 10;
            outline.OutlineMode = Outline.Mode.OutlineVisible;
            outline.enabled = false;
        }

        /**
     * Need to use OnRenderObject instead of Update or LateUpdate,
     * because of VRIK Bone Updates happening in LateUpdate 
     */
        protected void OnRenderObject()
        {

            if (!initialized)
            {
                return;
            }

            if (!wasInitialized)
            {
                PostInit();
                wasInitialized = true;
            }

            if (VRPlayer.ShouldPauseMovement && gameObject.GetComponentInParent<Player>() == Player.m_localPlayer)
            {
                return;
            }

            if (pulling)
            {
                if (!wasPulling)
                {
                    timeBasedChargePercentage = 0;
                    wasPulling = true;
                }

                rotateBowOnPulling();
                pullString();
            }
            else if (wasPulling)
            {
                wasPulling = false;
                pullObj.transform.position = pullStart.position;
                bowOrientation.localRotation = adjustedRotation;
                transform.SetPositionAndRotation(bowTransformUpdater.position, bowTransformUpdater.rotation);
            }

            morphBow();
            updateStringRenderer();
        }

        private void rotateBowOnPulling()
        {
            if (OnlyUseDominantHand())
            {
                handleDominantHandAiming();
                return;
            }

            if (bowHandAiming)
            {
                return;
            }

            float realLifeHandDistance = bowOrientation.InverseTransformPoint(mainHand.position).magnitude;

            // The angle between the push direction and the arrow direction.
            double pushOffsetAngle = Math.Asin(VHVRConfig.ArrowRestElevation() / realLifeHandDistance);

            // Align the forward vector of the pushObj with the direction of the push force and determine its y-axis using the orientation of the bow hand.
            Vector3 pushDirection = pushObj.transform.position - mainHand.position;
            pushObj.transform.LookAt(pushObj.transform.position + pushDirection, worldUp: transform.parent.forward);

            // Assuming that the bow is perpendicular to the arrow, the angle between the y-axis of the bow and the y-axis of the pushObj should also be pushOffsetAngle.
            bowOrientation.rotation = pushObj.transform.rotation * Quaternion.AngleAxis((float)(-pushOffsetAngle * (180.0 / Math.PI)), Vector3.right);

            transform.SetPositionAndRotation(bowTransformUpdater.position, bowTransformUpdater.rotation);
        }

        private void handleDominantHandAiming()
        {
            var dominantHandPointer = VHVRConfig.LeftHanded() ? VRPlayer.leftPointer : VRPlayer.rightPointer;
            Vector3 aimingDirection = (dominantHandPointer.rayDirection * Vector3.forward).normalized;
            bowOrientation.LookAt(bowOrientation.position + aimingDirection, mainHand.up);
            bowOrientation.position =
                mainHand.position +
                bowOrientation.TransformVector(
                    new Vector3(0, -VHVRConfig.ArrowRestElevation(), getPullLengthRestriction(timeBasedChargePercentage)));
            transform.SetPositionAndRotation(bowTransformUpdater.position, bowTransformUpdater.rotation);
        }

        private void morphBow()
        {
            if (!canAccessMesh && !useCustomShader)
            {
                return;
            }
            float pullDelta = pullStart.localPosition.z - pullObj.transform.localPosition.z;
            // Just a heuristic and simplified approximation for the bend angle.
            float bendAngleDegrees = pullDelta <= 0 ? 0 : Mathf.Asin(Math.Min(1, pullDelta * 1.25f / stringLength)) * 180 / Mathf.PI;

            upperLimbBone.localRotation = Quaternion.Euler(-bendAngleDegrees, 0, 0);
            lowerLimbBone.localRotation = Quaternion.Euler(bendAngleDegrees, 0, 0);

            if (useCustomShader)
            {
                Quaternion upperLimbRotation = Quaternion.AngleAxis(-bendAngleDegrees, bowRightInObjectSpace);
                Quaternion lowerLimbRotation = Quaternion.AngleAxis(bendAngleDegrees, bowRightInObjectSpace);
                Matrix4x4 upperLimbTransform = Matrix4x4.TRS(handleTopInObjectSpace - upperLimbRotation * handleTopInObjectSpace, upperLimbRotation, Vector3.one);
                Matrix4x4 lowerLimbTransform = Matrix4x4.TRS(handleBottomInObjectSpace - lowerLimbRotation * handleBottomInObjectSpace, lowerLimbRotation, Vector3.one);
                gameObject.GetComponent<MeshRenderer>().material.SetMatrix("_UpperLimbTransform", upperLimbTransform);
                gameObject.GetComponent<MeshRenderer>().material.SetMatrix("_LowerLimbTransform", lowerLimbTransform);
            }
        }

        private void updateStringRenderer()
        {
            gameObject.GetComponent<LineRenderer>().SetPosition(0, stringTop.position);
            gameObject.GetComponent<LineRenderer>().SetPosition(1, pulling ? pullObj.transform.position : stringTop.position);
            gameObject.GetComponent<LineRenderer>().SetPosition(2, stringBottom.position);
        }

        private void pullString()
        {

            Vector3 pullPos = bowOrientation.InverseTransformPoint(mainHand.position);

            if (bowHandAiming)
            {
                pullPos.x = 0f;
                pullPos.y = VHVRConfig.ArrowRestElevation();
            }
            pullPos.z = Mathf.Clamp(pullPos.z, -getPullLengthRestriction(), -GetBraceHeight());

            pullObj.transform.localPosition = pullPos;
        }
    }
}
