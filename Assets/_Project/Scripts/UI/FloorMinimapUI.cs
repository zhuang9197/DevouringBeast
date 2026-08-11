using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class FloorMinimapUI : MonoBehaviour
    {
        private sealed class RoomVisual
        {
            public Image Tile;
            public Outline Outline;
            public GameObject PlayerMarker;
        }

        [SerializeField] private Vector2 panelSize = new(230f, 230f);
        [SerializeField] private Vector2 screenOffset = new(-24f, -24f);
        [SerializeField] private Color panelColor = new(0.035f, 0.045f, 0.055f, 0.78f);
        [SerializeField] private Color unexploredColor = Color.black;
        [SerializeField] private Color adjacentColor = new(0.31f, 0.35f, 0.38f, 0.95f);
        [SerializeField] private Color visitedColor = new(0.18f, 0.68f, 0.62f, 1f);
        [SerializeField] private Color clearedColor = new(0.3f, 0.82f, 0.55f, 1f);
        [SerializeField] private Color currentColor = new(1f, 0.72f, 0.16f, 1f);
        [SerializeField] private Color adjacentOutlineColor = new(0.72f, 0.84f, 0.9f, 0.95f);
        [SerializeField] private Color demonColor = new(0.9f, 0.14f, 0.12f, 1f);
        [SerializeField] private Color floorExitColor = new(0.15f, 0.55f, 1f, 1f);

        private readonly List<RoomVisual> _rooms = new(10);
        private FloorMapManager _map;
        private RectTransform _content;
        private int _layoutVersion = -1;

        public static FloorMinimapUI EnsureFor(FloorMapManager map)
        {
            FloorMinimapUI existing = map.GetComponent<FloorMinimapUI>();
            return existing != null ? existing : map.gameObject.AddComponent<FloorMinimapUI>();
        }

        private void Awake()
        {
            _map = GetComponent<FloorMapManager>();
        }

        private void OnEnable()
        {
            if (_map == null) _map = GetComponent<FloorMapManager>();
            if (_map != null) _map.MinimapChanged += Refresh;
        }

        private void Start()
        {
            BuildPanel();
            Refresh();
        }

        private void OnDisable()
        {
            if (_map != null) _map.MinimapChanged -= Refresh;
        }

        private void BuildPanel()
        {
            if (_content != null) return;
            GameObject canvasObject = GameObject.Find("Canvas");
            Canvas canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject panel = new("FloorMinimap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = Vector2.one;
            panelRect.pivot = Vector2.one;
            panelRect.anchoredPosition = screenOffset;
            panelRect.sizeDelta = panelSize;
            Image background = panel.GetComponent<Image>();
            background.color = panelColor;
            background.raycastTarget = false;

            GameObject content = new("Rooms", typeof(RectTransform));
            content.transform.SetParent(panel.transform, false);
            _content = content.GetComponent<RectTransform>();
            _content.anchorMin = Vector2.zero;
            _content.anchorMax = Vector2.one;
            _content.offsetMin = new Vector2(16f, 16f);
            _content.offsetMax = new Vector2(-16f, -16f);
        }

        private void Refresh()
        {
            if (_map == null || _content == null) return;
            if (_layoutVersion != _map.LayoutVersion) RebuildLayout();
            for (int i = 0; i < _rooms.Count; i++)
            {
                if (!_map.TryGetMinimapRoom(i, out _, out bool visited, out bool cleared,
                    out bool current, out bool adjacent, out bool demon, out bool floorExit)) continue;
                RoomVisual visual = _rooms[i];
                visual.Tile.color = !visited ? unexploredColor : floorExit ? floorExitColor : demon ? demonColor :
                    current ? currentColor : cleared ? clearedColor : visitedColor;
                visual.Outline.enabled = adjacent || current;
                visual.Outline.effectColor = current ? Color.white : adjacentOutlineColor;
                visual.PlayerMarker.SetActive(current);
            }
        }

        private void RebuildLayout()
        {
            foreach (RoomVisual room in _rooms)
                if (room.Tile != null) Destroy(room.Tile.gameObject);
            _rooms.Clear();
            _layoutVersion = _map.LayoutVersion;
            if (_map.MinimapRoomCount <= 0) return;

            Vector2Int minimum = new(int.MaxValue, int.MaxValue);
            Vector2Int maximum = new(int.MinValue, int.MinValue);
            for (int i = 0; i < _map.MinimapRoomCount; i++)
            {
                if (!_map.TryGetMinimapRoom(i, out Vector2Int cell, out _, out _, out _, out _)) continue;
                minimum = Vector2Int.Min(minimum, cell);
                maximum = Vector2Int.Max(maximum, cell);
            }

            int spanX = Mathf.Max(1, maximum.x - minimum.x + 1);
            int spanY = Mathf.Max(1, maximum.y - minimum.y + 1);
            float usable = Mathf.Min(panelSize.x, panelSize.y) - 44f;
            float pitch = Mathf.Min(30f, usable / Mathf.Max(spanX, spanY));
            float tileSize = Mathf.Clamp(pitch * 0.7f, 11f, 22f);
            Vector2 center = new((minimum.x + maximum.x) * 0.5f, (minimum.y + maximum.y) * 0.5f);

            for (int i = 0; i < _map.MinimapRoomCount; i++)
            {
                _map.TryGetMinimapRoom(i, out Vector2Int cell, out _, out _, out _, out _);
                GameObject tileObject = new($"Room_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
                tileObject.transform.SetParent(_content, false);
                RectTransform rect = tileObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * tileSize;
                rect.anchoredPosition = new Vector2((cell.x - center.x) * pitch, (cell.y - center.y) * pitch);
                Image tile = tileObject.GetComponent<Image>();
                tile.raycastTarget = false;
                Outline outline = tileObject.GetComponent<Outline>();
                outline.effectDistance = new Vector2(2f, -2f);
                outline.useGraphicAlpha = false;

                GameObject marker = new("Player", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                marker.transform.SetParent(tileObject.transform, false);
                RectTransform markerRect = marker.GetComponent<RectTransform>();
                markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);
                markerRect.sizeDelta = Vector2.one * Mathf.Max(5f, tileSize * 0.34f);
                markerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Image markerImage = marker.GetComponent<Image>();
                markerImage.color = Color.white;
                markerImage.raycastTarget = false;
                _rooms.Add(new RoomVisual { Tile = tile, Outline = outline, PlayerMarker = marker });
            }
        }
    }
}
