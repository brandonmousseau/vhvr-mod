using UnityEngine;
using ValheimVRMod.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    class CrosshairManager
    {
        public static readonly int CROSSHAIR_LAYER = 22;
        public static readonly int CROSSHAIR_LAYER_MASK = (1 << CROSSHAIR_LAYER);
        private static readonly Vector3 CROSSHAIR_CANVAS_OFFSET = new Vector3(0f, 0f, 0.5f);
        private static readonly Vector3 CROSSHAIR_SCALAR = new Vector3(0.3f, 0.3f);

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

        public Canvas guiCanvas;

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

        public void maybeReparentCrosshair()
        {
            if (SceneManager.GetActiveScene().name != "main" || guiCanvas == null || !ensureCrosshairCanvas())
            {
                return;
            }
            if ((_canvasCrosshairRoot == null || _canvasCrosshairRootClone == null)
                && !findVanillaCrosshairs(guiCanvas.transform))
            {
                return;
            }
            _canvasCrosshairRoot.SetActive(false); // Disable the original crosshairs
            _canvasCrosshairRootClone.SetActive(VRPlayer.attachedToPlayer);
            configureCrosshairElements(_canvasCrosshairRootClone, CROSSHAIR_LAYER,
                _crosshairCanvasParent.transform.position, _crosshairCanvasParent.transform.rotation);
            var rectTransform = _canvasCrosshairRootClone.GetComponent<RectTransform>();
            setCanvasPosition();
            UpdateHudReferences();
            if (rectTransform == null)
            {
                LogError("Crosshair Rect Transform is Null");
                return;
            }
            rectTransform.SetParent(_crosshairCanvas.GetComponent<RectTransform>());
        }

        private bool ensureCrosshairCanvas()
        {
            if (_crosshairCanvas != null)
            {
                return true;
            }
            createCrosshairCamera();
            _crosshairCanvasParent = new GameObject("CrosshairCanvasGameObject");
            _crosshairCanvasParent.layer = CROSSHAIR_LAYER;
            GameObject.DontDestroyOnLoad(_crosshairCanvasParent);
            _crosshairCanvas = _crosshairCanvasParent.AddComponent<Canvas>();
            _crosshairCanvas.renderMode = RenderMode.WorldSpace;
            _crosshairCanvas.worldCamera = _crosshairCamera;
            _crosshairCanvas.GetComponent<RectTransform>().SetParent(_crosshairCanvasParent.transform, false);
            float canvasWidth = _crosshairCanvas.GetComponent<RectTransform>().rect.width;
            float scaleFactor = .6f / canvasWidth;
            _crosshairCanvas.GetComponent<RectTransform>().localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
            setCanvasPosition();
            _crosshairCamera.orthographicSize = _crosshairCanvas.GetComponent<RectTransform>().rect.height * 0.5f;
            LogDebug("Created Crosshair Canvas");
            return true;
        }

        private void setCanvasPosition()
        {
            if (_crosshairCanvasParent == null || VRPlayer.instance == null || _crosshairCamera == null)
            {
                return;
            }
            _crosshairCamera.transform.rotation = CameraUtils.getCamera(CameraUtils.VR_CAMERA).transform.rotation;
            _crosshairCanvasParent.transform.SetParent(_crosshairCamera.gameObject.transform, false);
            _crosshairCanvasParent.transform.position = VRPlayer.instance.transform.position;
            _crosshairCanvasParent.transform.localPosition = CROSSHAIR_CANVAS_OFFSET;
            _crosshairCanvasParent.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        private void createCrosshairCamera()
        {
            if (_crosshairCamera != null)
            {
                return;
            }
            GameObject crosshairCameraObject = new GameObject(CameraUtils.CROSSHAIR_CAMERA);
            GameObject.DontDestroyOnLoad(crosshairCameraObject);
            _crosshairCamera = crosshairCameraObject.AddComponent<Camera>();
            _crosshairCamera.CopyFrom(CameraUtils.getCamera(CameraUtils.VR_CAMERA));
            _crosshairCamera.depth = crosshairDepth;
            _crosshairCamera.clearFlags = CameraClearFlags.Depth;
            _crosshairCamera.cullingMask = CROSSHAIR_LAYER_MASK;
            _crosshairCamera.transform.SetParent(CameraUtils.getCamera(CameraUtils.VR_CAMERA).gameObject.transform, false);
            _crosshairCamera.orthographic = true;
            _crosshairCamera.enabled = true;
            LogDebug("Created Crosshair Camera");
        }

        private void configureCrosshairElements(GameObject clone, int layer, Vector3 position, Quaternion rotation)
        {
            if (clone == null)
            {
                return;
            }
            clone.layer = layer;
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            clone.transform.localScale = CROSSHAIR_SCALAR * VVRConfig.CrosshairScalar();
            if (clone.name == "crosshair" && clone.transform.parent != null &&
                clone.transform.parent.gameObject.name == "crosshair")
            {
                clone.SetActive(VVRConfig.ShowStaticCrosshair());
            }
            foreach (Transform t in clone.transform)
            {
                configureCrosshairElements(t.gameObject, layer, position, rotation);
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
                }
            }
        }

        // Make sure the HUD is always using to our copies of the
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
                Text hoverText = _hoverNameClone.GetComponent<Text>();
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
                    LogDebug("Steal GUI Bar is null");
                }
            }
            else
            {
                LogDebug("Stealth Bar clone is null");
            }
        }

    }
}
