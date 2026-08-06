using System.Collections;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>Reusable oval ground shadow for characters and landed objects.</summary>
    [DisallowMultipleComponent]
    public sealed class GroundShadow : MonoBehaviour
    {
        private const int ShadowSortingOrder = -5;
        private static Sprite _shadowSprite;

        [SerializeField] private Vector2 size = new(1.1f, 0.42f);
        [SerializeField] private Vector2 offset = new(0f, -0.35f);
        [SerializeField, Range(0f, 1f)] private float opacity = 0.38f;

        private Transform _visual;
        private Coroutine _landingRoutine;

        public static GroundShadow Ensure(GameObject owner)
        {
            GroundShadow shadow = owner.GetComponent<GroundShadow>();
            return shadow != null ? shadow : owner.AddComponent<GroundShadow>();
        }

        private void Awake()
        {
            GameObject shadowObject = new("GroundShadow", typeof(SpriteRenderer));
            shadowObject.transform.SetParent(transform, false);
            shadowObject.transform.localPosition = offset;
            int layer = LayerMask.NameToLayer("Shadows");
            if (layer >= 0) shadowObject.layer = layer;
            SpriteRenderer renderer = shadowObject.GetComponent<SpriteRenderer>();
            renderer.sprite = GetShadowSprite();
            renderer.color = new Color(0f, 0f, 0f, opacity);
            renderer.sortingOrder = ShadowSortingOrder;
            _visual = shadowObject.transform;
            _visual.localScale = new Vector3(size.x, size.y, 1f);
        }

        public void BeginLanding(float duration)
        {
            if (_visual == null) return;
            if (_landingRoutine != null) StopCoroutine(_landingRoutine);
            _landingRoutine = StartCoroutine(LandingRoutine(Mathf.Max(0.01f, duration)));
        }

        private IEnumerator LandingRoutine(float duration)
        {
            Vector3 target = new(size.x, size.y, 1f);
            _visual.localScale = new Vector3(0.08f, 0.03f, 1f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _visual.localScale = Vector3.LerpUnclamped(new Vector3(0.08f, 0.03f, 1f), target, t);
                yield return null;
            }
            _visual.localScale = target;
            _landingRoutine = null;
        }

        private static Sprite GetShadowSprite()
        {
            if (_shadowSprite != null) return _shadowSprite;
            const int width = 64;
            const int height = 32;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            {
                name = "RuntimeOvalShadow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = (y + 0.5f) / height * 2f - 1f;
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f) / width * 2f - 1f;
                    float edge = Mathf.Clamp01(1f - (nx * nx + ny * ny));
                    pixels[x + y * width] = new Color(1f, 1f, 1f, edge * edge);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _shadowSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f), width);
            _shadowSprite.name = "RuntimeOvalShadow";
            _shadowSprite.hideFlags = HideFlags.HideAndDontSave;
            return _shadowSprite;
        }
    }
}
