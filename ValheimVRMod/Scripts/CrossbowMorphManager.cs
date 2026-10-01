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
        private Transform arrowHandTransform;
        private ItemDrop.ItemData weapon;
        private GameObject bolt;
        private GameObject boltAttach;
        private bool useBowBendingShader;
        private Vector3 bowForward;
        private Vector3 bowRight;
        private Vector3 bowUp;
        private Vector3 leverPivot;

        // The projectile prefab of a weapon that shoots its own rather than one loaded from its ammo, i. e. the
        // grappling hook's hook; null for the crossbows, which take theirs from the ammo instead.
        private GameObject ownProjectile;
        // Whether the weapon takes bolts from its ammo, which are shown on it and loaded by hand. The grappling
        // hook does not: it shoots its own projectile, so it is never loaded and firing is not gated on it.
        private bool usesBolts = true;
        // Whether ownProjectile is shown resting on the string whenever the weapon is unloaded. This is display
        // only - it is deliberately not hand loaded, and does not gate firing.
        private bool showsOwnProjectile;
        private bool weaponRequiresReload = true;
        private bool boltLoaded = false;
        public bool isBoltLoaded { get { return !usesBolts || boltLoaded; } }

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
        // If crossbow anatomy data is not available, fallback to the vanilla auto-reload logic. A weapon that doesn't
        // reload at all has nothing to pull either.
        public bool shouldAutoReload { get { return anatomy == null || !VHVRConfig.CrossbowManualReload() || !weaponRequiresReload; } }

        void Start()
        {
            instance = this;
            arrowHandTransform = VRPlayer.arrowHand.transform;
            boltAttach = new GameObject();
            boltAttach.transform.SetParent(arrowHandTransform, false);
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
            var item = Player.m_localPlayer.GetLeftItem();
            // The grappling hook shoots the hook itself rather than anything loaded from its ammo, so the hook is
            // what gets shown on the string. Its shot can be the secondary attack (see
            // CrossbowManager.IsPullingTrigger), so fall back to that attack's projectile.
            ownProjectile =
                !EquipScript.IsGrapplingHook(item) ? null :
                item.m_shared.m_attack.m_attackProjectile != null ? item.m_shared.m_attack.m_attackProjectile :
                item.m_shared.m_secondaryAttack?.m_attackProjectile;
            usesBolts = !EquipScript.IsGrapplingHook(item);
            showsOwnProjectile = ownProjectile != null;
            weaponRequiresReload = item.m_shared.m_attack.m_requiresReload;
            anatomy = CrossbowAnatomy.getAnatomy(item.m_shared.m_name);
            if (anatomy == null)
            {
                boltLoaded = true;
                return;
            }
            // Anatomy points are in this object's local space, so its mesh bounds are the reference for tuning them.
            var meshFilter = gameObject.GetComponent<MeshFilter>();
            LogUtils.LogDebug(
                "Crossbow anatomy for " + item.m_shared.m_name + " applied to " + gameObject.name +
                (meshFilter != null && meshFilter.sharedMesh != null ? ", local mesh bounds " + meshFilter.sharedMesh.bounds : ", no mesh"));
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
                Vector3 handOffsetFromPivot = transform.InverseTransformPoint(arrowHandTransform.position) - leverPivot;
                realLifeDrawPercentage =
                    Mathf.Clamp01(
                        0.5f -
                        Vector3.Dot(handOffsetFromPivot, bowForward) / Mathf.Max(Vector3.Dot(handOffsetFromPivot, bowUp), maxDrawDelta * 0.5f) * 0.5f);
            }

            MorphBow();
            UpdateStringAndLever();

            if (showsOwnProjectile)
            {
                UpdateOwnProjectile();
            }

            if (isPulling || shouldAutoReload || showsOwnProjectile)
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
            // Vanilla deactivates the unloaded object - the one this component lives on - once the weapon is
            // loaded, so OnRenderObject stops running and cannot hide the shown projectile itself. The projectile
            // hangs off the weapon root rather than this object (see attachBoltToCrossbow), so it would otherwise
            // stay on the string next to the hook that the loaded model already has.
            if (bolt != null && showsOwnProjectile)
            {
                bolt.SetActive(false);
            }

            if (isPulling)
            {
                VrikCreator.ResetHandConnectors();
                isPulling = false;
                if (Player.m_localPlayer.IsWeaponLoaded())
                {
                    // The unloaded crossbow object is set inactive upon successful weapon reload, which is a good point to provide haptic feedback.
                    VRPlayer.arrowHand.hapticAction.Execute(0, 0.2f, 100, 0.3f, VRPlayer.arrowHandInputSource);
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
            return !CrossbowManager.isCurrentlyTwoHanded() && anatomy != null && Vector3.Distance(arrowHandTransform.position, pullStart.position) <= MAX_NOCKING_DISTANCE;
        }

        private void UpdatePullStatus()
        {
            if (shouldAutoReload)
            {
                if (bolt == null)
                {
                    boltLoaded = createBolt();
                }
                return;
            }
            bool wasPulling = isPulling;
            isPulling =
                !Player.m_localPlayer.IsWeaponLoaded() &&
                SteamVR_Actions.valheim_Grab.GetState(VRPlayer.arrowHandInputSource) &&
                (wasPulling || IsHandClosePullStart());
            if (isPulling)
            {
                if (!Player.m_localPlayer.IsReloadActionQueued())
                {
                    Player.m_localPlayer.ResetLoadedWeapon();
                    Player.m_localPlayer.QueueReloadAction();
                }
                VrikCreator.GetLocalPlayerArrowHandConnector().position =
                    Vector3.Lerp(leverRenderer.GetPosition(1), leverRenderer.GetPosition(2), 0.5f);

                boltLoaded = bolt != null;
            }
            else if (wasPulling)
            {
                VrikCreator.ResetHandConnectors();
                if (!Player.m_localPlayer.IsWeaponLoaded())
                {
                    Player.m_localPlayer.CancelReloadAction();
                    boltAttach.transform.SetParent(arrowHandTransform, false);
                    boltAttach.transform.localPosition = Vector3.zero;
                    boltLoaded = false;
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

            Quaternion leftLimbRotation = Quaternion.AngleAxis(bendAngleDegrees, anatomy.limbBendAxis);
            Quaternion rightLimbRotation = Quaternion.AngleAxis(-bendAngleDegrees, anatomy.limbBendAxis);
            leftLimbBone.localRotation = leftLimbRotation;
            rightLimbBone.localRotation = rightLimbRotation;
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
            // Follow the actual resting string rather than assuming it lies along -X: a slanted vanilla string (e.g. the
            // grappling hook's) would otherwise drift outside the string radius and only partly get hidden.
            Vector3 stringTopToBottom = (anatomy.restingStringLeft - anatomy.restingStringRight).normalized;
            bowBendingMaterial.SetVector("_StringTopToBottomDirection", new Vector4(stringTopToBottom.x, stringTopToBottom.y, stringTopToBottom.z, 0));
            bowBendingMaterial.SetFloat("_StringLength", Vector3.Distance(anatomy.restingStringLeft, anatomy.restingStringRight));
            bowBendingMaterial.SetFloat("_StringRadius", anatomy.stringRadius);

            return bowBendingMaterial;
        }

        public void destroyBolt()
        {
            if (bolt != null)
            {
                ZNetView netView = bolt.GetComponent<ZNetView>();
                if (netView != null)
                {
                    netView.Destroy();
                }
                else
                {
                    Destroy(bolt);
                }
                // Destruction only takes effect at the end of the frame, so clear this now: the checks that
                // recreate the projectile test it for null within the same frame.
                bolt = null;
            }
            boltLoaded = false;
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
                    BhapticsTactsuit.PlaybackHaptics(
                        VRPlayer.isRightHandMainWeaponHand ?
                        "HolsterArrowRightShoulder" :
                        "HolsterArrowLeftShoulder");
                }
                return;
            }

            boltAttach.transform.SetParent(arrowHandTransform);
            boltAttach.transform.localRotation = Quaternion.identity;
            boltAttach.transform.localPosition = Vector3.zero;

            if (!createBolt())
            {
                return;
            }

            //bHaptics
            if (!BhapticsTactsuit.suitDisabled)
            {
                BhapticsTactsuit.PlaybackHaptics(
                    VRPlayer.isRightHandMainWeaponHand ?
                    "UnholsterArrowRightShoulder" :
                    "UnholsterArrowLeftShoulder");
            }
        }

        // The shown projectile only belongs on the string while the weapon is unloaded - at rest as well as
        // during the pull. Once loaded, the weapon model has the hook in it already, and while the chain is
        // deployed the real hook is out on the end of it.
        private void UpdateOwnProjectile()
        {
            var item = Player.m_localPlayer.GetLeftItem();
            bool show =
                !Player.m_localPlayer.IsWeaponLoaded() &&
                GrapplingPoint.m_localGrappler == null &&
                (item?.m_lastProjectile == null || !item.m_lastProjectile.activeSelf);
            if (show && bolt == null)
            {
                createBolt();
            }
            if (bolt != null)
            {
                bolt.SetActive(show);
            }
        }

        private bool createBolt()
        {
            GameObject projectilePrefab = ownProjectile;
            if (projectilePrefab == null)
            {
                ItemDrop.ItemData ammoItem = EquipScript.EquipAmmo();
                if (ammoItem == null)
                {
                    // Out of ammo
                    return false;
                }
                projectilePrefab = ammoItem.m_shared.m_attack.m_attackProjectile;
            }
            if (projectilePrefab == null)
            {
                return false;
            }

            bolt = Instantiate(projectilePrefab, boltAttach.transform);
            // we need to disable the Projectile Component, else the arrow will shoot out of the hands like a New Year rocket
            Projectile projectile = bolt.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.enabled = false;
            }
            // also Destroy the Trail, as this produces particles when moving with arrow in hand
            Destroy(findTrail(bolt.transform));
            Destroy(bolt.GetComponentInChildren<Collider>());
            var localRotation =
                projectilePrefab == ownProjectile ?
                    Quaternion.Euler(anatomy.ownProjectileRotation) :
                    Quaternion.identity;
            bolt.transform.localRotation = localRotation;
            // The anatomy distance is measured for the crossbows' own bolts and says nothing about a weapon's own
            // projectile, whose model is a different length and pivoted differently, so measure that one instead.
            bolt.transform.localPosition =
                localRotation *
                new Vector3(0, 0, anatomy.boltCenterToTailDistance);
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
                Vector3.Distance(arrowHandTransform.transform.position, transform.TransformPoint(anatomy.anchorPoint)) <= 0.2f)
            {
                boltAttach.transform.SetParent(transform.parent, false);
                boltAttach.transform.localPosition = anchorpoint;
                boltLoaded = true;
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
