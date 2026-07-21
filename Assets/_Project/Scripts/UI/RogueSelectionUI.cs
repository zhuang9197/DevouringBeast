using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DevouringBeast
{
    public sealed class RogueSelectionUI : MonoBehaviour
    {
        private RogueSkillManager _manager;
        private Font _font;
        private GameObject _descriptionOverlay;
        private Text _descriptionText;

        public static RogueSelectionUI Show(RogueSkillManager manager, RogueSkillCatalog catalog,
            IReadOnlyList<RogueSkillDefinition> choices)
        {
            RogueSelectionUI existing = FindFirstObjectByType<RogueSelectionUI>();
            if (existing != null) Destroy(existing.gameObject);

            GameObject root = new("RogueSelectionCanvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(RogueSelectionUI));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            RogueSelectionUI ui = root.GetComponent<RogueSelectionUI>();
            ui.Build(manager, catalog, choices);
            return ui;
        }

        private void Build(RogueSkillManager manager, RogueSkillCatalog catalog,
            IReadOnlyList<RogueSkillDefinition> choices)
        {
            _manager = manager;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject blocker = UiObject("Blocker", transform, typeof(Image));
            Stretch(blocker.GetComponent<RectTransform>());
            blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            GameObject panel = UiObject("RoguePanel", blocker.transform, typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.15f, 0.15f);
            panelRect.anchorMax = new Vector2(0.85f, 0.85f);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            Image panelImage = panel.GetComponent<Image>();
            panelImage.sprite = catalog.rogueSelectionBackground;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;

            Text title = CreateText("Title", panel.transform, "选择一项肉鸽技能", 42, FontStyle.Bold);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.2f, 0.82f);
            titleRect.anchorMax = new Vector2(0.8f, 0.96f);
            titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.18f, 0.12f, 0.09f);

            for (int i = 0; i < choices.Count; i++) CreateChoice(panel.transform, catalog, choices[i], i, choices.Count);
            CreateDescriptionOverlay(panel.transform);
        }

        private void CreateChoice(Transform parent, RogueSkillCatalog catalog, RogueSkillDefinition skill,
            int index, int count)
        {
            float gap = 0.025f;
            float width = (0.84f - gap * (count - 1)) / count;
            float x = 0.08f + index * (width + gap);
            GameObject card = UiObject("Skill_" + skill.id, parent, typeof(Image), typeof(Button),
                typeof(UIButtonAudio), typeof(RogueSkillCardInteraction));
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(x, 0.12f);
            rect.anchorMax = new Vector2(x + width, 0.78f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = card.GetComponent<Image>();
            image.sprite = catalog.buttonBackground;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 0.96f, 0.82f, 0.98f);
            card.GetComponent<RogueSkillCardInteraction>().Initialize(
                () => _manager.SelectSkill(skill),
                () => ShowDescription(skill),
                HideDescription);

            GameObject iconObject = UiObject("Icon", card.transform, typeof(Image));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.2f, 0.57f);
            iconRect.anchorMax = new Vector2(0.8f, 0.95f);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = catalog.GetIcon(skill.iconName);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            Text name = CreateText("Name", card.transform, skill.displayName, 30, FontStyle.Bold);
            SetRect(name.rectTransform, 0.06f, 0.43f, 0.94f, 0.58f);
            name.color = new Color(0.22f, 0.12f, 0.08f);
            name.alignment = TextAnchor.MiddleCenter;

            int current = _manager.GetLevel(skill.id);
            string levelLabel = current == 0 ? "新！" : current + " → " + (current + 1);
            Text level = CreateText("Level", card.transform, levelLabel, 25, FontStyle.Bold);
            SetRect(level.rectTransform, 0.08f, 0.32f, 0.92f, 0.44f);
            level.color = current == 0 ? new Color(0.8f, 0.18f, 0.08f) : new Color(0.35f, 0.2f, 0.08f);
            level.alignment = TextAnchor.MiddleCenter;

            Text description = CreateText("Description", card.transform, skill.description, 21, FontStyle.Normal);
            SetRect(description.rectTransform, 0.08f, 0.06f, 0.92f, 0.32f);
            description.alignment = TextAnchor.UpperCenter;
            description.color = new Color(0.18f, 0.13f, 0.1f);
        }

        private void CreateDescriptionOverlay(Transform parent)
        {
            _descriptionOverlay = UiObject("FullDescription", parent, typeof(Image));
            SetRect(_descriptionOverlay.GetComponent<RectTransform>(), 0.08f, 0.08f, 0.92f, 0.78f);
            Image background = _descriptionOverlay.GetComponent<Image>();
            background.color = new Color(0.08f, 0.06f, 0.04f, 0.94f);
            background.raycastTarget = false;

            _descriptionText = CreateText("Text", _descriptionOverlay.transform, string.Empty, 30, FontStyle.Normal);
            SetRect(_descriptionText.rectTransform, 0.06f, 0.08f, 0.94f, 0.92f);
            _descriptionText.alignment = TextAnchor.MiddleCenter;
            _descriptionText.color = Color.white;
            _descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
            _descriptionOverlay.SetActive(false);
        }

        private void ShowDescription(RogueSkillDefinition skill)
        {
            if (_descriptionOverlay == null || _descriptionText == null || skill == null) return;
            _descriptionText.text = skill.displayName + "\n\n" + skill.description;
            _descriptionOverlay.SetActive(true);
            _descriptionOverlay.transform.SetAsLastSibling();
        }

        private void HideDescription()
        {
            if (_descriptionOverlay != null) _descriptionOverlay.SetActive(false);
        }

        private Text CreateText(string name, Transform parent, string value, int size, FontStyle style)
        {
            GameObject go = UiObject(name, parent, typeof(Text));
            Text text = go.GetComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject UiObject(string name, Transform parent, params System.Type[] components)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            foreach (System.Type type in components) if (type != typeof(RectTransform)) go.AddComponent(type);
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
