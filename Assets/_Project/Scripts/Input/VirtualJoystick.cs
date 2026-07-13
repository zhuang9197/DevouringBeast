using UnityEngine;
using UnityEngine.EventSystems;

namespace DevouringBeast
{
    /// <summary>
    /// 虚拟摇杆 — 处理触屏移动输入
    /// 挂载到屏幕左下区域的 UI Image 上，通过 EventSystem 检测拖拽
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("摇杆参数")]
        [Tooltip("摇杆最大活动半径（像素）")]
        [SerializeField] private float maxRadius = 120f;
        [Tooltip("死区半径（像素），小于此距离输出零")]
        [SerializeField] private float deadZone = 20f;

        /// <summary>当前归一化输出方向 (-1~1)</summary>
        public Vector2 Input { get; private set; }

        private Vector2 _originPos;
        private RectTransform _rectTransform;
        private bool _isDragging;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _originPos = _rectTransform.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isDragging = true;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            // 获取屏幕坐标差值
            Vector2 touchPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out touchPos);

            Vector2 delta = touchPos - _originPos;

            // 限制最大半径
            if (delta.magnitude > maxRadius)
            {
                delta = delta.normalized * maxRadius;
            }

            // 移动摇杆视觉
            _rectTransform.anchoredPosition = _originPos + delta;

            // 计算归一化输入（含死区）
            if (delta.magnitude < deadZone)
            {
                Input = Vector2.zero;
            }
            else
            {
                // 超过死区部分重新映射到 0~1
                float adjustedMag = (delta.magnitude - deadZone) / (maxRadius - deadZone);
                adjustedMag = Mathf.Clamp01(adjustedMag);
                Input = delta.normalized * adjustedMag;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isDragging = false;
            Input = Vector2.zero;
            // 摇杆回中
            _rectTransform.anchoredPosition = _originPos;
        }
    }
}
