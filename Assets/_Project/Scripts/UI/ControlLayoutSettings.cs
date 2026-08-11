using UnityEngine;

namespace DevouringBeast
{
    public enum GameplayControlButton
    {
        Primary,
        Swallow
    }

    public static class ControlLayoutSettings
    {
        private const string Prefix = "DevouringBeast.ControlLayout.";
        private const float DefaultScale = 1f;

        public static float GetScale(GameplayControlButton button)
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(Key(button, "Scale"), DefaultScale), 0.65f, 1.5f);
        }

        public static Vector2 GetOffset(GameplayControlButton button)
        {
            return new Vector2(
                Mathf.Clamp(PlayerPrefs.GetFloat(Key(button, "OffsetX"), 0f), -180f, 180f),
                Mathf.Clamp(PlayerPrefs.GetFloat(Key(button, "OffsetY"), 0f), -180f, 180f));
        }

        public static void SetScale(GameplayControlButton button, float value)
        {
            PlayerPrefs.SetFloat(Key(button, "Scale"), Mathf.Clamp(value, 0.65f, 1.5f));
        }

        public static void SetOffset(GameplayControlButton button, Vector2 value)
        {
            PlayerPrefs.SetFloat(Key(button, "OffsetX"), Mathf.Clamp(value.x, -180f, 180f));
            PlayerPrefs.SetFloat(Key(button, "OffsetY"), Mathf.Clamp(value.y, -180f, 180f));
        }

        public static void Reset(GameplayControlButton button)
        {
            SetScale(button, DefaultScale);
            SetOffset(button, Vector2.zero);
        }

        public static void Apply(RectTransform rect, GameplayControlButton button)
        {
            if (rect == null) return;
            rect.anchoredPosition += GetOffset(button);
            rect.localScale *= GetScale(button);
        }

        public static void Save() => PlayerPrefs.Save();

        private static string Key(GameplayControlButton button, string property)
        {
            return Prefix + button + "." + property;
        }
    }
}
