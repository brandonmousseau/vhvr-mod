using UnityEngine;
using ValheimVRMod.Utilities;
using ValheimVRMod.Patches;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HarmonyLib;

using static ValheimVRMod.Utilities.LogUtils;
using TMPro;

namespace ValheimVRMod.VRCore.UI
{
    class CrosshairManager
    {
        private static readonly float CROSSHAIR_SCALAR = 0.1f;
        private static readonly float MIN_CROSSHAIR_DISTANCE = 0.5f;
        public static readonly float WEAPON_CROSSHAIR_DISTANCE = 128f;

        public static int crosshairDepth = 1;

        public static CrosshairManager instance { get
            {
                if (_instance == null)
                {
                    _instance = new CrosshairManager();
                }
                return _instance;
            } }

        private static CrosshairManager _instance;

        private static int CROSSHAIR_RAYCAST_LAYERMASK = getCrosshairRaycastLayerMask();

        public Canvas guiCanvas;

        private GameObject _pieceHealthCanvasParent;
        private Canvas _pieceHealthCanvas;

        private Camera _crosshairCamera;
        private Canvas _crosshairCanvas;
        private GameObject _crosshairCanvasParent;
        private GameObject _canvasCrosshairRoot;

        private GameObject _canvasCrosshairRootClone;
        private GameObject _crosshairClone;
        private GameObject _crosshairBowClone;
        private GameObject _hoverNameClone;
        private GameObject _sneakDetectedClone;
        private GameObject _sneakHiddenClone;
        private GameObject _sneakAlertClone;
        private GameObject _stealthBarClone;
        private GameObject _pieceHealthRoot;
        private GameObject _pieceHealthBar;

        private GameObject _hoverNameCanvasParent;
        private Canvas _hoverNameCanvas;
        // Left hand hovername objects
        public GameObject crosshairCloneLeftHand;
        public GameObject hoverNameCloneLeftHand;
        private GameObject _hoverNameCanvasParentLeft;
        private Canvas _hoverNameCanvasLeft;

        // Weapon crosshair objects
        private GameObject _crosshairCloneForWeapon;
        private GameObject _weaponCrosshair;
        public GameObject weaponCrosshair { get {
                if (_weaponCrosshair == null)
                {
                    _weaponCrosshair = new GameObject("WeaponCrosshair");
                    _weaponCrosshair.layer = LayerUtils.getWorldspaceUiLayer();
                    Canvas canvas = _weaponCrosshair.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.WorldSpace;
                    float canvasWidth = canvas.GetComponent<RectTransform>().rect.width;
                    float scaleFactor = CROSSHAIR_SCALAR * VHVRConfig.CrosshairScalar() / canvasWidth;
                    _weaponCrosshair.transform.localScale = Vector3.one * scaleFactor * WEAPON_CROSSHAIR_DISTANCE;
                }
                if (_crosshairCloneForWeapon == null)
                {
                    _crosshairCloneForWeapon = GameObject.Instantiate(_crosshairClone);
                }
                if (_crosshairCloneForWeapon != null)
                {
                    _crosshairCloneForWeapon.transform.SetParent(_weaponCrosshair.GetComponent<Canvas>().GetComponent<RectTransform>(), false);
                }
                return _weaponCrosshair;
            }
        }

