using UnityEngine;
using UnityEngine.UI;
using ValheimVRMod.Utilities;
using HarmonyLib;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
 
    // Since I separated the crosshair and the placement mode controls, when
    // in placement mode, it can be hard to know where the current position of
    // the cursor is when there is no placement ghost. This puts a hammer icon
    // in the place of the placement ghost to help guide the player on where
    // the current cursor position is.
    class RepairModePositionIndicator
    {

        private static RepairModePositionIndicator _instance;
        public static RepairModePositionIndicator instance { get
            {
                if (_instance == null || _hammerCanvasRoot == null)
                {
                    _instance = new RepairModePositionIndicator();
                }
                return _instance;
            } }

        private static GameObject _hammerCanvasRoot;
        private GameObject _hammerImageParent;
        private Image _hammerImage;
        private Canvas _hammerCanvas;
        private Camera _hammerCam;

        private RepairModePositionIndicator()
        {
            Texture2D hammerTex = loadHammerTexture();
            if (hammerTex == null)
            {
                return;
            }
            if (!ensureHammerCam())
            {
                return;
            }
            _hammerCanvasRoot = new GameObject("RepairModeHammerIndicatorRoot");
            GameObject.DontDestroyOnLoad(_hammerCanvasRoot);
            _hammerCanvasRoot.layer = LayerUtils.getWorldspaceUiLayer();
            _hammerCanvas = _hammerCanvasRoot.AddComponent<Canvas>();
            _hammerCanvas.renderMode = RenderMode.WorldSpace;
            _hammerImageParent = new GameObject("RepairModeHammerIndicatorParent");
            _hammerImageParent.transform.SetParent(_hammerCanvas.GetComponent<RectTransform>(), false);
            _hammerImageParent.transform.localPosition = _hammerCanvas.GetComponent<RectTransform>().rect.center;
            _hammerImage = _hammerImageParent.AddComponent<Image>();
            _hammerImage.sprite = 
                Sprite.Create(hammerTex,
                        new Rect(0.0f, 0.0f, hammerTex.width, hammerTex.height),
                        new Vector2(0.5f, 0.5f));
            _hammerCanvasRoot.SetActive(false);
        }

        public void Update()
        {
            if (_hammerCanvasRoot == null)
            {
                return;
            }
            if (!inPlaceMode() || hasPlacementGhost() || !ensureHammerCam())
            {
                _hammerCanvasRoot.SetActive(false);
                return;
            }
            _hammerCanvas.worldCamera = _hammerCam;
            _hammerCanvasRoot.transform.LookAt(_hammerCam.transform);
            _hammerCanvasRoot.transform.Rotate(new Vector3(0f, 180f, 0f));
            Vector3 position;
            float distance;
            getPosition(out position, out distance);
            _hammerCanvas.transform.localScale = Vector3.one * .0005f * distance;
            _hammerCanvasRoot.transform.position = position;
            _hammerCanvasRoot.SetActive(true);
        }

        private bool ensureHammerCam()
        {
            if (_hammerCam != null)
            {
                return true;
            }
            _hammerCam = CameraUtils.getWorldspaceUiCamera();
            return _hammerCam != null;
        }

        private void getPosition(out Vector3 position, out float distance)
        {
            RaycastHit hit;
            Vector3 rayStart = PlaceModeRayVectorProvider.startingPosition;
            Vector3 rayDirection = PlaceModeRayVectorProvider.rayDirection;
            float maxDistance = 50f;
            if (!Physics.Raycast(rayStart, rayDirection, out hit, maxDistance, getRayMask()) ||
                !hit.collider ||
                hit.collider.attachedRigidbody)
            {
                position = rayStart + rayDirection * maxDistance;
                distance = maxDistance;
                return;
            }
            position = hit.point;
            distance = hit.distance;
        }

        private int getRayMask()
        {
            if (Player.m_localPlayer == null)
            {
                return 0;
            }
            return Player.m_localPlayer.m_placeRayMask;
        }

        private bool inPlaceMode()
        {
            return Player.m_localPlayer != null && Player.m_localPlayer.InPlaceMode();
        }

        private bool hasPlacementGhost()
        {
            if (Player.m_localPlayer == null)
            {
                return false;
            }
            var ghost = Player.m_localPlayer.m_placementGhost;
            return ghost != null && ghost.activeSelf;
        }

        private static Texture2D loadHammerTexture() { 
            foreach (Texture2D t in Resources.FindObjectsOfTypeAll(typeof(Texture2D)))
            {
                if (t.name == "hammer")
                {
                    LogDebug("Found hammer texture.");
                    return t;
                }
            }
            LogError("Could not find hammer texture.");
            return null;
        }

    }
}
