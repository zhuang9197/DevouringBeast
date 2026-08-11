using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DevouringBeast
{
    public sealed class VictoryUI : MonoBehaviour
    {
        public static void Show(CompletedRunData history)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null || GameObject.Find("VictoryPanel") != null) return;
            GameObject panel = new("VictoryPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(canvas.transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f, 0.94f);

            Text title = CreateText(panel.transform, "\u901a\u5173\u6210\u529f", 46, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.2f, 0.58f), new Vector2(0.8f, 0.78f));
            float time = history != null ? history.clearTimeSeconds : 0f;
            int health = history != null ? history.healthSpent : 0;
            Text detail = CreateText(panel.transform,
                string.Format("\u901a\u5173\u65f6\u95f4 {0:0.0}s\n\u6d88\u8017\u8840\u91cf {1}", time, health), 24, TextAnchor.MiddleCenter);
            SetRect(detail.rectTransform, new Vector2(0.2f, 0.38f), new Vector2(0.8f, 0.58f));
            GameObject buttonObject = new("ReturnToMenu", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panel.transform, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            SetRect(buttonRect, new Vector2(0.35f, 0.18f), new Vector2(0.65f, 0.31f));
            buttonObject.GetComponent<Image>().color = new Color(0.2f, 0.65f, 0.6f, 1f);
            Text buttonText = CreateText(buttonObject.transform, "\u8fd4\u56de\u4e3b\u9875", 24, TextAnchor.MiddleCenter);
            SetRect(buttonText.rectTransform, Vector2.zero, Vector2.one);
            buttonObject.GetComponent<Button>().onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneNames.Menu);
            });
        }

        private static Text CreateText(Transform parent, string value, int size, TextAnchor anchor)
        {
            GameObject go = new("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = value;
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
