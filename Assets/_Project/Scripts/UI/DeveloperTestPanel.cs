using System;
using UnityEngine;
using UnityEngine.UI;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class DeveloperTestPanel : MonoBehaviour
    {
        private EnemyArchetype[] _archetypes;
        private int _selectedIndex;
        private Text _selectionText;
        private Text _statusText;
        private Button _spawnButton;

        public static DeveloperTestPanel EnsureFor(FloorMapManager map)
        {
            DeveloperTestPanel existing = map.GetComponent<DeveloperTestPanel>();
            return existing != null ? existing : map.gameObject.AddComponent<DeveloperTestPanel>();
        }

        private void Start()
        {
            _archetypes = (EnemyArchetype[])Enum.GetValues(typeof(EnemyArchetype));
            BuildUi();
            RefreshSelection();
        }

        private void Update()
        {
            if (_spawnButton == null) return;
            bool ready = WaveManager.Instance != null && WaveManager.Instance.IsReady;
            _spawnButton.interactable = ready;
            if (_statusText != null)
                _statusText.text = ready ? "\u6d4b\u8bd5\u6a21\u5f0f" : "\u6b63\u5728\u52a0\u8f7d\u602a\u7269\u8d44\u6e90...";
        }

        private void BuildUi()
        {
            Canvas canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>() ?? FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            GameObject panel = new("DeveloperTestPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 0.5f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.anchoredPosition = new Vector2(18f, -70f);
            panelRect.sizeDelta = new Vector2(280f, 238f);
            panel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.92f);

            Text title = CreateText("Title", panel.transform, 20, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.98f));
            title.text = "\u602a\u7269\u4e0e\u6280\u80fd\u6d4b\u8bd5";

            Button previous = CreateButton("Previous", panel.transform, new Vector2(0.05f, 0.62f),
                new Vector2(0.2f, 0.8f), "<");
            Button next = CreateButton("Next", panel.transform, new Vector2(0.8f, 0.62f),
                new Vector2(0.95f, 0.8f), ">");
            _selectionText = CreateText("Selection", panel.transform, 16, TextAnchor.MiddleCenter);
            SetRect(_selectionText.rectTransform, new Vector2(0.22f, 0.62f), new Vector2(0.78f, 0.8f));
            previous.onClick.AddListener(() => ChangeSelection(-1));
            next.onClick.AddListener(() => ChangeSelection(1));

            _spawnButton = CreateButton("Spawn", panel.transform, new Vector2(0.05f, 0.43f),
                new Vector2(0.95f, 0.59f), "\u751f\u6210\u6240\u9009\u602a\u7269");
            Button levelUp = CreateButton("LevelUp", panel.transform, new Vector2(0.05f, 0.24f),
                new Vector2(0.47f, 0.4f), "\u5347\u7ea7");
            Button reset = CreateButton("Reset", panel.transform, new Vector2(0.53f, 0.24f),
                new Vector2(0.95f, 0.4f), "\u91cd\u7f6e");
            _spawnButton.onClick.AddListener(SpawnSelected);
            levelUp.onClick.AddListener(TriggerLevelUp);
            reset.onClick.AddListener(ResetProgress);

            _statusText = CreateText("Status", panel.transform, 13, TextAnchor.MiddleCenter);
            SetRect(_statusText.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.2f));
        }

        private void ChangeSelection(int delta)
        {
            if (_archetypes == null || _archetypes.Length == 0) return;
            _selectedIndex = (_selectedIndex + delta + _archetypes.Length) % _archetypes.Length;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (_selectionText != null && _archetypes != null && _archetypes.Length > 0)
                _selectionText.text = _archetypes[_selectedIndex].ToString();
        }

        private void SpawnSelected()
        {
            WaveManager waves = WaveManager.Instance;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (waves == null || !waves.IsReady || player == null) return;
            Vector2 position = (Vector2)player.transform.position + Vector2.right * 4f;
            if (MapBounds.Instance != null) position = MapBounds.Instance.ClampPosition(position);
            waves.SpawnSummoned(_archetypes[_selectedIndex], position);
        }

        private static void TriggerLevelUp()
        {
            RogueSkillManager skills = RogueSkillManager.Active;
            SwallowContainer container = skills != null ? skills.GetComponent<SwallowContainer>() : null;
            if (container == null || skills.IsChoiceOpen) return;
            container.AddProgress(Mathf.Max(0f, container.RequiredMass - container.CurrentMass));
        }

        private static void ResetProgress()
        {
            RogueSkillManager skills = RogueSkillManager.Active;
            SwallowContainer container = skills != null ? skills.GetComponent<SwallowContainer>() : null;
            skills?.ResetForTesting();
            container?.ResetForTesting();
        }

        private static Button CreateButton(string name, Transform parent, Vector2 min, Vector2 max, string label)
        {
            GameObject owner = new(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIButtonAudio));
            owner.transform.SetParent(parent, false);
            SetRect(owner.GetComponent<RectTransform>(), min, max);
            owner.GetComponent<Image>().color = new Color(0.2f, 0.24f, 0.3f, 1f);
            Text text = CreateText("Label", owner.transform, 16, TextAnchor.MiddleCenter);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one);
            text.text = label;
            return owner.GetComponent<Button>();
        }

        private static Text CreateText(string name, Transform parent, int size, TextAnchor alignment)
        {
            GameObject owner = new(name, typeof(RectTransform), typeof(Text));
            owner.transform.SetParent(parent, false);
            Text text = owner.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
