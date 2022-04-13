using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
using Valve.VR.InteractionSystem;
using UnityEngine.UI;

namespace ValheimVRMod.Scripts {
    public class FishingManager : MonoBehaviour {
        private const int MAX_SNAPSHOTS = 7;
        private const int MIN_SNAPSHOTSCHECK = 3;
        private const float MIN_DISTANCE = 0.2f;
        private static float maxDist = 1.0f;
        private Transform rodTop;
        private int tickCounter;
        private List<Vector3> snapshots = new List<Vector3>();
        private bool preparingThrow;

        private SteamVR_Input_Sources inputSource;
        private SteamVR_Action_Boolean inputAction;
        private Hand inputHand;
        private SteamVR_Input_Sources offHandSource;
        private Hand offHand;
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

        public static float attackDrawPercentage;
        public static Vector3 spawnPoint;
        public static Vector3 aimDir;
        public static bool isThrowing;
        public static bool isFishing;
        public static bool isPulling;
        public static GameObject fixedRodTop;

        public static FishingManager instance;

        private void Awake() {

            inputSource = VHVRConfig.LeftHanded()
                ? SteamVR_Input_Sources.LeftHand
                : SteamVR_Input_Sources.RightHand;
            inputAction = VHVRConfig.LeftHanded()
                ? SteamVR_Actions.valheim_UseLeft
                : SteamVR_Actions.valheim_Use;
            inputHand = VHVRConfig.LeftHanded()
                ? VRPlayer.leftHand
                : VRPlayer.rightHand;

            offHandSource = VHVRConfig.LeftHanded()
                ? SteamVR_Input_Sources.RightHand
                : SteamVR_Input_Sources.LeftHand;
            offHand = VHVRConfig.LeftHanded()
                ? VRPlayer.rightHand
                : VRPlayer.leftHand;

            reelOffset = VHVRConfig.LeftHanded()
                ? -0.08f
                : 0.08f;

            rodTop = transform.parent.Find("_RodTop");
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
            var rectTrans = fishingText.GetComponent<RectTransform>();
            rectTrans.localPosition = Vector3.up*-2;
            rectTrans.sizeDelta = new Vector2(400, 100);
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
            var rectTrans2 = fishingText.GetComponent<RectTransform>();
            rectTrans2.localPosition = Vector3.up * -2;
            rectTrans2.sizeDelta = new Vector2(400, 100);
            baitTextParent.transform.localScale *= 0.001f;

            baitTextParent.transform.SetParent(transform);
            baitTextParent.transform.localPosition = new Vector3(0, 0.65f, 0.03f);
            baitTextParent.transform.rotation = transform.rotation;
        }
        private void OnDestroy() {
            Destroy(reelCrank);
            Destroy(reelWheel);
            Destroy(reelParent);
            Destroy(fixedRodTop);
            Destroy(fishingTextParent);
            Destroy(baitTextParent);
        }

        private void Update() {
            foreach (FishingFloat instance in FishingFloat.GetAllInstances()) {
                if (instance.GetOwner() == Player.m_localPlayer) {
                    fishingFloat = instance;
                    isFishing = true;
                    return;
                }
            }
            isFishing = false;
        }
        private void OnRenderObject() {

            fixedRodTop.transform.position = rodTop.position;
            UpdateBaitText();
            if (!reelGrabbed)
            {
                if (fishingFloat)
                fishingFloat.m_pullLineSpeed = 1;
                isPulling = isFishing && inputAction.GetState(inputSource);
            }

            if (fishingFloat)
            {
                fishingText.text = (Mathf.Round(fishingFloat.m_lineLength*10)/10) + "m" ;
                var posMod = new Vector3(fishingFloat.transform.position.x, Mathf.Max(fishingFloat.m_floating.m_waterLevel, fishingFloat.transform.position.y) + 0.5f, fishingFloat.transform.position.z);
                fishingTextParent.transform.position = posMod;
                fishingTextParent.transform.LookAt(CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform.position);
                fishingTextParent.SetActive(true);

                //Set color of the rodline according to stamina
                var stamina = Player.m_localPlayer.GetStamina();
                if (stamina <= 30)
                {
                    fishingFloat.m_rodLine.m_lineRenderer.material.color = Color.Lerp(Color.red, Color.yellow, stamina/30);
                }
                else if (stamina <= 50)
                {
                    fishingFloat.m_rodLine.m_lineRenderer.material.color = Color.Lerp(Color.yellow, Color.white, (stamina - 30)/20);
                }
                else
                {
                    fishingFloat.m_rodLine.m_lineRenderer.material.color = Color.white;
                }
            }
            else
            {
                fishingTextParent.SetActive(false);
            }
            UpdateReel();
            if (!isFishing && inputAction.GetStateDown(inputSource)) {
                preparingThrow = true;
            }

            if (!inputAction.GetStateUp(inputSource)) {
                return;
            }
            if (isFishing || isThrowing || !preparingThrow) {
                return;
            }
            if (snapshots.Count < MIN_SNAPSHOTSCHECK) {
                return;
            }

            spawnPoint = rodTop.position;
            var dist = 0.0f;
            Vector3 posEnd = fixedRodTop.transform.position;
            Vector3 posStart = fixedRodTop.transform.position;

            foreach (Vector3 snapshot in snapshots) {
                var curDist = Vector3.Distance(snapshot, posEnd);
                if (curDist > dist) {
                    dist = curDist;
                    posStart = snapshot;
                }
            }

            aimDir = (posEnd - posStart).normalized;
            aimDir = Quaternion.AngleAxis(-30, Vector3.Cross(Vector3.up, aimDir)) * aimDir;
            attackDrawPercentage = Vector3.Distance(snapshots[snapshots.Count - 1], snapshots[snapshots.Count - 2]) / maxDist;

            if (Vector3.Distance(posEnd, posStart) > MIN_DISTANCE) {
                isThrowing = true;
                preparingThrow = false; 
            }
        }

