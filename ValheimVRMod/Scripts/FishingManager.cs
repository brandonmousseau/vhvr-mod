using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;

namespace ValheimVRMod.Scripts
{
    public class FishingManager : SwingLaunchManager
    {
        private Transform rodTop;
        private int tickCounter;

        private float reelOffset;

        private FishingFloat fishingFloat;
        private GameObject reelParent;
        private GameObject reelWheel;
        private GameObject reelCrank;
        public bool reelGrabbed;
        private Vector3 handCenter = new Vector3(0, 0, -0.1f);
        private Vector3 reelStart;
        private float reeltimer;
        private float reelTolerance;
        private float savedRotation;
        private float totalRotation;

        private GameObject fishingTextParent;
        private Text fishingText;
        private GameObject baitTextParent;
        private Text baitText;

        public static bool isFishing;
        public static bool isPulling;
        public static GameObject fixedRodTop;

        private bool wasHooked;

        public static FishingManager instance;

        private void Awake()
        {
            reelOffset = VHVRConfig.LeftHanded()
                ? -0.08f
                : 0.08f;

            rodTop = transform.parent.Find("_RodTop");
            rodTop.transform.localPosition = new Vector3(0, -0.01f, 3.3f);
            // TODO: removed fixedRodTop since it is unused.
            fixedRodTop = new GameObject();
            instance = this;
            CreateReel();
            CreateText();
            UpdateBaitText();
        }

        private void CreateReel()
        {
            reelParent = new GameObject();
            reelParent.transform.SetParent(transform);
            reelParent.transform.rotation = transform.rotation;
            reelParent.transform.localPosition = new Vector3(0, 0.6f, -0.07f);

            reelWheel = Instantiate(VRAssetManager.GetAsset<GameObject>("Reel"));
            reelWheel.transform.SetParent(reelParent.transform);
            reelWheel.transform.localRotation = Quaternion.identity;
            reelWheel.transform.localPosition = new Vector3(0, 0, 0);

            reelCrank = Instantiate(VRAssetManager.GetAsset<GameObject>("Crank"));
            reelCrank.transform.SetParent(reelParent.transform);
            reelCrank.transform.localRotation = Quaternion.identity;

            reelCrank.transform.localPosition = new Vector3(reelOffset, 0.04f, 0);
        }

        private void CreateText()
        {
            fishingTextParent = new GameObject();
            var fishingSubparent = new GameObject();
            fishingSubparent.transform.SetParent(fishingTextParent.transform);
            fishingSubparent.transform.Rotate(0, 180, 0);
            var canvasText = fishingSubparent.AddComponent<Canvas>();
            canvasText.renderMode = RenderMode.WorldSpace;
            fishingText = fishingSubparent.AddComponent<Text>();
            fishingText.fontSize = 40;
            Font ArialFont = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
            fishingText.font = ArialFont;
            fishingText.horizontalOverflow = HorizontalWrapMode.Overflow;
            fishingText.verticalOverflow = VerticalWrapMode.Overflow;
            fishingText.alignment = TextAnchor.MiddleCenter;
            fishingText.enabled = true;
            fishingText.color = Color.yellow * 0.8f;
            fishingTextParent.transform.localScale *= 0.005f;

            baitTextParent = new GameObject();
            var baitSubParent = new GameObject();
            baitSubParent.transform.SetParent(baitTextParent.transform);
            baitSubParent.transform.Rotate(0, 180, 0);
            var canvasText2 = baitSubParent.AddComponent<Canvas>();
            canvasText2.renderMode = RenderMode.WorldSpace;
            baitText = baitSubParent.AddComponent<Text>();
            baitText.fontSize = 40;
            baitText.font = ArialFont;
            baitText.horizontalOverflow = HorizontalWrapMode.Overflow;
            baitText.verticalOverflow = VerticalWrapMode.Overflow;
            baitText.alignment = TextAnchor.MiddleCenter;
            baitText.enabled = true;
            baitText.color = Color.white * 0.8f;
            baitTextParent.transform.localScale *= 0.001f;

            baitTextParent.transform.SetParent(transform);
            baitTextParent.transform.localPosition = new Vector3(0, 0.65f, 0.03f);
            baitTextParent.transform.rotation = transform.rotation;
        }
        private void OnDestroy()
        {
            Destroy(reelCrank);
            Destroy(reelWheel);
            Destroy(reelParent);
            Destroy(fixedRodTop);
            Destroy(fishingTextParent);
            Destroy(baitTextParent);
        }

        private void Update()
        {
            foreach (FishingFloat instance in FishingFloat.GetAllInstances())
            {
                if (instance.GetOwner() == Player.m_localPlayer)
                {
                    fishingFloat = instance;
                    isFishing = true;
                    return;
                }
            }
            isFishing = false;
        }

