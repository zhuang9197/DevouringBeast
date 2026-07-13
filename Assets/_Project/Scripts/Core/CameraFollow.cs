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
            if (target == null) return;

            Vector3 desiredPos = target.position + offset;

            // 如果有 MapBounds，将相机限制在边界内
            if (MapBounds.Instance != null)
            {
                Vector2 min = MapBounds.Instance.Min;
                Vector2 max = MapBounds.Instance.Max;

                float halfH = _cam.orthographicSize;
                float halfW = halfH * _cam.aspect;

                desiredPos.x = Mathf.Clamp(desiredPos.x, min.x + halfW, max.x - halfW);
                desiredPos.y = Mathf.Clamp(desiredPos.y, min.y + halfH, max.y - halfH);
            }

            transform.position = desiredPos;
        }

        public void SetTarget(Transform t)
        {
            target = t;
        }
    }
}