        private void FixedUpdate() {
            tickCounter++;
            if (tickCounter < 5) {
                return;
            }
            snapshots.Add(fixedRodTop.transform.position);
            if (snapshots.Count > MAX_SNAPSHOTS) {
                snapshots.RemoveAt(0);
            }
            tickCounter = 0;
            if (isFishing && fishingFloat.GetCatch() && (int)(Time.fixedTime * 10) % 2 >= 1) {
                inputHand.hapticAction.Execute(0, 0.001f, 150, 0.7f, inputSource);
            }
        }

        private void UpdateReel()
        {
            var offHandCenter = offHand.transform.TransformPoint(handCenter);

            if (reelGrabbed)
            {
                var localHandPos = reelParent.transform.InverseTransformPoint(offHandCenter);
                reelCrank.transform.localPosition = (new Vector3(0, localHandPos.y, localHandPos.z).normalized * 0.04f) + (Vector3.right * reelOffset);
                if (reelStart == Vector3.zero)
                {
                    reelStart = reelCrank.transform.localPosition;
                }

                var angle = Vector3.SignedAngle(new Vector3(0, reelStart.y, reelStart.z), new Vector3(0, reelCrank.transform.localPosition.y, reelCrank.transform.localPosition.z),reelParent.transform.right);
                reeltimer += Time.deltaTime;

                if(Mathf.Abs(angle) + Mathf.Abs(savedRotation) >= 10)
                {
                    reelTolerance = 1;
                }
                else
                {
                    if(reelTolerance>=0)
                        reelTolerance -= Time.deltaTime;
                }

                isPulling = isFishing && reelTolerance > 0;

                if (reeltimer>=1)
                {
                    reeltimer = 0;
                    var rpm = ((angle + savedRotation)/60);
                    var speed = Mathf.Max(0, Mathf.Min(2.5f, Mathf.Abs(rpm * 1.2f)));
                    if (fishingFloat)
                    {
                        fishingFloat.m_pullLineSpeed = speed;
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
                    offHand.hapticAction.Execute(0, 0.002f, 150, 0.1f, offHandSource);
                }

                if (!SteamVR_Actions.valheim_Grab.GetState(offHandSource))
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
                if (SteamVR_Actions.valheim_Grab.GetState(offHandSource) && !WeaponWield.isCurrentlyTwoHanded()) 
                {
                    if (Vector3.Distance(offHandCenter, reelParent.transform.position) < 0.2f)
                    {
                        var handUp = offHand.transform.TransformDirection(0, -0.3f, -0.7f);
                        if (Mathf.Abs(Vector3.Dot(handUp, reelParent.transform.right)) > 0.6f)
                        {
                            reelGrabbed = true;
                        }
                    }
                }
            }
        }

        public void TriggerVibrateFish(FishingFloat fishFloat)
        {
            if(fishingFloat != fishFloat)
            {
                return;
            }
            inputHand.hapticAction.Execute(0.4f, 0.7f, 100, 0.2f, inputSource);
        }

        private void UpdateBaitText()
        {
            var baitCount = Player.m_localPlayer.m_inventory.CountItems("$item_fishingbait").ToString();
            baitText.text = baitCount;
        }
    }
}