using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// PlayerVisual — 玩家视觉表现
    /// 处理口中物品造成的膨胀效果
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        [Header("渲染")]
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer earsRenderer;

        [Header("膨胀效果")]
        [SerializeField] private float bulgeScale = 1.3f;
        [SerializeField] private float bulgeSpeed = 5f;

        private SwallowContainer _container;
        private Vector3 _baseScale;

        private void Awake()
        {
            _container = GetComponent<SwallowContainer>();
            if (bodyRenderer == null)
                bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            _baseScale = transform.localScale;
            if (bodyRenderer != null) bodyRenderer.color = Color.white;
            if (earsRenderer != null) earsRenderer.color = Color.white;
        }

        private void Update()
        {
            UpdateBulge();
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

        /// <summary>兼容旧事件绑定，玩家颜色始终保持原色。</summary>
        public void ResetToWhite()
        {
            if (bodyRenderer != null) bodyRenderer.color = Color.white;
            if (earsRenderer != null) earsRenderer.color = Color.white;
        }
    }
}
