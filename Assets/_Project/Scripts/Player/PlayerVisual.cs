using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// PlayerVisual — 玩家视觉表现
    /// 处理颜色变化（标签占比）、膨胀效果、升级动画
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        [Header("渲染")]
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer earsRenderer;

        [Header("膨胀效果")]
        [SerializeField] private float bulgeScale = 1.3f;
        [SerializeField] private float bulgeSpeed = 5f;

        [Header("标签颜色")]
        [SerializeField] private Color poisonColor = Color.green;
        [SerializeField] private Color fireColor = Color.red;
        [SerializeField] private Color evolutionColor = new Color(0.501961f, 0f, 1f, 1f);
        [SerializeField] private Color superpowerColor = Color.yellow;
        [SerializeField] private Color faithColor = Color.blue;
        private static readonly Color DefaultColor = Color.white;
        private static readonly ItemTag[] ItemTags = (ItemTag[])System.Enum.GetValues(typeof(ItemTag));

        private SwallowContainer _container;
        private Vector3 _baseScale;
        private Color _targetColor = DefaultColor;

        private void Awake()
        {
            _container = GetComponent<SwallowContainer>();
            if (bodyRenderer == null)
                bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            UpdateBulge();
            UpdateColor();
        }

        /// <summary>
        /// 口中物品存在时膨胀
        /// </summary>
        private void UpdateBulge()
        {
            float targetScale = _container.HasItems ? bulgeScale : 1f;
            float current = Mathf.Lerp(transform.localScale.x, targetScale * _baseScale.x, bulgeSpeed * Time.deltaTime);
            float yScale = Mathf.Lerp(transform.localScale.y, targetScale * _baseScale.y, bulgeSpeed * Time.deltaTime);
            transform.localScale = new Vector3(current, yScale, _baseScale.z);
        }

        /// <summary>
        /// 按标签占比混合颜色
        /// </summary>
        private void UpdateColor()
        {
            if (_container.TagMasses == null || bodyRenderer == null) return;

            Color blended = DefaultColor;

            foreach (ItemTag tag in ItemTags)
            {
                float ratio = _container.TagMasses.GetRatio(tag);
                if (ratio > 0f)
                {
                    Color tagColor = GetTagColor(tag);
                    blended = Color.Lerp(blended, tagColor, ratio * 0.9f); // 最多 80% 着色
                }
            }

            bodyRenderer.color = Color.Lerp(bodyRenderer.color, blended, 5f * Time.deltaTime);
        }

        private Color GetTagColor(ItemTag tag)
        {
            return tag switch
            {
                ItemTag.Poison => poisonColor,
                ItemTag.Fire => fireColor,
                ItemTag.Evolution => evolutionColor,
                ItemTag.Superpower => superpowerColor,
                ItemTag.Faith => faithColor,
                _ => DefaultColor
            };
        }

        /// <summary>
        /// 重置为白色（升级后调用）
        /// </summary>
        public void ResetToWhite()
        {
            _targetColor = DefaultColor;
            bodyRenderer.color = DefaultColor;
        }
    }
}
