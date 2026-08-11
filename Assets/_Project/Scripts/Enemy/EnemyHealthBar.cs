using UnityEngine;

namespace DevouringBeast
{
    /// <summary>使用共享 SpriteRenderer 的敌人世界空间血条，避免每个敌人创建独立 Canvas。</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer fillRenderer;
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private GameObject container;
        [SerializeField] private Vector3 offset = new(0f, 1.2f, 0f);
        [SerializeField] private float fullWidth = 1.5f;
        [SerializeField] private float fillHeight = 0.11f;
        [SerializeField] private float horizontalInset = 0.025f;

        private EnemyBase _enemy;
        private float _lastHealthPercent = -1f;
        private float _lastCounterScaleX;
        private float _nextRefreshTime;
        private bool _visualsVisible = true;

        public static EnemyHealthBar EnsureFor(EnemyBase enemy)
        {
            if (enemy == null)
                return null;

            EnemyHealthBar existing = enemy.GetComponentInChildren<EnemyHealthBar>(true);
            if (existing != null)
                return existing;

            Bounds visualBounds = new(enemy.transform.position, Vector3.zero);
            bool hasBounds = false;
            SpriteRenderer[] spriteRenderers = enemy.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = spriteRenderers[i];
                if (renderer.sprite == null)
                    continue;

                if (!hasBounds)
                {
                    visualBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    visualBounds.Encapsulate(renderer.bounds);
                }
            }

            float top = hasBounds ? visualBounds.max.y - enemy.transform.position.y : 1f;
            float width = hasBounds ? Mathf.Clamp(visualBounds.size.x * 0.9f, 1.2f, 2.5f) : 1.5f;
            Vector3 barOffset = new(0f, top + 0.25f, 0f);
            RogueSkillCatalog catalog = Resources.Load<RogueSkillCatalog>("Rogue/RogueSkillCatalog");

            GameObject barObject = new("EnemyHealthBar");
            barObject.transform.SetParent(enemy.transform, false);
            barObject.transform.localPosition = barOffset;

            GameObject backgroundObject = new("Background", typeof(SpriteRenderer));
            backgroundObject.transform.SetParent(barObject.transform, false);
            SpriteRenderer background = backgroundObject.GetComponent<SpriteRenderer>();
            background.sprite = catalog != null ? catalog.healthBar : null;
            background.drawMode = SpriteDrawMode.Simple;
            SetSimpleSize(background, new Vector2(width, 0.16f));
            background.sortingOrder = 100;

            GameObject fillObject = new("Fill", typeof(SpriteRenderer));
            fillObject.transform.SetParent(barObject.transform, false);
            SpriteRenderer fill = fillObject.GetComponent<SpriteRenderer>();
            fill.sprite = catalog != null ? catalog.healthFill : null;
            fill.drawMode = SpriteDrawMode.Simple;
            fill.sortingOrder = 101;

            EnemyHealthBar healthBar = barObject.AddComponent<EnemyHealthBar>();
            healthBar._enemy = enemy;
            healthBar.offset = barOffset;
            healthBar.fullWidth = width;
            healthBar.fillRenderer = fill;
            healthBar.backgroundRenderer = background;
            healthBar.container = barObject;
            healthBar.Refresh(true);
            return healthBar;
        }

        private static void SetSimpleSize(SpriteRenderer renderer, Vector2 targetSize)
        {
            if (renderer == null || renderer.sprite == null)
                return;

            Vector2 spriteSize = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(
                spriteSize.x > 0f ? targetSize.x / spriteSize.x : 1f,
                spriteSize.y > 0f ? targetSize.y / spriteSize.y : 1f,
                1f);
        }

        private void Awake()
        {
            if (_enemy == null)
                _enemy = GetComponentInParent<EnemyBase>();
        }

        private void LateUpdate()
        {
            if (_enemy == null)
                return;
            bool visible = !_enemy.IsDead && _enemy.IsVisible;
            SetVisualsVisible(visible);
            if (!visible)
                return;
            if (Time.unscaledTime < _nextRefreshTime)
                return;
            _nextRefreshTime = Time.unscaledTime + 0.1f;

            // EnemyAI 通过负 X 缩放转向；反向缩放血条，避免血量方向随朝向翻转。
            float counterScaleX = _enemy.transform.lossyScale.x < 0f ? -1f : 1f;
            if (!Mathf.Approximately(counterScaleX, _lastCounterScaleX))
            {
                transform.localScale = new Vector3(counterScaleX, 1f, 1f);
                _lastCounterScaleX = counterScaleX;
            }

            Refresh(false);
        }

        private void SetVisualsVisible(bool visible)
        {
            if (_visualsVisible == visible) return;
            _visualsVisible = visible;
            if (backgroundRenderer != null) backgroundRenderer.enabled = visible;
            if (fillRenderer != null) fillRenderer.enabled = visible && _lastHealthPercent > 0.001f;
            if (visible) Refresh(true);
        }

        private void Refresh(bool force)
        {
            if (_enemy == null)
                return;

            if (_enemy.IsDead)
            {
                if (container != null)
                    container.SetActive(false);
                return;
            }

            float healthPercent = Mathf.Clamp01(_enemy.HealthPercent);
            if (!force && Mathf.Approximately(healthPercent, _lastHealthPercent))
                return;

            _lastHealthPercent = healthPercent;
            if (fillRenderer == null)
                return;

            float innerWidth = Mathf.Max(0f, fullWidth - horizontalInset * 2f);
            float fillWidth = innerWidth * healthPercent;
            SetSimpleSize(fillRenderer, new Vector2(fillWidth, fillHeight));
            fillRenderer.transform.localPosition = new Vector3(
                -fullWidth * 0.5f + horizontalInset + fillWidth * 0.5f, 0f, -0.01f);
            fillRenderer.enabled = fillWidth > 0.001f;
        }

        public void ResetForReuse()
        {
            if (container != null)
                container.SetActive(true);
            _visualsVisible = true;
            if (backgroundRenderer != null) backgroundRenderer.enabled = true;
            _lastHealthPercent = -1f;
            _nextRefreshTime = 0f;
            Refresh(true);
        }
    }
}
