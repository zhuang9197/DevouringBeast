using UnityEngine;
using UnityEngine.UI;

namespace DevouringBeast
{
    /// <summary>跟随敌人头部、实时反映当前生命值的世界空间血条。</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private RectTransform fillRect;
        [SerializeField] private GameObject container;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.2f, 0f);

        private EnemyBase _enemy;

        public static EnemyHealthBar EnsureFor(EnemyBase enemy)
        {
            if (enemy == null)
                return null;

            EnemyHealthBar existing = enemy.GetComponentInChildren<EnemyHealthBar>(true);
            if (existing != null)
                return existing;

            Bounds visualBounds = new Bounds(enemy.transform.position, Vector3.zero);
            bool hasBounds = false;
            SpriteRenderer[] spriteRenderers = enemy.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i].sprite == null)
                    continue;

                if (!hasBounds)
                {
                    visualBounds = spriteRenderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    visualBounds.Encapsulate(spriteRenderers[i].bounds);
                }
            }

            float top = hasBounds
                ? visualBounds.max.y - enemy.transform.position.y
                : 1f;
            float width = hasBounds
                ? Mathf.Clamp(visualBounds.size.x * 0.9f, 1.2f, 2.5f)
                : 1.5f;
            Vector3 barOffset = new Vector3(0f, top + 0.25f, 0f);

            GameObject barObject = new GameObject("EnemyHealthBar", typeof(RectTransform));
            barObject.transform.SetParent(enemy.transform, false);
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.localPosition = barOffset;
            barRect.localRotation = Quaternion.identity;
            barRect.localScale = Vector3.one;
            barRect.sizeDelta = new Vector2(width, 0.16f);

            Canvas canvas = barObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

            GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(barObject.transform, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            Stretch(backgroundRect);
            Image background = backgroundObject.GetComponent<Image>();
            background.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            background.raycastTarget = false;

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(backgroundObject.transform, false);
            RectTransform healthFillRect = fillObject.GetComponent<RectTransform>();
            healthFillRect.anchorMin = Vector2.zero;
            healthFillRect.anchorMax = Vector2.one;
            healthFillRect.pivot = new Vector2(0f, 0.5f);
            healthFillRect.offsetMin = new Vector2(0.025f, 0.025f);
            healthFillRect.offsetMax = new Vector2(-0.025f, -0.025f);
            Image healthFill = fillObject.GetComponent<Image>();
            healthFill.color = Color.green;
            healthFill.raycastTarget = false;

            EnemyHealthBar healthBar = barObject.AddComponent<EnemyHealthBar>();
            healthBar._enemy = enemy;
            healthBar.offset = barOffset;
            healthBar.fillImage = healthFill;
            healthBar.fillRect = healthFillRect;
            healthBar.container = barObject;
            healthBar.Refresh();
            return healthBar;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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

            transform.position = _enemy.transform.position + offset;
            transform.rotation = Quaternion.identity;
            Refresh();
        }

        private void Refresh()
        {
            if (_enemy == null)
                return;

            float healthPercent = Mathf.Clamp01(_enemy.HealthPercent);
            if (fillRect != null)
            {
                fillRect.anchorMax = new Vector2(healthPercent, 1f);
                fillRect.offsetMax = new Vector2(-0.025f, -0.025f);
            }

            if (fillImage != null)
            {
                fillImage.color = healthPercent > 0.5f
                    ? Color.Lerp(Color.yellow, Color.green, (healthPercent - 0.5f) * 2f)
                    : Color.Lerp(Color.red, Color.yellow, healthPercent * 2f);
            }

            if (container != null && _enemy.IsDead)
                container.SetActive(false);
        }
    }
}