        protected override void OnRenderObject()
        {
            fixedRodTop.transform.position = rodTop.position;
            UpdateBaitText();
            if (!reelGrabbed)
            {
                if (fishingFloat)
                    fishingFloat.m_pullLineSpeed = 1;
                isPulling = isFishing && dominantHandInputAction.GetState(VRPlayer.dominantHandInputSource);
            }

            if (fishingFloat)
            {
                fishingText.text = (Mathf.Round(fishingFloat.m_lineLength * 10) / 10) + "m";
                var posMod = new Vector3(fishingFloat.transform.position.x, Mathf.Max(fishingFloat.m_floating.m_waterLevel, fishingFloat.transform.position.y) + 0.5f, fishingFloat.transform.position.z);
                fishingTextParent.transform.position = posMod;
                fishingTextParent.transform.LookAt(CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform.position);
                fishingTextParent.SetActive(true);

                //Set color of the rodline according to stamina
                var stamina = Player.m_localPlayer.GetStamina();
                if (stamina <= 30)
                {
                    fishingFloat.m_rodLine.m_lineRenderer.material.color = Color.Lerp(Color.red, Color.yellow, stamina / 30);
                }
                else if (stamina <= 50)
                {
                    fishingFloat.m_rodLine.m_lineRenderer.material.color = Color.Lerp(Color.yellow, Color.white, (stamina - 30) / 20);
                }
                else
                {
                    fishingFloat.m_rodLine.m_lineRenderer.material.color = Color.white;
                }
                var fish = fishingFloat.GetCatch();
                if (wasHooked && !fish)
                {
                    fishingFloat.m_nview.Destroy();
                    wasHooked = false;
                    return;
                }
                else if (!wasHooked && fish)
                {
                    wasHooked = true;
                }
            }
            else
            {
                fishingTextParent.SetActive(false);
                wasHooked = false;
            }
            UpdateReel();
            base.OnRenderObject();
        }

        private void FixedUpdate()
        {
            tickCounter++;
            if (tickCounter < 5)
            {
                return;
            }
            tickCounter = 0;
            if (isFishing && fishingFloat && fishingFloat.GetCatch() && (int)(Time.fixedTime * 10) % 2 >= 1)
            {
                VRPlayer.dominantHand.hapticAction.Execute(0, 0.001f, 150, 0.7f, VRPlayer.dominantHandInputSource);
            }
        }

        protected override Vector3 GetProjectileSpawnPoint()
        {
            return rodTop.transform.position;
        }

        protected override bool ReleaseTriggerToAttack()
        {
            // We are unable to skip vanilla fishing throwing animation which delays bait casting.
            // By allowing the attack to start before the player release trigger,
            // we can reduce the delay between releasing the trigger and bait casting.
            return false;
        }

       
        private void UpdateReel()
        {
            var offHandCenter = VRPlayer.dominantHand.otherHand.transform.TransformPoint(handCenter);

            if (reelGrabbed)
            {
                var localHandPos = reelParent.transform.InverseTransformPoint(offHandCenter);
                reelCrank.transform.localPosition = (new Vector3(0, localHandPos.y, localHandPos.z).normalized * 0.04f) + (Vector3.right * reelOffset);
                if (reelStart == Vector3.zero)
                {
                    reelStart = reelCrank.transform.localPosition;
                }

                var angle = Vector3.SignedAngle(new Vector3(0, reelStart.y, reelStart.z), new Vector3(0, reelCrank.transform.localPosition.y, reelCrank.transform.localPosition.z), reelParent.transform.right);
                reeltimer += Time.deltaTime;

                if (Mathf.Abs(angle) + Mathf.Abs(savedRotation) >= 10)
                {
                    reelTolerance = 1;
                }
                else
                {
                    if (reelTolerance >= 0)
                        reelTolerance -= Time.deltaTime;
                }

                isPulling = isFishing && reelTolerance > 0;

                if (reeltimer >= 1)
                {
                    reeltimer = 0;
                    var rpm = ((angle + savedRotation) / 60);
                    var speed = Mathf.Max(0, Mathf.Min(2.5f, Mathf.Abs(rpm * 1.2f)));
                    var force = Mathf.Max(0.1f, Mathf.Min(0.5f, Mathf.Abs(rpm * 0.5f)));
                    if (fishingFloat)
                    {
                        fishingFloat.m_pullLineSpeed = speed;
                        Utils.Pull(fishingFloat.m_body, rodTop.transform.position, fishingFloat.m_lineLength, fishingFloat.m_moveForce * force, 0.5f, 0.3f, false, false, 1f);
                    }
                    reelStart = reelCrank.transform.localPosition;
                    savedRotation = 0;
                }
                else
                {
                    if (reelCrank.transform.localPosition != reelStart)
                    {
                        savedRotation += angle;
                        totalRotation += angle;
                        reelStart = reelCrank.transform.localPosition;
                    }
                }

                if (Mathf.RoundToInt(Mathf.Abs(totalRotation)) > 45)
                {
                    totalRotation = 0;
                    VRPlayer.dominantHand.otherHand.hapticAction.Execute(0, 0.002f, 150, 0.1f, VRPlayer.nonDominantHandInputSource);
                }

                if (!SteamVR_Actions.valheim_Grab.GetState(VRPlayer.nonDominantHandInputSource))
                {
                    reeltimer = 0;
                    reelStart = Vector3.zero;
                    reelGrabbed = false;
                    isPulling = false;
                    totalRotation = 0;
                }
            }
            else
            {
                if (SteamVR_Actions.valheim_Grab.GetState(VRPlayer.nonDominantHandInputSource) && !LocalWeaponWield.isCurrentlyTwoHanded())
                {
                    if (Vector3.Distance(offHandCenter, reelParent.transform.position) < 0.2f)
                    {
                        var handUp = VRPlayer.dominantHand.otherHand.transform.TransformDirection(0, -0.3f, -0.7f);
                        reelGrabbed = true;
                    }
                }
            }
        }

        public void TriggerVibrateFish(FishingFloat fishFloat)
        {
            if (fishingFloat != fishFloat)
            {
                return;
            }
            VRPlayer.dominantHand.hapticAction.Execute(0.4f, 0.7f, 100, 0.2f, VRPlayer.dominantHandInputSource);
        }

        private void UpdateBaitText()
        {
            var bait = Player.m_localPlayer.m_ammoItem;
            if (bait == null)
            {
                baitText.text = "-";
                return;
            }
            var baitCount = Player.m_localPlayer.m_inventory.CountItems(bait.m_shared.m_name).ToString();
            baitText.text = baitCount;
        }
    }
}
