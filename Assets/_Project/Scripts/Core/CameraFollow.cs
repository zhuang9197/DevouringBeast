using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 相机跟随 — 让 Main Camera 始终跟随玩家
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("目标")]
        [SerializeField] private Transform target;

        [Header("跟随参数")]
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector3 offset = new(0f, 0f, -10f);

        private Camera _cam;
        private bool _hasInitializedPosition;
        private bool _fixedToRoom;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (_fixedToRoom) return;
            if (target == null) return;

            Vector3 desiredPos = target.position + offset;

            // 如果有 MapBounds，将相机限制在边界内
            if (MapBounds.Instance != null)
            {
                Vector2 min = MapBounds.Instance.Min;
                Vector2 max = MapBounds.Instance.Max;

                float halfH = _cam.orthographicSize;
                float halfW = halfH * _cam.aspect;

                desiredPos.x = ClampAxis(desiredPos.x, min.x, max.x, halfW);
                desiredPos.y = ClampAxis(desiredPos.y, min.y, max.y, halfH);
            }

            if (!_hasInitializedPosition || smoothSpeed <= 0f)
            {
                transform.position = desiredPos;
                _hasInitializedPosition = true;
                return;
            }

            float blend = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPos, blend);
        }

        private static float ClampAxis(float targetPosition, float min, float max, float halfViewportSize)
        {
            // 视口覆盖整个地图时不存在合法的跟随区间，固定在地图中心可避免 Clamp 上下限反转抖动。
            if (max - min <= halfViewportSize * 2f)
                return (min + max) * 0.5f;

            return Mathf.Clamp(targetPosition, min + halfViewportSize, max - halfViewportSize);
        }

        public void SetTarget(Transform t)
        {
            target = t;
            _fixedToRoom = false;
        }

        public void SetRoom(Vector2 center, Vector2 roomSize)
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            _fixedToRoom = true;
            _hasInitializedPosition = true;
            transform.position = new Vector3(center.x, center.y, offset.z);
            _cam.orthographic = true;
            _cam.orthographicSize = roomSize.y * 0.5f;
            ApplyFixedAspect(roomSize.x / Mathf.Max(0.01f, roomSize.y));
        }

        private void ApplyFixedAspect(float targetAspect)
        {
            float screenAspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            if (screenAspect > targetAspect)
            {
                float width = targetAspect / screenAspect;
                _cam.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }
            else
            {
                float height = screenAspect / targetAspect;
                _cam.rect = new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }
        }
    }
}