        public void maybeReparentCrosshair()
        {
            if (SceneManager.GetActiveScene().name != "main" || guiCanvas == null || !ensureCrosshairCanvas() || !ensureCrosshairCamera())
            {
                if (_hoverNameCanvasParentLeft)
                {
                    _hoverNameCanvasParentLeft.SetActive(false);
                }
                if (_hoverNameCanvasParent)
                {
                    _hoverNameCanvasParent.SetActive(false);
                }
                if (_crosshairCanvasParent)
                {
                    _crosshairCanvasParent.SetActive(false);
                }
                return;
            }
            if (Player.m_localPlayer && Player.m_localPlayer.IsAttachedToShip() && Player.m_localPlayer.GetControlledShip())
            {
                // TODO: check if these should simply be skipped when motion control is disabled.
                _hoverNameCanvasParentLeft?.SetActive(false);
                _hoverNameCanvasParent?.SetActive(false);
                return;
            }
            if ((_canvasCrosshairRoot == null || _canvasCrosshairRootClone == null)
                && !findVanillaCrosshairs(guiCanvas.transform))
            {
                return;
            }
            reparentPieceHealthObjects();
            if (VHVRConfig.UseVrControls())
            {
                reparentAndCloneHoverNameObjects();
            }
            _canvasCrosshairRoot.SetActive(false); // Disable the original crosshairs
            _canvasCrosshairRootClone.SetActive(VRPlayer.attachedToPlayer);
            _canvasCrosshairRootClone.transform.SetParent(_crosshairCanvas.transform, false);
            // Vanilla game has control of isActive state of _crosshairClone, so only disabling its Image component can hide it.
            _crosshairClone.GetComponent<Image>().enabled = VHVRConfig.ShowStaticCrosshair();
            if (crosshairCloneLeftHand)
            {
                crosshairCloneLeftHand.SetActive(VHVRConfig.ShowStaticCrosshair());
            }
            if (_hoverNameCanvasParentLeft)
            {
                _hoverNameCanvasParentLeft.SetActive(VRPlayer.attachedToPlayer);
            }
            if (_hoverNameCanvasParent)
            {
                _hoverNameCanvasParent.SetActive(VRPlayer.attachedToPlayer);
            }
            var rectTransform = _canvasCrosshairRootClone.GetComponent<RectTransform>();
            setCrosshairCanvasPositionAndScale();
            setPieceHealthCanvasPositionAndScale();
            if (VHVRConfig.UseVrControls())
            {
                setHoverNameCanvasesPositionAndScale();
            }
            UpdateHudReferences();
            if (rectTransform == null)
            {
                LogError("Crosshair Rect Transform is Null");
                return;
            }
            rectTransform.SetParent(_crosshairCanvas.GetComponent<RectTransform>());
        }

        private void maybeCreatePieceHealthObjects()
        {
            if (_pieceHealthCanvasParent != null && _pieceHealthCanvas != null || !ensureCrosshairCamera())
            {
                return;
            }
            _pieceHealthCanvasParent = new GameObject("WorldSpacePieceHealthCanvasParent");
            GameObject.DontDestroyOnLoad(_pieceHealthCanvasParent);
            _pieceHealthCanvasParent.layer = LayerUtils.getWorldspaceUiLayer();
            _pieceHealthCanvas = _pieceHealthCanvasParent.AddComponent<Canvas>();
            _pieceHealthCanvas.renderMode = RenderMode.WorldSpace;
            _pieceHealthCanvas.worldCamera = _crosshairCamera;
        }

        private void reparentPieceHealthObjects()
        {
            maybeCreatePieceHealthObjects();
            if (_pieceHealthRoot != null && _pieceHealthCanvas != null)
            {
                _pieceHealthRoot.transform.SetParent(_pieceHealthCanvas.GetComponent<RectTransform>(), false);
                _pieceHealthCanvas.transform.localPosition = _pieceHealthCanvas.GetComponent<RectTransform>().rect.center;
            }
        }

        private void maybeCreateHoverNameObjects()
        {
            if ((_hoverNameCanvasParent != null && _hoverNameCanvas != null && _hoverNameCanvasParentLeft != null && _hoverNameCanvasLeft != null) || !ensureCrosshairCamera())
            {
                return;
            }
            _hoverNameCanvasParent = new GameObject("HoverNameCanvasParent");
            GameObject.DontDestroyOnLoad(_hoverNameCanvasParent);
            _hoverNameCanvasParent.layer = LayerUtils.getWorldspaceUiLayer();
            _hoverNameCanvas = _hoverNameCanvasParent.AddComponent<Canvas>();
            _hoverNameCanvas.renderMode = RenderMode.WorldSpace;
            _hoverNameCanvas.worldCamera = _crosshairCamera;

            // Left hand
            _hoverNameCanvasParentLeft = new GameObject("HoverNameCanvasParentLeft");
            GameObject.DontDestroyOnLoad(_hoverNameCanvasParentLeft);
            _hoverNameCanvasParentLeft.layer = LayerUtils.getWorldspaceUiLayer();
            _hoverNameCanvasLeft = _hoverNameCanvasParentLeft.AddComponent<Canvas>();
            _hoverNameCanvasLeft.renderMode = RenderMode.WorldSpace;
            _hoverNameCanvasLeft.worldCamera = _crosshairCamera;
            hoverNameCloneLeftHand = GameObject.Instantiate(_hoverNameClone);
            crosshairCloneLeftHand = GameObject.Instantiate(_crosshairClone);
        }

