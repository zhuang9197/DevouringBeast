using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace DevouringBeast
{
    /// <summary>
    /// PlayerHealthUI — 玩家血条 UI
    /// 订阅 PlayerHealth.OnHealthChanged 事件
    /// </summary>
    public class PlayerHealthUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private Image fillImage;
        //[SerializeField] private Text healthText;

        private PlayerHealth _playerHealth;
        private readonly List<Image> _hearts = new();
        private RogueSkillCatalog _catalog;

        private void Start()
        {
            _catalog = Resources.Load<RogueSkillCatalog>("Rogue/RogueSkillCatalog");
            if (fillImage != null && fillImage.transform.parent != null)
                fillImage.transform.parent.gameObject.SetActive(false);
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerHealth = player.GetComponent<PlayerHealth>();
                if (_playerHealth != null)
                {
                    _playerHealth.OnHealthChanged.AddListener(UpdateHealth);
                    UpdateHealth(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
                }
            }
        }

        private void UpdateHealth(int current, int max)
        {
            if (_catalog == null) return;
            int heartCount = Mathf.CeilToInt(Mathf.Max(0, max) / 2f);
            EnsureHeartCount(heartCount);
            for (int i = 0; i < _hearts.Count; i++)
            {
                Image heart = _hearts[i];
                bool visible = i < heartCount;
                heart.gameObject.SetActive(visible);
                if (!visible) continue;
                int value = Mathf.Clamp(current - i * 2, 0, 2);
                heart.sprite = value >= 2 ? _catalog.healthFull
                    : value == 1 ? _catalog.healthHalf : _catalog.healthEmpty;
            }
        }

        private void EnsureHeartCount(int count)
        {
            while (_hearts.Count < count)
            {
                int index = _hearts.Count;
                GameObject go = new GameObject($"Heart_{index + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.SetParent(transform, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(48f, 48f);
                rect.anchoredPosition = new Vector2(index * 50f, 0f);
                Image image = go.GetComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                _hearts.Add(image);
            }
        }

        private void OnDestroy()
        {
            if (_playerHealth != null)
                _playerHealth.OnHealthChanged.RemoveListener(UpdateHealth);
        }
    }
}
