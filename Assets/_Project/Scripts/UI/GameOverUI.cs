using UnityEngine;
using UnityEngine.UI;

namespace DevouringBeast
{
    public sealed class GameOverUI : MonoBehaviour
    {
        public static void Show()
        {
            if (FindFirstObjectByType<GameOverUI>() != null) return;
            RogueSkillCatalog catalog = Resources.Load<RogueSkillCatalog>("Rogue/RogueSkillCatalog");
            GameObject root = new("GameOverCanvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(GameOverUI));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject blocker = CreateImage("Blocker", root.transform, null, new Color(0f,0f,0f,0.72f));
            Stretch(blocker.GetComponent<RectTransform>());
            GameObject panel = CreateImage("ResultPanel", blocker.transform,
                catalog != null ? catalog.rogueSelectionBackground : null, Color.white);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.28f,0.22f); panelRect.anchorMax = new Vector2(0.72f,0.78f);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

            CreateText("Title", panel.transform, "挑战失败", 58, new Vector2(0.15f,0.68f), new Vector2(0.85f,0.9f));
            CreateText("ArchiveHint", panel.transform, "本次探险已记录到探险历程", 25,
                new Vector2(0.12f,0.48f), new Vector2(0.88f,0.62f));
            Button menu = CreateButton(panel.transform, "返回主菜单", catalog, new Vector2(0.18f,0.22f), new Vector2(0.82f,0.42f));
            menu.onClick.AddListener(GameManager.Instance.ReturnToMainMenu);
        }

        private static Button CreateButton(Transform parent, string label, RogueSkillCatalog catalog, Vector2 min, Vector2 max)
        {
            GameObject go = CreateImage(label, parent, catalog != null ? catalog.buttonBackground : null, Color.white);
            SetRect(go.GetComponent<RectTransform>(), min, max);
            Button button = go.AddComponent<Button>();
            go.AddComponent<UIButtonAudio>();
            CreateText("Label", go.transform, label, 34, Vector2.zero, Vector2.one);
            return button;
        }

        private static GameObject CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>(); image.sprite = sprite; image.color = color;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            return go;
        }

        private static void CreateText(string name, Transform parent, string value, int size, Vector2 min, Vector2 max)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent,false);
            SetRect(go.GetComponent<RectTransform>(), min, max);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value;
            text.fontSize = size; text.fontStyle = FontStyle.Bold; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.2f,0.12f,0.08f); text.raycastTarget = false;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static void Stretch(RectTransform rect) => SetRect(rect, Vector2.zero, Vector2.one);
    }
}
