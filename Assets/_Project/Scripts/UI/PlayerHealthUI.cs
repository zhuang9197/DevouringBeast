using UnityEngine;
using UnityEngine.UI;

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
        private Image _backgroundImage;

        private void Start()
        {
            RogueSkillCatalog catalog = Resources.Load<RogueSkillCatalog>("Rogue/RogueSkillCatalog");
            if (fillImage != null && catalog != null)
            {
                fillImage.sprite = catalog.healthFill;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                _backgroundImage = fillImage.transform.parent != null ? fillImage.transform.parent.GetComponent<Image>() : null;
                if (_backgroundImage != null) { _backgroundImage.sprite=catalog.healthBar; _backgroundImage.type=Image.Type.Sliced; _backgroundImage.color=Color.white; }
            }
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
            if (fillImage != null)
            {
                float pct = max > 0 ? (float)current / max : 0f;
                fillImage.fillAmount = pct;

                if (pct > 0.5f)
                    fillImage.color = Color.Lerp(Color.yellow, Color.green, (pct - 0.5f) * 2f);
                else
                    fillImage.color = Color.Lerp(Color.red, Color.yellow, pct * 2f);
            }

            // if (healthText != null)
            //     healthText.text = current + " / " + max;
        }

        private void OnDestroy()
        {
            if (_playerHealth != null)
                _playerHealth.OnHealthChanged.RemoveListener(UpdateHealth);
        }
    }
}
