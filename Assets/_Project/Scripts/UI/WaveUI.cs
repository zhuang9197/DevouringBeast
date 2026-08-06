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
        [SerializeField] private float warningThreshold = 3f;

        private WaveManager _waveManager;
        private Image _timerBackground;
        private GameObject _bossHealthBar;
        private Image _bossHealthFill;
        private int _lastWave = int.MinValue;
        private int _lastEnemies = int.MinValue;
        private int _lastTimerSeconds = int.MinValue;
        private float _lastTimerPercent = -1f;
        private bool _lastWarning;
        private bool _hasWarningState;
        private bool _lastBossVisible;
        private float _lastBossHealthPercent = -1f;

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
                BuildBossHealthBar();
            }
        }

        private void BuildBossHealthBar()
        {
            if (_timerBackground == null || _bossHealthBar != null) return;
            _bossHealthBar = Instantiate(_timerBackground.gameObject, _timerBackground.transform.parent);
            _bossHealthBar.name = "BossHealthBar";
            RectTransform rect = _bossHealthBar.GetComponent<RectTransform>();
            rect.anchoredPosition = _timerBackground.rectTransform.anchoredPosition + Vector2.down * 38f;

            Image background = _bossHealthBar.GetComponent<Image>();
            Image[] images = _bossHealthBar.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
                if (images[i] != background)
                {
                    _bossHealthFill = images[i];
                    break;
                }
            if (_bossHealthFill != null)
            {
                _bossHealthFill.sprite = timerBarImage.sprite;
                _bossHealthFill.type = Image.Type.Filled;
                _bossHealthFill.fillMethod = Image.FillMethod.Horizontal;
                _bossHealthFill.fillOrigin = 0;
                _bossHealthFill.color = warningColor;
                _bossHealthFill.raycastTarget = false;
            }
            if (background != null) background.raycastTarget = false;
            _bossHealthBar.SetActive(false);
        }

        private void Update()
        {
            if (_waveManager == null) return;

            UpdateUI(_waveManager.CurrentWave, _waveManager.EnemiesRemaining,
                     _waveManager.Timer, _waveManager.MaxTimer);
            UpdateBossHealth();
        }

        private void UpdateBossHealth()
        {
            if (_bossHealthBar == null) return;
            bool visible = _waveManager.ShouldShowBossHealth;
            if (visible != _lastBossVisible)
            {
                _bossHealthBar.SetActive(visible);
                _lastBossVisible = visible;
            }
            if (!visible || _bossHealthFill == null) return;
            float percent = _waveManager.BossHealthPercent;
            if (!Mathf.Approximately(percent, _lastBossHealthPercent))
            {
                _bossHealthFill.fillAmount = percent;
                _lastBossHealthPercent = percent;
            }
        }

        private void UpdateUI(int wave, int enemies, float timer, float maxTimer)
        {
            if (waveText != null && wave != _lastWave)
                waveText.text = "Floor " + wave;

            if (enemiesText != null && enemies != _lastEnemies)
                enemiesText.text = "Enemies: " + enemies;
            _lastWave = wave;
            _lastEnemies = enemies;

            // 用 sizeDelta 控制进度条宽度（从右向左缩短）
            float pct = maxTimer > 0 ? Mathf.Clamp01(timer / maxTimer) : 0f;
            if (timerBarImage != null && !Mathf.Approximately(pct, _lastTimerPercent))
                timerBarImage.fillAmount = pct;
            _lastTimerPercent = pct;

            // 颜色
            bool warning = timer <= warningThreshold;
            if (!_hasWarningState || warning != _lastWarning)
            {
                if (timerBarImage != null)
                    timerBarImage.color = warning ? warningColor : normalColor;
                if (timerText != null)
                    timerText.color = warning ? warningColor : Color.white;
                _lastWarning = warning;
                _hasWarningState = true;
            }

            int timerSeconds = Mathf.CeilToInt(timer);
            if (timerText != null && timerSeconds != _lastTimerSeconds)
            {
                timerText.text = _waveManager != null && _waveManager.IsCrisis ? "CRISIS" : timerSeconds + "s";
                _lastTimerSeconds = timerSeconds;
            }
        }
    }
}
