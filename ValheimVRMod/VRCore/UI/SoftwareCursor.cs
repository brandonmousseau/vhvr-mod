using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using ValheimVRMod.Utilities;

using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.UI
{
    class SoftwareCursor : MonoBehaviour
    {
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        private static readonly float CURSOR_SPEED = 10.0f;
        private static Texture2D _cursorTexture;
        private static GameObject _instance;
        private static SoftwareCursor _cursorInstance;
        public RectTransform parent;
        private static Vector3 lastCursorPosition = Vector3.zero;

        private bool cursorVisible;

        // Convert the cursor coordinates to simulated screen mouse position coordinates
        // and vice versa.
        public static Vector3 simulatedMousePosition { get
            {
                if (instance.GetComponent<SoftwareCursor>().parent == null)
                {
                    return Vector3.zero;
                }
                RectTransform p = instance.GetComponent<SoftwareCursor>().parent;
                Vector2 position = lastCursorPosition;
                // Shift from 0,0 in center to 0,0 is top left
                position += new Vector2(p.rect.width * 0.5f, p.rect.height * 0.5f);
                float xScale = p.rect.width / Screen.width;
                float yScale = p.rect.height / Screen.height;
                //LogDebug("Simulated Position = " + new Vector3(position.x / xScale, position.y / yScale));
                return new Vector3(position.x / xScale, position.y / yScale);
            } set
            {
                if (instance.GetComponent<SoftwareCursor>().parent == null)
                {
                    return;
                }
                RectTransform p = instance.GetComponent<SoftwareCursor>().parent;
                float xScale = p.rect.width / Screen.width;
                float yScale = p.rect.height / Screen.height;
                float xValue = value.x * xScale;
                float yValue = value.y * yScale;
                Vector2 lastPosition = new Vector2(xValue, yValue);
                lastPosition -= new Vector2(p.rect.width * 0.5f, p.rect.height * 0.5f);
                lastCursorPosition = lastPosition;
            }
        }

        public static GameObject instance { get {
                if (_cursorInstance == null && searchForCursorTexture())
                {
                    _instance = new GameObject("VRSoftwareCursor");
                    _instance.layer = LayerMask.NameToLayer("UI");
                    var canvasGroup = _instance.AddComponent<CanvasGroup>();
                    // We need to ensure the cursor object does not block ray
                    // casts, otherwise it will interfere with things, including UI
                    // elements.
                    canvasGroup.blocksRaycasts = false;
                    DontDestroyOnLoad(_instance);
                    _cursorInstance = _instance.AddComponent<SoftwareCursor>();
                    Image cursorImage = _instance.AddComponent<Image>();
                    cursorImage.sprite =
                        Sprite.Create(_cursorTexture,
                        new Rect(0.0f, 0.0f, _cursorTexture.width, _cursorTexture.height),
                        new Vector2(0.5f, 0.5f));
                    cursorImage.transform.localScale = new Vector2(0.5f, 0.5f);
                    _instance.SetActive(true);
                }
                return _instance;
            } }

        void OnEnable()
        {
            LogDebug("SoftwareCursor: OnEnable()");
            cursorVisible = Cursor.visible;
            if (searchForCursorTexture())
            {
                LogDebug("Found Cursor texture");
                Texture2D blankCursor = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                blankCursor.SetPixel(0, 0, Color.clear);
                Cursor.SetCursor(blankCursor, Vector2.zero, CursorMode.ForceSoftware);
            } else
            {
                LogError("Could not locate cursor texture!");
                enabled = false;
            }
        }

        void Update()
        {
            if (_instance == null)
            {
                return;
            }
            updateCursorLocation();
            _instance.GetComponent<Image>().enabled = Cursor.visible;
            if (Application.isFocused)
            {
                // We'll lock the hardware cursor in place using this
                // system function. I found that using Cursor.lockState
                // was having side effects on mouse position/raycast calculations
                // and was messing up the simulated cursor behavior a bit.
                SetCursorPos(Screen.width/2, Screen.height/2);
            }
        }

        void updateCursorLocation()
        {
            if (parent == null)
            {
                LogDebug("Parent of SoftwareCursor is null.");
                return;
            }
            instance.SetActive(true);
            Rect rect = parent.rect;
            float deltaX = Input.GetAxis("Mouse X");
            float deltaY = Input.GetAxis("Mouse Y");
            Vector2 newPosition = lastCursorPosition;
            if (Cursor.visible && !cursorVisible)
            {
                // Cursor just became visible this update, so re-center it.
                newPosition = rect.center;
            }
            cursorVisible = Cursor.visible;
            newPosition.x += deltaX * CURSOR_SPEED * PlayerController.m_mouseSens;
            newPosition.y += deltaY * CURSOR_SPEED * PlayerController.m_mouseSens;
            newPosition.x = Mathf.Clamp(newPosition.x, rect.xMin, rect.xMax);
            newPosition.y = Mathf.Clamp(newPosition.y, rect.yMin, rect.yMax);
            lastCursorPosition = newPosition;
            instance.transform.localPosition = newPosition + new Vector2(0f, -0.5f * _cursorTexture.height);
        }

        // TODO: Find if there is a better way to grab
        // the texture.
        private static bool searchForCursorTexture()
        {
            if (_cursorTexture != null)
            {
                return true;
            }
            foreach (var g in Resources.FindObjectsOfTypeAll(typeof(Texture2D)))
            {
                if (g.name == "cursor")
                {
                    _cursorTexture = g as Texture2D;
                    return true;
                }
            }
            return false;
        }
    }
}
