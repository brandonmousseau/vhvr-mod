using System;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{

    // Manages the manual operation and the shape change of the cross bow during pulling.
    class CrossbowMorphManager : MonoBehaviour
    {
        public static CrossbowMorphManager instance { get; private set; }

        // The max distance allowed between the dominant hand and the nocking point to start pulling the string.
        private const float MAX_NOCKING_DISTANCE = 0.2f;
        private const float LEVER_HALF_WIDTH = 0.0625f;

        private bool initialized = false;
        private CrossbowAnatomy anatomy;
        private Transform leftLimbBone;
        private Transform rightLimbBone;
        private Transform stringLeft;
        private Transform stringRight;
        private Transform pullStart; // Where the hand should grab to start pulling the string.
        private LineRenderer stringRenderer;
        private LineRenderer leverRenderer;
        private float maxDrawDelta;
        private Transform mainHand;
        private ItemDrop.ItemData weapon;
        private GameObject bolt;
        private GameObject boltAttach;
        private bool useBowBendingShader;
        private Vector3 bowForward;
        private Vector3 bowRight;
        private Vector3 bowUp;
        private Vector3 leverPivot;

        public bool isBoltLoaded = false;

        // Note: we make draw length proportional to the square root of reload progress.
        private float vanillaDrawPercentageRestriction; // Draw percentage restriction due to the vanilla reload animation progress.
        private float realLifeDrawPercentage;
        private float drawPercentage
        {
            get
            {
                return shouldAutoReload ? vanillaDrawPercentageRestriction : Mathf.Min(vanillaDrawPercentageRestriction, realLifeDrawPercentage);
            }
        }

        private MeshRenderer hideableGlowMeshRenderer;

        public bool isPulling { get; private set; }
        public bool shouldAutoReload { get { return anatomy == null || !VHVRConfig.CrossbowManualReload(); } } // If crossbow anatomy data is not available, fallback to the vanilla auto-reload logic.

        void Start()
        {
            instance = this;
            mainHand = VRPlayer.dominantHand.transform;
            boltAttach = new GameObject();
            boltAttach.transform.SetParent(mainHand, false);
            weapon = Player.m_localPlayer.GetLeftItem();
        }

        public void UpdateWeaponLoading(Player player, float dt)
        {
            if (player != Player.m_localPlayer || anatomy == null)
            {
                return;
            }

            Player.MinorActionData action = GetReloadAction(player);
            vanillaDrawPercentageRestriction = action == null ? 0 : ReloadPercentageToDrawPercentage(action.m_time / action.m_duration);
            if (action != null && !shouldAutoReload)
            {
                action.m_time = Mathf.Clamp(action.m_time, 0, DrawPercentageToReloadPercentage(realLifeDrawPercentage) * action.m_duration);
            }
        }

        void Awake()
        {
            anatomy = CrossbowAnatomy.getAnatomy(Player.m_localPlayer.GetLeftItem().m_shared.m_name);
            if (anatomy == null)
            {
                isBoltLoaded = true;
                return;
            }
            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            try
            {
                meshRenderer.material = GetBowBendingMaterial(meshRenderer.material);
                useBowBendingShader = true;
            }
            catch (Exception e)
            {
                LogUtils.LogError("Bow bending material not found!");
                useBowBendingShader = false;
            }
            createBones();
            createNewStringAndLever();
            UpdateStringAndLever();
            hideableGlowMeshRenderer = WeaponUtils.GetHideableBowGlowMeshRenderer(transform, Player.m_localPlayer.GetLeftItem().m_shared.m_name);
            bowForward = (anatomy.restingNockingPoint - anatomy.anchorPoint).normalized;
            bowRight = (anatomy.hardLimbRight - anatomy.hardLimbLeft).normalized;
            bowUp = Vector3.Cross(bowForward, bowRight);
            leverPivot = Vector3.Lerp(anatomy.restingNockingPoint, anatomy.anchorPoint, 0.5f) - bowUp * maxDrawDelta * 0.5f;
            initialized = true;
        }

        void OnRenderObject()
        {
            if (!initialized)
            {
                return;
            }

            UpdatePullStatus();

            if (Player.m_localPlayer.IsWeaponLoaded())
            {
                realLifeDrawPercentage = 1;
            }
            else if (!isPulling)
            {
                realLifeDrawPercentage = 0;
            }
            else
            {
                Vector3 handOffsetFromPivot = transform.InverseTransformPoint(mainHand.position) - leverPivot;
                realLifeDrawPercentage =
                    Mathf.Clamp01(
                        0.5f -
                        Vector3.Dot(handOffsetFromPivot, bowForward) / Mathf.Max(Vector3.Dot(handOffsetFromPivot, bowUp), maxDrawDelta * 0.5f) * 0.5f);
            }

            MorphBow();
            UpdateStringAndLever();

            if (isPulling || shouldAutoReload)
            {
                // We use string position to calcualte bolt position so it can only be updated after updating the string.
                attachBoltToCrossbow();
            }

            if (hideableGlowMeshRenderer)
            {
                hideableGlowMeshRenderer.enabled = !isPulling;
            }
        }

        void OnDisable()
        {
            if (isPulling)
            {
                VrikCreator.ResetHandConnectors();
                isPulling = false;
                if (Player.m_localPlayer.IsWeaponLoaded())
                {
                    // The unloaded crossbow object is set inactive upon successful weapon reload, which is a good point to provide haptic feedback.
                    VRPlayer.dominantHand.hapticAction.Execute(0, 0.2f, 100, 0.3f, VRPlayer.dominantHandInputSource);
                }
            }
        }

        void OnDestroy()
        {
            if (!initialized)
            {
                return;
            }
            Destroy(leftLimbBone.gameObject);
            Destroy(rightLimbBone.gameObject);
            Destroy(stringLeft.gameObject);
            Destroy(stringRight.gameObject);
            Destroy(pullStart.gameObject);
            destroyBolt();
            Destroy(boltAttach);
        }

        private Player.MinorActionData GetReloadAction(Player player)
        {
            if (!player.IsReloadActionQueued())
            {
                return null;
            }
            foreach (Player.MinorActionData action in player.m_actionQueue)
            {
                if (action.m_type == Player.MinorActionData.ActionType.Reload)
                {
                    return action;
                }
            }
            return null;
        }

        private static float ReloadPercentageToDrawPercentage(float reloadPercentage)
        {
            return Mathf.Sqrt(Mathf.Max(reloadPercentage, 0));
        }

        private static float DrawPercentageToReloadPercentage(float drawPercentage)
        {
            return Mathf.Max(drawPercentage * drawPercentage, 0);
        }

        private void createBones()
        {
            leftLimbBone = new GameObject().transform;
            leftLimbBone.parent = transform;
            leftLimbBone.localPosition = anatomy.hardLimbLeft;
            leftLimbBone.rotation = Quaternion.identity;

            rightLimbBone = new GameObject().transform;
            rightLimbBone.parent = transform;
            rightLimbBone.localPosition = anatomy.hardLimbRight;
            rightLimbBone.rotation = Quaternion.identity;
        }

        private void createNewStringAndLever()
        {
            stringLeft = new GameObject().transform;
            stringLeft.parent = transform;
            stringLeft.localPosition = anatomy.restingStringLeft;

            stringRight = new GameObject().transform;
            stringRight.parent = transform;
            stringRight.localPosition = anatomy.restingStringRight;

            pullStart = new GameObject().transform;
            pullStart.parent = transform;
            pullStart.localPosition = anatomy.restingNockingPoint;

            maxDrawDelta = (anatomy.anchorPoint - anatomy.restingNockingPoint).magnitude;

            stringRenderer = gameObject.AddComponent<LineRenderer>();
            stringRenderer.useWorldSpace = true;
            stringRenderer.widthMultiplier = 0.006f;
            stringRenderer.positionCount = 4;
            stringRenderer.material = Instantiate(VRAssetManager.GetAsset<Material>("StandardClone"));
            stringRenderer.material.color = new Color(0.4f, 0.33f, 0.31f);
            stringRenderer.material.SetFloat("_Glossiness", 0);
            stringRenderer.material.SetFloat("_Smoothness", 0);
            stringRenderer.material.SetFloat("_Metallic", 0);

            leverRenderer = new GameObject().AddComponent<LineRenderer>();
            leverRenderer.transform.parent = transform;
            leverRenderer.useWorldSpace = true;
            leverRenderer.widthMultiplier = 0.01f;
            leverRenderer.positionCount = 4;
            leverRenderer.material = Instantiate(VRAssetManager.GetAsset<Material>("StandardClone"));
            leverRenderer.material.color = new Color(0.6f, 0.6f, 0.6f);
            leverRenderer.material.SetFloat("_Glossiness", 0);
            leverRenderer.material.SetFloat("_Smoothness", 0);
            leverRenderer.material.SetFloat("_Metallic", 0);
        }

        public bool IsHandClosePullStart()
        {
            return !CrossbowManager.isCurrentlyTwoHanded() && anatomy != null && Vector3.Distance(mainHand.position, pullStart.position) <= MAX_NOCKING_DISTANCE;
        }

        private void UpdatePullStatus()
        {
            if (shouldAutoReload)
            {
                if (bolt == null)
                {
                    isBoltLoaded = createBolt();
                }
                return;
            }
            bool wasPulling = isPulling;
            isPulling =
                !Player.m_localPlayer.IsWeaponLoaded() &&
                SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource) &&
                (wasPulling || IsHandClosePullStart());
            if (isPulling)
            {
                if (!Player.m_localPlayer.IsReloadActionQueued())
                {
                    Player.m_localPlayer.ResetLoadedWeapon();
                    Player.m_localPlayer.QueueReloadAction();
                }
                VrikCreator.GetLocalPlayerDominantHandConnector().position =
                    Vector3.Lerp(leverRenderer.GetPosition(1), leverRenderer.GetPosition(2), 0.5f);

                isBoltLoaded = bolt != null;
            }
            else if (wasPulling)
            {
                VrikCreator.ResetHandConnectors();
                if (!Player.m_localPlayer.IsWeaponLoaded())
                {
                    Player.m_localPlayer.CancelReloadAction();
                    boltAttach.transform.SetParent(mainHand, false);
                    boltAttach.transform.localPosition = Vector3.zero;
                    isBoltLoaded = false;
                }
            }
        }

        private void UpdateStringAndLever()
        {
            Vector3 nock = transform.TransformPoint(Vector3.Lerp(anatomy.restingNockingPoint, anatomy.anchorPoint, drawPercentage));
            Vector3 lateralOffset = transform.TransformDirection(bowRight) * LEVER_HALF_WIDTH;
            stringRenderer.SetPosition(0, stringLeft.position);
            stringRenderer.SetPosition(1, nock - lateralOffset);
            stringRenderer.SetPosition(2, nock + lateralOffset);
            stringRenderer.SetPosition(3, stringRight.position);

            if (!isPulling)
            {
                leverRenderer.enabled = false;
                return;
            }

            Vector3 globalLeverPivot = transform.TransformPoint(leverPivot);
            Vector3 leverHandle = (nock - globalLeverPivot).normalized * maxDrawDelta + globalLeverPivot;
            
            leverRenderer.SetPosition(0, globalLeverPivot + lateralOffset);
            leverRenderer.SetPosition(1, leverHandle + lateralOffset);
            leverRenderer.SetPosition(2, leverHandle - lateralOffset);
            leverRenderer.SetPosition(3, globalLeverPivot - lateralOffset);

            leverRenderer.enabled = true;
        }

        private void MorphBow()
        {
            if (!useBowBendingShader)
            {
                return;
            }

            // Just a heuristic and simplified approximation for the bend angle.
            float bendAngleDegrees = Mathf.Asin(drawPercentage * Mathf.Sin(anatomy.maxBendAngleRadians)) * 180 / Mathf.PI;

            if (bendAngleDegrees > 0)
            {
                if (stringLeft.parent != leftLimbBone)
                {
                    stringLeft.SetParent(leftLimbBone, true);
                }
                if (stringRight.parent != rightLimbBone)
                {
                    stringRight.SetParent(rightLimbBone, true);
                }
            }

            leftLimbBone.localRotation = Quaternion.Euler(0, 0, bendAngleDegrees);
            rightLimbBone.localRotation = Quaternion.Euler(0, 0, -bendAngleDegrees);
            Quaternion leftLimbRotation = Quaternion.AngleAxis(bendAngleDegrees, Vector3.forward);
            Quaternion rightLimbRotation = Quaternion.AngleAxis(-bendAngleDegrees, Vector3.forward);
            Matrix4x4 leftLimbTransform = Matrix4x4.TRS(anatomy.hardLimbLeft - leftLimbRotation * anatomy.hardLimbLeft, leftLimbRotation, Vector3.one);
            Matrix4x4 rightLimbTransform = Matrix4x4.TRS(anatomy.hardLimbRight - rightLimbRotation * anatomy.hardLimbRight, rightLimbRotation, Vector3.one);
            gameObject.GetComponent<MeshRenderer>().material.SetMatrix("_UpperLimbTransform", rightLimbTransform);
            gameObject.GetComponent<MeshRenderer>().material.SetMatrix("_LowerLimbTransform", leftLimbTransform);
        }

        private Material GetBowBendingMaterial(Material vanillaMaterial)
        {
            // TODO: Consider share this method with BowManager.
            Material bowBendingMaterial = Instantiate(VRAssetManager.GetAsset<Material>("BowBendingMaterial"));
            bowBendingMaterial.color = vanillaMaterial.color;
            bowBendingMaterial.mainTexture = vanillaMaterial.mainTexture;
            bowBendingMaterial.SetTexture("_BumpMap", vanillaMaterial.GetTexture("_BumpMap"));
            bowBendingMaterial.SetTexture("_MetallicGlossMap", vanillaMaterial.GetTexture("_MetallicGlossMap"));
            bowBendingMaterial.SetTexture("_EmissionMap", vanillaMaterial.GetTexture("_EmissionMap"));

            // A higher render queue is needed to to prevent certain shadow artifacts when using vertex and fragment shaders. It is not necesssary for surface shaders.
            // meshRenderer.material.renderQueue = 3000;

            bowBendingMaterial.SetVector("_HandleVector", Vector3.right);
            bowBendingMaterial.SetFloat("_HandleTopHeight", anatomy.hardLimbRight.x);
            bowBendingMaterial.SetFloat("_HandleBottomHeight", anatomy.hardLimbLeft.x);
            bowBendingMaterial.SetFloat("_SoftLimbHeight", anatomy.softLimbHeight);

            bowBendingMaterial.SetVector("_StringTop", new Vector4(anatomy.restingStringRight.x, anatomy.restingStringRight.y, anatomy.restingStringRight.z, 1));
            bowBendingMaterial.SetVector("_StringTopToBottomDirection", new Vector4(-1, 0, 0, 0));
            bowBendingMaterial.SetFloat("_StringLength", Vector3.Distance(anatomy.restingStringLeft, anatomy.restingStringRight));
            bowBendingMaterial.SetFloat("_StringRadius", anatomy.stringRadius);

            return bowBendingMaterial;
        }

        public void destroyBolt()
        {
            if (bolt != null)
            {
                bolt.GetComponent<ZNetView>().Destroy();
            }
            isBoltLoaded = false;
        }

        public void toggleBolt()
        {

            if (isBoltLoaded || shouldAutoReload)
            {
                return;
            }

            if (bolt != null)
            {
                destroyBolt();
                //bHaptics
                if (!BhapticsTactsuit.suitDisabled)
                {
                    BhapticsTactsuit.PlaybackHaptics(VHVRConfig.LeftHanded() ?
                         "HolsterArrowLeftShoulder" : "HolsterArrowRightShoulder");
                }
                return;
            }

            boltAttach.transform.SetParent(mainHand);
            boltAttach.transform.localRotation = Quaternion.identity;
            boltAttach.transform.localPosition = Vector3.zero;

            if (!createBolt())
            {
                return;
            }

            //bHaptics
            if (!BhapticsTactsuit.suitDisabled)
            {
                BhapticsTactsuit.PlaybackHaptics(VHVRConfig.LeftHanded() ?
                    "UnholsterArrowLeftShoulder" : "UnholsterArrowRightShoulder");
            }
        }

        private bool createBolt()
        {
            ItemDrop.ItemData ammoItem = EquipScript.equipAmmo();
            if (ammoItem == null)
            {
                // Out of ammo
                return false;
            }

            bolt = Instantiate(ammoItem.m_shared.m_attack.m_attackProjectile, boltAttach.transform);
            // we need to disable the Projectile Component, else the arrow will shoot out of the hands like a New Year rocket
            bolt.GetComponent<Projectile>().enabled = false;
            // also Destroy the Trail, as this produces particles when moving with arrow in hand
            Destroy(findTrail(bolt.transform));
            Destroy(bolt.GetComponentInChildren<Collider>());
            bolt.transform.localRotation = Quaternion.identity;
            bolt.transform.localPosition = new Vector3(0, 0, anatomy.boltCenterToTailDistance);
            foreach (ParticleSystem particleSystem in bolt.GetComponentsInChildren<ParticleSystem>())
            {
                particleSystem.transform.localScale *= VHVRConfig.ArrowParticleSize();
            }
            boltAttach.transform.localRotation = Quaternion.identity;
            boltAttach.transform.localPosition = Vector3.zero;

            return true;
        }

        private GameObject findTrail(Transform transform)
        {

            foreach (ParticleSystem p in transform.GetComponentsInChildren<ParticleSystem>())
            {
                var go = p.gameObject;
                if (go.name == "trail")
                {
                    return go;
                }
            }

            return null;
        }

        public bool isHoldingBolt()
        {
            return bolt != null && !isBoltLoaded;
        }

        public void loadBoltIfBoltInHandIsNearAnchor()
        {
            var anchorpoint = new Vector3(0, 0.082f, -0.29f);

            if (
                !CrossbowManager.isCurrentlyTwoHanded() &&
                isHoldingBolt() &&
                Player.m_localPlayer.IsWeaponLoaded() &&
                Vector3.Distance(mainHand.transform.position, transform.TransformPoint(anatomy.anchorPoint)) <= 0.2f)
            {
                boltAttach.transform.SetParent(transform.parent, false);
                boltAttach.transform.localPosition = anchorpoint;
                isBoltLoaded = true;
            }
        }

        private void attachBoltToCrossbow()
        {
            boltAttach.transform.SetParent(transform.parent);
            boltAttach.transform.position = Vector3.Lerp(stringRenderer.GetPosition(1), stringRenderer.GetPosition(2), 0.5f);
            boltAttach.transform.localRotation = Quaternion.identity;
        }
    }
}