        private void reparentAndCloneHoverNameObjects()
        {
            if (_hoverNameClone == null)
            {
                return;
            }
            maybeCreateHoverNameObjects();
            if (_hoverNameCanvas != null && _hoverNameCanvasLeft != null)
            {
                _hoverNameClone.transform.SetParent(_hoverNameCanvas.GetComponent<RectTransform>(), false);
                _crosshairClone.transform.SetParent(_hoverNameCanvas.GetComponent<RectTransform>(), false);
                hoverNameCloneLeftHand.transform.SetParent(_hoverNameCanvasLeft.GetComponent<RectTransform>(), false);
                crosshairCloneLeftHand.transform.SetParent(_hoverNameCanvasLeft.GetComponent<RectTransform>(), false);
            }
        }

        private bool ensureCrosshairCanvas()
        {
            if (_crosshairCanvas != null)
            {
                return true;
            }
            if (!ensureCrosshairCamera())
            {
                return false;
            }
            _crosshairCanvasParent = new GameObject("CrosshairCanvasGameObject");
            _crosshairCanvasParent.layer = LayerUtils.getWorldspaceUiLayer();
            GameObject.DontDestroyOnLoad(_crosshairCanvasParent);
            _crosshairCanvas = _crosshairCanvasParent.AddComponent<Canvas>();
            _crosshairCanvas.renderMode = RenderMode.WorldSpace;
            _crosshairCanvas.worldCamera = _crosshairCamera;
            _crosshairCanvas.GetComponent<RectTransform>().SetParent(_crosshairCanvasParent.transform, false);
            _crosshairCamera.orthographicSize = _crosshairCanvas.GetComponent<RectTransform>().rect.height * 0.5f;
            LogDebug("Created Crosshair Canvas");
            return true;
        }

