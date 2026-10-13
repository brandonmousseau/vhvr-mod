using ValheimVRMod.VRCore.Backends;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ValheimVRMod.Utilities;

namespace ValheimVRMod.VRCore.UI
{
    /**
     * The FBT tab of the VHVR settings dialog: a table assigning trackers to body joints. There is a row for
     * turning a joint off, a row for auto-detection and a row for each connected tracker, and a column for each
     * joint. Each column is a radio group, the selected circle is the tracker driving that joint.
     *
     * The selection is stored as device indices in the config (-1: off, 0: auto, otherwise the device index, see
     * VRPlayer.ResolveJointTransform()) and, like the rest of the dialog, only saved when the dialog is confirmed.
     */
    class FullBodyTrackingTab : MonoBehaviour
    {
        private const int OFF = -1;
        private const int AUTO = 0;

        private const float REFRESH_INTERVAL = 1f;
        private const float TITLE_LEFT = -420;
        private const float TITLE_WIDTH = 440;
        private const float HEADER_Y = 250;
        private const float FIRST_ROW_Y = 212;
        private const float LAST_ROW_Y = -200;
        private const float MAX_ROW_HEIGHT = 32;
        private const float CELL_WIDTH = 120;
        private const float CIRCLE_SIZE = 26;
        private const int CIRCLE_TEXTURE_SIZE = 64;

        private const string AUTO_HOVER_TIP = "Automatically detect using tracker roles and positions during recenter pose";

        private static readonly string[] COLUMN_TITLES = { "Hip", "Left foot", "Right foot" };
        private static readonly float[] COLUMN_X = { 100, 230, 360 };
        private static readonly Color SELECTED_COLOR = new Color(1f, 0.72f, 0.36f);
        private static readonly Color DISCONNECTED_COLOR = new Color(1f, 1f, 1f, 0.5f);

        private static Sprite ringSprite;
        private static Sprite dotSprite;

        // A text object to clone the labels from, so that they get the font of the vanilla settings.
        public GameObject labelPrefab;

        private ConfigEntry<int>[] entries;
        // The device index picked for each column, not saved until the dialog is confirmed.
        private int[] selection;
        private Transform table;
        private readonly List<Row> rows = new List<Row>();
        // What the table currently lists, to tell when trackers have come or gone.
        private string shownTrackers;
        private float lastRefreshTime;

        private class Row
        {
            public int deviceIndex;
            public GameObject[] dots;
        }

        private struct Tracker
        {
            public int deviceIndex;
            public string title;
            public bool connected;
        }

        // Shows a text in the tooltip of the settings dialog while hovered, like ConfigComponent does for a config option.
        private class HoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private static HoverTip current;

            public string text;

            private void LateUpdate()
            {
                if (current != this)
                {
                    return;
                }
                TMP_Text textObj = ConfigSettings.toolTip.GetComponentInChildren<TMP_Text>();
                textObj.text = text;
                ConfigSettings.toolTip.GetComponent<Image>().rectTransform.sizeDelta = new Vector2(908, textObj.preferredHeight + 8);
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                current = this;
                ConfigSettings.toolTip.GetComponentInChildren<TMP_Text>(includeInactive: true).text = text;
                ConfigSettings.toolTip.SetActive(true);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                Hide();
            }

            // The table is rebuilt when trackers come or go, and the tab can be switched while a title is hovered.
            private void OnDisable()
            {
                Hide();
            }

            private void Hide()
            {
                if (current == this)
                {
                    current = null;
                    ConfigSettings.toolTip.SetActive(false);
                }
            }
        }

        public void Initialize(ConfigEntry<int> hip, ConfigEntry<int> leftFoot, ConfigEntry<int> rightFoot)
        {
            entries = new[] { hip, leftFoot, rightFoot };
            selection = new[] { hip.Value, leftFoot.Value, rightFoot.Value };
        }

        private void OnEnable()
        {
            RefreshTable();
        }

        private void Update()
        {
            if (Time.unscaledTime - lastRefreshTime > REFRESH_INTERVAL)
            {
                RefreshTable();
            }
        }

