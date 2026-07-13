using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 地图边界 — 限制玩家、敌人、物品的活动范围
    /// 单例模式，供其他脚本查询
    /// </summary>
    public class MapBounds : MonoBehaviour
    {
        public static MapBounds Instance { get; private set; }

        [Header("边界范围")]
        [SerializeField] private Vector2 center = Vector2.zero;
        [SerializeField] private Vector2 size = new(40f, 40f);

        [Header("可视化")]
        [SerializeField] private bool createWallColliders = true;

        public Vector2 Min => center - size * 0.5f;
        public Vector2 Max => center + size * 0.5f;

        private void Awake()
        {
            Instance = this;

            if (createWallColliders)
                CreateWalls();
        }

        /// <summary>
        /// 将位置限制在边界内
        /// </summary>
        public Vector2 ClampPosition(Vector2 pos)
        {
            Vector2 min = Min;
            Vector2 max = Max;
            pos.x = Mathf.Clamp(pos.x, min.x, max.x);
            pos.y = Mathf.Clamp(pos.y, min.y, max.y);
            return pos;
        }

        /// <summary>
        /// 在边界四周创建不可见的碰撞墙
        /// </summary>
        private void CreateWalls()
        {
            float wallThickness = 2f;
            Vector2 min = Min;
            Vector2 max = Max;

            // 上墙
            CreateWall("Wall_Top",
                new Vector2(center.x, max.y + wallThickness * 0.5f),
                new Vector2(size.x + wallThickness * 2f, wallThickness));

            // 下墙
            CreateWall("Wall_Bottom",
                new Vector2(center.x, min.y - wallThickness * 0.5f),
                new Vector2(size.x + wallThickness * 2f, wallThickness));

            // 左墙
            CreateWall("Wall_Left",
                new Vector2(min.x - wallThickness * 0.5f, center.y),
                new Vector2(wallThickness, size.y));

            // 右墙
            CreateWall("Wall_Right",
                new Vector2(max.x + wallThickness * 0.5f, center.y),
                new Vector2(wallThickness, size.y));
        }

        private void CreateWall(string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = pos;
            go.layer = LayerMask.NameToLayer("Default");

            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            go.tag = "Wall";
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(center, size);
        }
#endif
    }
}