        private void setCrosshairCanvasPositionAndScale()
        {
            if (_crosshairCanvasParent == null || VRPlayer.instance == null || _crosshairCamera == null)
            {
                return;
            }
            float canvasWidth = _crosshairCanvas.GetComponent<RectTransform>().rect.width;
            float scaleFactor = CROSSHAIR_SCALAR * VHVRConfig.CrosshairScalar() / canvasWidth;
            _crosshairCamera.transform.rotation = CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform.rotation;
            _crosshairCamera.transform.position = CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform.position;
            _crosshairCanvasParent.transform.SetParent(_crosshairCamera.gameObject.transform, false);
            _crosshairCanvasParent.transform.position = VRPlayer.instance.transform.position;
            float crosshairDistance = calculateCrosshairDistance();
            _crosshairCanvasParent.transform.localPosition = new Vector3(0f, 0f, crosshairDistance);
            _crosshairCanvas.GetComponent<RectTransform>().localScale = Vector3.one * scaleFactor * crosshairDistance;
            _crosshairCanvasParent.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        private void setPieceHealthCanvasPositionAndScale()
        {
            if (_pieceHealthCanvasParent == null)
            {
                return;
            }
            Piece hoveringPiece = getHoveringPiece();
            if (hoveringPiece != null)
            {
                float distance = Vector3.Distance(hoveringPiece.gameObject.transform.position, _crosshairCamera.transform.position);
                _pieceHealthCanvasParent.transform.position = hoveringPiece.gameObject.transform.position;
                _pieceHealthCanvasParent.transform.localScale = Vector3.one * 0.0015f * distance;
                _pieceHealthCanvasParent.transform.LookAt(_crosshairCamera.transform);
                _pieceHealthCanvasParent.transform.Rotate(0f, 180f, 0f);
            }
        }

        private void setHoverNameCanvasesPositionAndScale()
        {
            if (_hoverNameCanvasParent == null)
            {
                return;
            }
            float canvasWidth = _hoverNameCanvas.GetComponent<RectTransform>().rect.width;
            float scaleFactor = CROSSHAIR_SCALAR * VHVRConfig.CrosshairScalar() / canvasWidth;
            var positionRight = HandBasedInteractionPatches.currentHitPositionRight;
            var positionLeft = HandBasedInteractionPatches.currentHitPositionLeft;
            float distanceRight = Vector3.Distance(positionRight, _crosshairCamera.transform.position);
            float distanceLeft = Vector3.Distance(positionLeft, _crosshairCamera.transform.position);
            _hoverNameCanvasParent.transform.position = positionRight;
            _hoverNameCanvasParent.transform.localScale = Vector3.one * scaleFactor * distanceRight;
            _hoverNameCanvasParent.transform.LookAt(_crosshairCamera.transform);
            _hoverNameCanvasParent.transform.Rotate(0f, 180f, 0f);
            _hoverNameCanvasParentLeft.transform.position = positionLeft;
            _hoverNameCanvasParentLeft.transform.localScale = Vector3.one * scaleFactor * distanceLeft;
            _hoverNameCanvasParentLeft.transform.LookAt(_crosshairCamera.transform);
            _hoverNameCanvasParentLeft.transform.Rotate(0f, 180f, 0f);
        }

        private Piece getHoveringPiece()
        {
            if (Player.m_localPlayer == null)
            {
                return null;
            }
            return Player.m_localPlayer.m_hoveringPiece;
        }

        private float calculateCrosshairDistance()
        {
            RaycastHit hit;
            if (Physics.Raycast(new Ray(_crosshairCamera.transform.position, _crosshairCamera.transform.forward), out hit,
                _crosshairCamera.farClipPlane * 0.95f, CROSSHAIR_RAYCAST_LAYERMASK))
            {
                return Mathf.Max(MIN_CROSSHAIR_DISTANCE, hit.distance);
            }
            return _crosshairCamera.farClipPlane * 0.95f;
        }

        private static int getCrosshairRaycastLayerMask()
        {
            int mask = Physics.DefaultRaycastLayers;
            // Ignore these layers
            mask &= ~(1 << 14); // character_trigger
            mask &= ~(1 << 21); // WaterVolume
            mask &= ~(1 << 4);  // Water
            mask &= ~(1 << 25); // viewblock
            mask &= ~(1 << 31); // smoke
            mask &= ~(1 << 5);  // UI
            mask &= ~(1 << 24); // pathblocker
            mask &= ~(1 << LayerUtils.getUiPanelLayer());
            mask &= ~(1 << LayerUtils.getHandsLayer());
            mask &= ~(1 << LayerUtils.getWorldspaceUiLayer());
            return mask;
        }

        private bool ensureCrosshairCamera()
        {
            if (_crosshairCamera != null)
            {
                return true;
            }
            _crosshairCamera = CameraUtils.getWorldspaceUiCamera();
            return _crosshairCamera != null;
        }

        private void configureCrosshairElements(GameObject clone)
        {
            if (clone == null)
            {
                LogError("Null Crosshair Clone while configuring clones.");
                return;
            }
            clone.layer = LayerUtils.getWorldspaceUiLayer();
            foreach (Transform t in clone.transform)
            {
                configureCrosshairElements(t.gameObject);
            }
        }

        // Function will recursively walk down canvas tree until it reaches
        // the first element named "crosshair" and caches it. This element
        // is a root node that holds all the relevant crosshair components
        private bool findVanillaCrosshairs(Transform t)
        {
            if (t == null)
            {
                return false;
            }
            if (t.gameObject.name == "crosshair")
            {
                LogDebug("Found crosshair canvas root element.");
                _canvasCrosshairRoot = t.gameObject;
                _canvasCrosshairRootClone = GameObject.Instantiate(_canvasCrosshairRoot);
                configureCrosshairElements(_canvasCrosshairRootClone);
                _canvasCrosshairRootClone.SetActive(true);
                cacheClones();
                return true;
            }
            foreach (Transform child in t)
            {
                if (findVanillaCrosshairs(child))
                {
                    return true;
                }
            }
            return false;
        }

        private void cacheClones()
        {
            if (_canvasCrosshairRootClone == null)
            {
                LogDebug("Crosshair root clone is null.");
            }
            foreach (Transform child in _canvasCrosshairRootClone.transform)
            {
                if (child.gameObject.name == "crosshair")
                {
                    _crosshairClone = child.gameObject;
                }
                else if (child.gameObject.name == "crosshair_bow")
                {
                    _crosshairBowClone = child.gameObject;
                }
                else if (child.gameObject.name == "HoverName")
                {
                    _hoverNameClone = child.gameObject;
                }
                else if (child.gameObject.name == "Sneak_hidden")
                {
                    _sneakHiddenClone = child.gameObject;
                }
                else if (child.gameObject.name == "Sneak_detected")
                {
                    _sneakDetectedClone = child.gameObject;
                }
                else if (child.gameObject.name == "Sneak_alert")
                {
                    _sneakAlertClone = child.gameObject;
                }
                else if (child.gameObject.name == "StealthBar")
                {
                    _stealthBarClone = child.gameObject;
                }
                else if (child.gameObject.name == "PieceHealthRoot")
                {
                    _pieceHealthRoot = child.gameObject;
                    foreach (Transform healthRootChild in child)
                    {
                        if (healthRootChild.gameObject.name == "PieceHealthBar")
                        {
                            _pieceHealthBar = healthRootChild.gameObject;
                            break;
                        }
                    }
                }
            }
        }

        // Make sure the HUD is always pointing to our copies of the
        // crosshair elements.
        private void UpdateHudReferences()
        {
            var hud = Hud.instance;
            if (hud == null)
            {
                LogDebug("NULL Hud");
            }
            if (_crosshairClone != null)
            {
                Image crosshairImage = _crosshairClone.GetComponent<Image>();
                if (crosshairImage != null)
                {
                    hud.m_crosshair = crosshairImage;
                }
                else
                {
                    LogDebug("Null CrosshairClone Image");
                }
            }
            else
            {
                LogDebug("Null Crosshair Clone");
            }
            if (_crosshairBowClone != null)
            {
                Image crosshairBowImage = _crosshairBowClone.GetComponent<Image>();
                if (crosshairBowImage != null)
                {
                    hud.m_crosshairBow = crosshairBowImage;
                }
                else
                {
                    LogDebug("Null CrosshairBow Clone Image");
                }
            }
            else
            {
                LogDebug("Null CrosshairBow Clone");
            }
            if (_hoverNameClone != null)
            {
                TextMeshProUGUI hoverText = _hoverNameClone.GetComponent<TextMeshProUGUI>();
                if (hoverText != null)
                {
                    hud.m_hoverName = hoverText;
                }
                else
                {
                    LogDebug("Null HoverText Text");
                }
            }
            else
            {
                LogDebug("Null HoverName clone");
            }
            if (_pieceHealthRoot != null)
            {
                RectTransform pieceHealthRootRect = _pieceHealthRoot.GetComponent<RectTransform>();
                if (pieceHealthRootRect != null)
                {
                    hud.m_pieceHealthRoot = pieceHealthRootRect;
                }
                else
                {
                    LogDebug("Null Piece Health Root RectTransform");
                }
            }
            else
            {
                LogDebug("Piece health root is null");
            }
            if (_sneakAlertClone != null)
            {
                hud.m_targetedAlert = _sneakAlertClone;
            }
            else
            {
                LogDebug("Sneak Alert Clone is null");
            }
            if (_sneakDetectedClone != null)
            {
                hud.m_targeted = _sneakDetectedClone;
            }
            else
            {
                LogDebug("Sneak Detected Clone is null");
            }
            if (_sneakHiddenClone != null)
            {
                hud.m_hidden = _sneakHiddenClone;
            }
            else
            {
                LogDebug("Sneak hidden clone is null");
            }
            if (_stealthBarClone != null)
            {
                GuiBar stealthGuiBar = _stealthBarClone.GetComponent<GuiBar>();
                if (stealthGuiBar != null)
                {
                    hud.m_stealthBar = stealthGuiBar;
                }
                else
                {
                    LogDebug("Stealth GUI Bar is null");
                }
            }
            else
            {
                LogDebug("Stealth Bar clone is null");
            }
            if (_pieceHealthBar != null)
            {
                GuiBar pieceHealthGuiBar = _pieceHealthBar.GetComponent<GuiBar>();
                if (pieceHealthGuiBar != null)
                {
                    hud.m_pieceHealthBar = pieceHealthGuiBar;
                } else
                {
                    LogDebug("PieceHealthBar GUI bar is null");
                }
            } else
            {
                LogDebug("PieceHealthBar is null.");
            }
        }

    }
}