        private void OnDestroy()
        {
            if (!ConfigSettings.doSave || entries == null)
            {
                return;
            }
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i].Value = selection[i];
            }
        }

        // Rebuilds the table if the connected trackers have changed since it was built.
        private void RefreshTable()
        {
            lastRefreshTime = Time.unscaledTime;
            if (entries == null)
            {
                return;
            }

            var trackers = ListTrackers();
            var description = new StringBuilder();
            foreach (var tracker in trackers)
            {
                description.Append(tracker.deviceIndex).Append(':').Append(tracker.title).Append('\n');
            }
            if (table != null && description.ToString() == shownTrackers)
            {
                return;
            }
            shownTrackers = description.ToString();
            BuildTable(trackers);
        }

        // The connected trackers, plus the ones that are assigned to a joint but aren't connected, so that an
        // assignment never disappears from the table.
        private List<Tracker> ListTrackers()
        {
            var trackers = new List<Tracker>();
            foreach (var tracker in VRBackend.Active.GetConnectedTrackers())
            {
                trackers.Add(new Tracker { deviceIndex = tracker.Index, title = tracker.Title, connected = true });
            }

            foreach (int deviceIndex in selection)
            {
                if (deviceIndex > 0 && !trackers.Exists(tracker => tracker.deviceIndex == deviceIndex))
                {
                    trackers.Add(new Tracker { deviceIndex = deviceIndex, title = "#" + deviceIndex + "  (not connected)", connected = false });
                }
            }
            trackers.Sort((a, b) => a.deviceIndex.CompareTo(b.deviceIndex));
            return trackers;
        }

        // E. g. "#5  VIVE Tracker 3.0  LHR-1234ABCD  (waist)", the role being the one assigned in SteamVR.




        private void BuildTable(List<Tracker> trackers)
        {
            if (table != null)
            {
                Destroy(table.gameObject);
            }
            rows.Clear();

            var tableRect = new GameObject("TrackerTable", typeof(RectTransform)).GetComponent<RectTransform>();
            tableRect.SetParent(transform, false);
            tableRect.anchorMin = tableRect.anchorMax = tableRect.pivot = new Vector2(0.5f, 0.5f);
            tableRect.sizeDelta = Vector2.zero;
            tableRect.anchoredPosition = Vector2.zero;
            table = tableRect;

            CreateLabel(table, "Tracker", new Vector2(TITLE_LEFT, HEADER_Y), TITLE_WIDTH, TextAlignmentOptions.MidlineLeft);
            for (int column = 0; column < COLUMN_TITLES.Length; column++)
            {
                CreateLabel(table, COLUMN_TITLES[column], new Vector2(COLUMN_X[column], HEADER_Y), CELL_WIDTH, TextAlignmentOptions.Midline);
            }

            // Off, auto, the trackers, and a line saying that there are none.
            int rowCount = 2 + Mathf.Max(trackers.Count, 1);
            float rowHeight = Mathf.Min(MAX_ROW_HEIGHT, (FIRST_ROW_Y - LAST_ROW_Y) / (rowCount - 1));
            float y = FIRST_ROW_Y;
            CreateRow(OFF, "Off", y, rowHeight, Color.white, null);
            y -= rowHeight;
            CreateRow(AUTO, "Auto", y, rowHeight, Color.white, AUTO_HOVER_TIP);
            y -= rowHeight;
            foreach (var tracker in trackers)
            {
                // The title may not fit in the table, the hover tip shows it in full.
                CreateRow(tracker.deviceIndex, tracker.title, y, rowHeight, tracker.connected ? Color.white : DISCONNECTED_COLOR, tracker.title);
                y -= rowHeight;
            }
            if (trackers.Count == 0)
            {
                CreateLabel(table, "No trackers connected", new Vector2(TITLE_LEFT, y), TITLE_WIDTH, TextAlignmentOptions.MidlineLeft).color =
                    DISCONNECTED_COLOR;
            }

            UpdateSelection();
        }

        private void CreateRow(int deviceIndex, string title, float y, float height, Color color, string hoverTip)
        {
            // The title and the cells are children of the row, so that the row stays hovered while they are.
            var rowRect = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            rowRect.SetParent(table, false);
            rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0.5f, 0.5f);
            float halfWidth = Mathf.Max(-TITLE_LEFT, COLUMN_X[COLUMN_X.Length - 1] + CELL_WIDTH * 0.5f);
            rowRect.sizeDelta = new Vector2(halfWidth * 2, height);
            rowRect.anchoredPosition = new Vector2(0, y);
            if (hoverTip != null)
            {
                // The tip shows for the whole row, including the gaps between its cells.
                rowRect.gameObject.AddComponent<Image>().color = Color.clear;
                rowRect.gameObject.AddComponent<HoverTip>().text = hoverTip;
            }

            CreateLabel(rowRect, title, new Vector2(TITLE_LEFT, 0), TITLE_WIDTH, TextAlignmentOptions.MidlineLeft).color = color;
            var row = new Row { deviceIndex = deviceIndex, dots = new GameObject[COLUMN_X.Length] };
            for (int column = 0; column < COLUMN_X.Length; column++)
            {
                row.dots[column] = CreateCell(rowRect, column, deviceIndex, new Vector2(COLUMN_X[column], 0));
            }
            rows.Add(row);
        }

        // Creates a selectable circle and returns the dot that marks it as selected.
        private GameObject CreateCell(Transform parent, int column, int deviceIndex, Vector2 position)
        {
            EnsureSprites();

            // The whole cell is clickable rather than just the circle, which is a small target for a laser pointer.
            var cell = new GameObject("Cell", typeof(RectTransform));
            var cellRect = cell.GetComponent<RectTransform>();
            cellRect.SetParent(parent, false);
            cellRect.anchorMin = cellRect.anchorMax = cellRect.pivot = new Vector2(0.5f, 0.5f);
            cellRect.sizeDelta = new Vector2(CELL_WIDTH, MAX_ROW_HEIGHT);
            cellRect.anchoredPosition = position;
            cell.AddComponent<Image>().color = Color.clear;

            var ring = CreateCircleImage("Ring", cellRect, ringSprite, CIRCLE_SIZE);
            var dot = CreateCircleImage("Dot", cellRect, dotSprite, CIRCLE_SIZE);
            dot.color = SELECTED_COLOR;

            var button = cell.AddComponent<Button>();
            button.targetGraphic = ring;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.6f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = SELECTED_COLOR;
            button.colors = colors;
            button.onClick.AddListener(() => Select(column, deviceIndex));

            return dot.gameObject;
        }

        private static Image CreateCircleImage(string name, Transform parent, Sprite sprite, float size)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text CreateLabel(Transform parent, string text, Vector2 position, float width, TextAlignmentOptions alignment)
        {
            var labelObj = Instantiate(labelPrefab, parent);
            ConfigSettings.StripLocalization(labelObj);
            labelObj.SetActive(true);
            var label = labelObj.GetComponent<TMP_Text>();
            bool leftAligned = alignment == TextAlignmentOptions.MidlineLeft;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.pivot = new Vector2(leftAligned ? 0 : 0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(width, MAX_ROW_HEIGHT);
            label.rectTransform.anchoredPosition = position;
            label.enableAutoSizing = false;
            label.fontSize = 18;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private void Select(int column, int deviceIndex)
        {
            selection[column] = deviceIndex;
            if (deviceIndex > 0)
            {
                // A tracker can only drive one joint, the joint it is taken from is left to auto-detection.
                for (int other = 0; other < selection.Length; other++)
                {
                    if (other != column && selection[other] == deviceIndex)
                    {
                        selection[other] = AUTO;
                    }
                }
            }
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            foreach (var row in rows)
            {
                for (int column = 0; column < row.dots.Length; column++)
                {
                    row.dots[column].SetActive(selection[column] == row.deviceIndex);
                }
            }
        }

        private static void EnsureSprites()
        {
            if (ringSprite == null)
            {
                ringSprite = CreateCircleSprite(0.78f, 0.94f);
            }
            if (dotSprite == null)
            {
                dotSprite = CreateCircleSprite(0f, 0.5f);
            }
        }

        // An antialiased white ring (or disc, if innerRadius is 0), the radii being fractions of the sprite's half size.
        private static Sprite CreateCircleSprite(float innerRadius, float outerRadius)
        {
            var texture = new Texture2D(CIRCLE_TEXTURE_SIZE, CIRCLE_TEXTURE_SIZE, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            float halfSize = CIRCLE_TEXTURE_SIZE * 0.5f;
            var pixels = new Color[CIRCLE_TEXTURE_SIZE * CIRCLE_TEXTURE_SIZE];
            for (int y = 0; y < CIRCLE_TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < CIRCLE_TEXTURE_SIZE; x++)
                {
                    float distance = new Vector2(x + 0.5f - halfSize, y + 0.5f - halfSize).magnitude;
                    // Fades over one pixel at either edge.
                    float alpha = Mathf.Clamp01(outerRadius * halfSize - distance);
                    if (innerRadius > 0)
                    {
                        alpha *= Mathf.Clamp01(distance - innerRadius * halfSize);
                    }
                    pixels[y * CIRCLE_TEXTURE_SIZE + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, CIRCLE_TEXTURE_SIZE, CIRCLE_TEXTURE_SIZE), new Vector2(0.5f, 0.5f));
        }
    }
}
