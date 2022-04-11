using System.Collections.Generic;
using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;
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
        private FishingFloat fishingFloat;
        private GameObject reelParent;
        private GameObject reelWheel;
        private GameObject reelCrank;
        private bool reelGrabbed;
        private Vector3 handCenter = new Vector3(0, 0, -0.1f);
        private Vector3 reelStart;
        private float reeltimer;
        private float reelTolerance;
        private float savedRotation;
        private float totalRotation;

        private GameObject fishingTextParent;
        private Text fishingText;

        public static float attackDrawPercentage;
        public static Vector3 spawnPoint;
        public static Vector3 aimDir;
        public static bool isThrowing;
        public static bool isFishing;
        public static bool isPulling;
        public static GameObject fixedRodTop;

        public static FishingManager instance;

        private void Awake() {
            rodTop = transform.parent.Find("_RodTop");
            fixedRodTop = new GameObject();
            instance = this;
            CreateReel();
            CreateText();
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
            var offset = VHVRConfig.LeftHanded()
                ? -0.08f
                : 0.08f;
            reelCrank.transform.localPosition = new Vector3(offset, 0.04f, 0);
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
        }
        private void OnDestroy() {
            Destroy(reelCrank);
            Destroy(reelWheel);
            Destroy(reelParent);
            Destroy(fixedRodTop);
            Destroy(fishingTextParent);
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
            var inputSource = VHVRConfig.LeftHanded()
                ? SteamVR_Input_Sources.LeftHand
                : SteamVR_Input_Sources.RightHand;
            var inputAction = VHVRConfig.LeftHanded()
                ? SteamVR_Actions.valheim_UseLeft
                : SteamVR_Actions.valheim_Use;

            fixedRodTop.transform.position = rodTop.position;

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
            attackDrawPercentage = Vector3.Distance(snapshots[snapshots.Count - 1], snapshots[snapshots.Count - 2]) /
                                   maxDist;

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

            var inputSource = VHVRConfig.LeftHanded()
                ? SteamVR_Input_Sources.LeftHand
                : SteamVR_Input_Sources.RightHand;

            var inputHand = VHVRConfig.LeftHanded()
                ? VRPlayer.leftHand
                : VRPlayer.rightHand;

            if (isFishing && fishingFloat.GetCatch() && (int)(Time.fixedTime * 10) % 2 >= 1) {
                inputHand.hapticAction.Execute(0, 0.001f, 150, 0.7f, inputSource);
            }
        }

        private void UpdateReel()
        {
            var inputCenter = VHVRConfig.LeftHanded()
                ? VRPlayer.rightHand.transform.TransformPoint(handCenter)
                : VRPlayer.leftHand.transform.TransformPoint(handCenter);
            var inputSource = VHVRConfig.LeftHanded()
                ? SteamVR_Input_Sources.RightHand
                : SteamVR_Input_Sources.LeftHand;
            var inputHand = VHVRConfig.LeftHanded()
                ? VRPlayer.rightHand
                : VRPlayer.leftHand;
            var offset = VHVRConfig.LeftHanded()
                ? -0.08f
                : 0.08f;


            if (reelGrabbed)
            {
                var localHandPos = reelParent.transform.InverseTransformPoint(inputCenter);
                reelCrank.transform.localPosition = (new Vector3(0, localHandPos.y, localHandPos.z).normalized * 0.04f) + (Vector3.right * offset);
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
                    if (fishingFloat)
                    {
                        fishingFloat.m_pullLineSpeed = Mathf.Max(0, Mathf.Min(2.5f, Mathf.Abs(rpm*1.2f)));
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
                    inputHand.hapticAction.Execute(0, 0.002f, 150, 0.1f, inputSource);
                }

                if (!SteamVR_Actions.valheim_Grab.GetState(inputSource))
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
                if (SteamVR_Actions.valheim_Grab.GetState(inputSource))
                {
                    if (Vector3.Distance(inputCenter, reelParent.transform.position) < 0.2f)
                    {
                        var handUp = inputHand.transform.TransformDirection(0, -0.3f, -0.7f);
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
            var inputSource = VHVRConfig.LeftHanded()
               ? SteamVR_Input_Sources.LeftHand
               : SteamVR_Input_Sources.RightHand;

            var inputHand = VHVRConfig.LeftHanded()
                ? VRPlayer.leftHand
                : VRPlayer.rightHand;

            inputHand.hapticAction.Execute(0.4f, 0.7f, 100, 0.2f, inputSource);
        }
    }
}