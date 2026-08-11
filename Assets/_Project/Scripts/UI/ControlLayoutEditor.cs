using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class ControlLayoutEditor : MonoBehaviour
    {
        private const float PreviewOffsetScale = 0.35f;

        private RogueSkillCatalog _catalog;
        private GameObject _editorPanel;
        private Slider _scaleSlider;
        private Slider _offsetXSlider;
        private Slider _offsetYSlider;
        private Text _scaleValue;
        private Text _offsetXValue;
        private Text _offsetYValue;
        private RectTransform _primaryPreview;
        private RectTransform _swallowPreview;
        private Image _primaryTargetImage;
        private Image _swallowTargetImage;
        private readonly List<GameObject> _hiddenOptionsChildren = new();
        private GameplayControlButton _selected = GameplayControlButton.Primary;
        private bool _updating;

        public void Initialize()
        {
            if (_editorPanel != null) return;
            _catalog = Resources.Load<RogueSkillCatalog>("Rogue/RogueSkillCatalog");
            BuildOpenButton();
            BuildEditorPanel();
            SelectButton(GameplayControlButton.Primary);
            _editorPanel.SetActive(false);
        }

        public void CloseEditor()
        {
            if (_editorPanel != null) _editorPanel.SetActive(false);
            foreach (GameObject child in _hiddenOptionsChildren)
                if (child != null) child.SetActive(true);
            _hiddenOptionsChildren.Clear();
            ControlLayoutSettings.Save();
        }

        private void BuildOpenButton()
        {
            if (transform.Find("ControlLayoutButton") != null) return;
            Transform legacyClose = transform.Find("BorderButton");
            if (legacyClose != null && legacyClose is RectTransform closeRect)
                closeRect.anchoredPosition = new Vector2(0f, -215f);

            Button button = CreateButton("ControlLayoutButton", transform, new Vector2(0f, -125f),
                new Vector2(300f, 64f), "\u64cd\u4f5c\u6309\u94ae\u5e03\u5c40");
            button.onClick.AddListener(OpenEditor);
        }

        private void BuildEditorPanel()
        {
            _editorPanel = new GameObject("ControlLayoutPanel", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(CanvasGroup));
            _editorPanel.transform.SetParent(transform, false);
            Stretch(_editorPanel.GetComponent<RectTransform>());
            Image background = _editorPanel.GetComponent<Image>();
            background.color = new Color(0.055f, 0.065f, 0.085f, 0.99f);
            background.raycastTarget = true;
            CanvasGroup group = _editorPanel.GetComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;

            Text title = CreateText("Title", _editorPanel.transform, new Vector2(0f, 238f),
                new Vector2(520f, 44f), 26, "\u64cd\u4f5c\u6309\u94ae\u5e03\u5c40");
            title.fontStyle = FontStyle.Bold;

            Button primaryTarget = CreateButton("PrimaryTarget", _editorPanel.transform,
                new Vector2(-105f, 190f), new Vector2(190f, 48f), "\u5438\u5165 / \u5410\u51fa");
            Button swallowTarget = CreateButton("SwallowTarget", _editorPanel.transform,
                new Vector2(105f, 190f), new Vector2(190f, 48f), "\u541e\u566c");
            _primaryTargetImage = primaryTarget.GetComponent<Image>();
            _swallowTargetImage = swallowTarget.GetComponent<Image>();
            primaryTarget.onClick.AddListener(() => SelectButton(GameplayControlButton.Primary));
            swallowTarget.onClick.AddListener(() => SelectButton(GameplayControlButton.Swallow));

            GameObject preview = new("Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            preview.transform.SetParent(_editorPanel.transform, false);
            RectTransform previewRect = preview.GetComponent<RectTransform>();
            previewRect.anchorMin = previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.anchoredPosition = new Vector2(0f, 70f);
            previewRect.sizeDelta = new Vector2(640f, 180f);
            preview.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);
            Text previewLabel = CreateText("PreviewLabel", preview.transform, new Vector2(-254f, 65f),
                new Vector2(100f, 28f), 14, "\u4f4d\u7f6e\u9884\u89c8");
            previewLabel.alignment = TextAnchor.MiddleLeft;
            _primaryPreview = CreatePreviewButton("PrimaryPreview", preview.transform,
                _catalog != null ? _catalog.suckButton : null, new Vector2(210f, -8f), 76f);
            _swallowPreview = CreatePreviewButton("SwallowPreview", preview.transform,
                _catalog != null ? _catalog.swallowButton : null, new Vector2(132f, -62f), 70f);

            _scaleSlider = CreateSettingSlider("Scale", -50f, 0.65f, 1.5f,
                "\u5927\u5c0f", out _scaleValue);
            _offsetXSlider = CreateSettingSlider("OffsetX", -112f, -180f, 180f,
                "\u6c34\u5e73\u504f\u79fb", out _offsetXValue);
            _offsetYSlider = CreateSettingSlider("OffsetY", -174f, -180f, 180f,
                "\u5782\u76f4\u504f\u79fb", out _offsetYValue);
            _scaleSlider.onValueChanged.AddListener(OnScaleChanged);
            _offsetXSlider.onValueChanged.AddListener(_ => OnOffsetChanged());
            _offsetYSlider.onValueChanged.AddListener(_ => OnOffsetChanged());

            Button reset = CreateButton("Reset", _editorPanel.transform, new Vector2(-95f, -235f),
                new Vector2(170f, 50f), "\u6062\u590d\u9ed8\u8ba4");
            Button close = CreateButton("Close", _editorPanel.transform, new Vector2(95f, -235f),
                new Vector2(170f, 50f), "\u5b8c\u6210");
            reset.onClick.AddListener(ResetSelected);
            close.onClick.AddListener(CloseEditor);
        }

        private void OpenEditor()
        {
            if (_editorPanel == null) return;
            _hiddenOptionsChildren.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child == _editorPanel || !child.activeSelf) continue;
                child.SetActive(false);
                _hiddenOptionsChildren.Add(child);
            }
            _editorPanel.transform.SetAsLastSibling();
            _editorPanel.SetActive(true);
            SelectButton(_selected);
        }

        private void SelectButton(GameplayControlButton button)
        {
            _selected = button;
            _updating = true;
            _scaleSlider.SetValueWithoutNotify(ControlLayoutSettings.GetScale(button));
            Vector2 offset = ControlLayoutSettings.GetOffset(button);
            _offsetXSlider.SetValueWithoutNotify(offset.x);
            _offsetYSlider.SetValueWithoutNotify(offset.y);
            _updating = false;
            RefreshTargetVisuals();
            RefreshPreview();
        }

        private void OnScaleChanged(float value)
        {
            if (_updating) return;
            ControlLayoutSettings.SetScale(_selected, value);
            RefreshPreview();
        }

        private void OnOffsetChanged()
        {
            if (_updating) return;
            ControlLayoutSettings.SetOffset(_selected,
                new Vector2(_offsetXSlider.value, _offsetYSlider.value));
            RefreshPreview();
        }

        private void ResetSelected()
        {
            ControlLayoutSettings.Reset(_selected);
            SelectButton(_selected);
        }

        private void RefreshTargetVisuals()
        {
            Color selected = new(0.95f, 0.76f, 0.28f, 1f);
            Color normal = Color.white;
            if (_primaryTargetImage != null)
                _primaryTargetImage.color = _selected == GameplayControlButton.Primary ? selected : normal;
            if (_swallowTargetImage != null)
                _swallowTargetImage.color = _selected == GameplayControlButton.Swallow ? selected : normal;
        }

        private void RefreshPreview()
        {
            ApplyPreview(_primaryPreview, GameplayControlButton.Primary, new Vector2(210f, -8f));
            ApplyPreview(_swallowPreview, GameplayControlButton.Swallow, new Vector2(132f, -62f));
            if (_scaleValue != null)
                _scaleValue.text = Mathf.RoundToInt(ControlLayoutSettings.GetScale(_selected) * 100f) + "%";
            Vector2 offset = ControlLayoutSettings.GetOffset(_selected);
            if (_offsetXValue != null) _offsetXValue.text = Mathf.RoundToInt(offset.x).ToString();
            if (_offsetYValue != null) _offsetYValue.text = Mathf.RoundToInt(offset.y).ToString();
        }

        private static void ApplyPreview(RectTransform rect, GameplayControlButton button, Vector2 basePosition)
        {
            if (rect == null) return;
            rect.anchoredPosition = basePosition + ControlLayoutSettings.GetOffset(button) * PreviewOffsetScale;
            rect.localScale = Vector3.one * ControlLayoutSettings.GetScale(button);
        }

        private Slider CreateSettingSlider(string name, float y, float minimum, float maximum,
            string labelText, out Text valueText)
        {
            Text label = CreateText(name + "Label", _editorPanel.transform, new Vector2(-260f, y),
                new Vector2(150f, 42f), 18, labelText);
            label.alignment = TextAnchor.MiddleRight;

            GameObject sliderObject = new(name, typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(_editorPanel.transform, false);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(25f, y);
            sliderRect.sizeDelta = new Vector2(360f, 42f);

            GameObject track = CreateImage("Background", sliderObject.transform, new Color(0.25f, 0.28f, 0.34f, 1f));
            RectTransform trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.sizeDelta = new Vector2(0f, 10f);

            GameObject fillArea = new("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRect.offsetMin = new Vector2(8f, -5f);
            fillAreaRect.offsetMax = new Vector2(-8f, 5f);
            GameObject fill = CreateImage("Fill", fillArea.transform, new Color(0.95f, 0.67f, 0.2f, 1f));
            Stretch(fill.GetComponent<RectTransform>());

            GameObject handleArea = new("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>());
            GameObject handle = CreateImage("Handle", handleArea.transform, Color.white);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(30f, 42f);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;

            valueText = CreateText(name + "Value", _editorPanel.transform, new Vector2(284f, y),
                new Vector2(78f, 42f), 18, string.Empty);
            return slider;
        }

        private RectTransform CreatePreviewButton(string name, Transform parent, Sprite sprite,
            Vector2 position, float size)
        {
            GameObject button = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            button.transform.SetParent(parent, false);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.one * size;
            Image image = button.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = sprite != null ? Color.white : new Color(0.95f, 0.67f, 0.2f, 1f);
            return rect;
        }

        private Button CreateButton(string name, Transform parent, Vector2 position, Vector2 size, string text)
        {
            GameObject buttonObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button), typeof(UIButtonAudio));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.sprite = _catalog != null ? _catalog.buttonBackground : null;
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = image.sprite != null ? Color.white : new Color(0.2f, 0.23f, 0.28f, 1f);
            Text label = CreateText("Label", buttonObject.transform, Vector2.zero, Vector2.zero, 18, text);
            Stretch(label.rectTransform);
            return buttonObject.GetComponent<Button>();
        }

        private static GameObject CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return imageObject;
        }

        private static Text CreateText(string name, Transform parent, Vector2 position, Vector2 size,
            int fontSize, string value)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnDestroy() => ControlLayoutSettings.Save();
    }
}
