using UnityEngine;
using UnityEngine.UI;

namespace DevouringBeast
{
    /// <summary>
    /// WaveUI — 波次信息显示
    /// 波次数 + 剩余敌人数 + 倒计时进度条（从右向左缩短，≤10s变红）
    /// </summary>
    public class WaveUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private Text waveText;
        [SerializeField] private Text enemiesText;
        [SerializeField] private RectTransform timerBarRect;
        [SerializeField] private Image timerBarImage;
        [SerializeField] private Text timerText;

        [Header("颜色")]
        [SerializeField] private Color normalColor = new(0.3f, 0.8f, 1f);
        [SerializeField] private Color warningColor = Color.red;
        [SerializeField] private float warningThreshold = 10f;

        [Header("进度条宽度")]
        [SerializeField] private float fullBarWidth = 580f;

        private WaveManager _waveManager;
        private Image _timerBackground;

        private void Start()
        {
            _waveManager = FindObjectOfType<WaveManager>();
            RogueSkillCatalog catalog = Resources.Load<RogueSkillCatalog>("Rogue/RogueSkillCatalog");
            if (timerBarImage != null && catalog != null)
            {
                timerBarImage.sprite=catalog.progressFill;
                timerBarImage.type=Image.Type.Filled;
                timerBarImage.fillMethod=Image.FillMethod.Horizontal;
                timerBarImage.fillOrigin=0;
                _timerBackground=timerBarImage.transform.parent != null ? timerBarImage.transform.parent.GetComponent<Image>() : null;
                if (_timerBackground != null) { _timerBackground.sprite=catalog.progressBar; _timerBackground.type=Image.Type.Sliced; _timerBackground.color=Color.white; }
            }
        }

        private void Update()
        {
            if (_waveManager == null) return;

            UpdateUI(_waveManager.CurrentWave, _waveManager.EnemiesRemaining,
                     _waveManager.Timer, _waveManager.MaxTimer);
        }

        private void UpdateUI(int wave, int enemies, float timer, float maxTimer)
        {
            if (waveText != null)
                waveText.text = "Wave " + wave;

            if (enemiesText != null)
                enemiesText.text = "Enemies: " + enemies;

            // 用 sizeDelta 控制进度条宽度（从右向左缩短）
            float pct = maxTimer > 0 ? Mathf.Clamp01(timer / maxTimer) : 0f;
            if (timerBarImage != null) timerBarImage.fillAmount = pct;

            // 颜色
            bool warning = timer <= warningThreshold;
            if (timerBarImage != null)
                timerBarImage.color = warning ? warningColor : normalColor;

            if (timerText != null)
            {
                timerText.text = Mathf.CeilToInt(timer) + "s";
                timerText.color = warning ? warningColor : Color.white;
            }
        }
    }
}
