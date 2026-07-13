using UnityEngine;

namespace DevouringBeast
{
    /// <summary>玩家朝向</summary>
    public enum Facing
    {
        Front,    // S — 正面朝向镜头
        Back,     // W — 背面
        SideLeft, // A — 侧面（图集默认方向）
        SideRight // D — 侧面镜像
    }

    /// <summary>
    /// 玩家动画数据 — 存储所有精灵数组、静态精灵、suck 分帧点
    /// </summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Player Anim Data", fileName = "PlayerAnimData")]
    public class PlayerAnimData : ScriptableObject
    {
        [Header("静态精灵（嘴里无东西）")]
        public Sprite idleFront;
        public Sprite idleBack;
        public Sprite idleSide; // 默认朝左(A)

        [Header("静态精灵（嘴里有东西 — full）")]
        public Sprite fullFront;
        public Sprite fullBack;
        public Sprite fullSide;

        [Header("Front (S) 动画帧")]
        public Sprite[] frontRun;
        public Sprite[] frontFullWalk;
        public Sprite[] frontSuck;
        [Tooltip("front_suck 第一阶段结束帧索引（0-based），之后为第二阶段循环")]
        public int frontSuckWindupEnd = 14; // 0~14

        [Header("Back (W) 动画帧")]
        public Sprite[] backRun;
        public Sprite[] backFullWalk;
        public Sprite[] backSuck;
        [Tooltip("back_suck 第一阶段结束帧索引（0-based）")]
        public int backSuckWindupEnd = 10; // 0~10

        [Header("Side (A/D) 动画帧")]
        public Sprite[] sideRun;
        public Sprite[] sideFullWalk;
        public Sprite[] sideSuck;
        [Tooltip("side_suck 第一阶段结束帧索引（0-based）")]
        public int sideSuckWindupEnd = 12; // 0~12

        /// <summary>
        /// 获取指定朝向的 run 动画
        /// </summary>
        public Sprite[] GetRun(Facing facing)
        {
            return facing switch
            {
                Facing.Front => frontRun,
                Facing.Back => backRun,
                _ => sideRun
            };
        }

        /// <summary>
        /// 获取指定朝向的 fullWalk 动画
        /// </summary>
        public Sprite[] GetFullWalk(Facing facing)
        {
            return facing switch
            {
                Facing.Front => frontFullWalk,
                Facing.Back => backFullWalk,
                _ => sideFullWalk
            };
        }

        /// <summary>
        /// 获取指定朝向的 suck 动画
        /// </summary>
        public Sprite[] GetSuck(Facing facing)
        {
            return facing switch
            {
                Facing.Front => frontSuck,
                Facing.Back => backSuck,
                _ => sideSuck
            };
        }

        /// <summary>
        /// 获取指定朝向 suck 第一阶段结束帧索引
        /// </summary>
        public int GetSuckWindupEnd(Facing facing)
        {
            return facing switch
            {
                Facing.Front => frontSuckWindupEnd,
                Facing.Back => backSuckWindupEnd,
                _ => sideSuckWindupEnd
            };
        }

        /// <summary>
        /// 获取指定朝向的空闲静态精灵
        /// </summary>
        public Sprite GetIdleSprite(Facing facing, bool hasItems)
        {
            if (hasItems)
            {
                return facing switch
                {
                    Facing.Front => fullFront,
                    Facing.Back => fullBack,
                    _ => fullSide
                };
            }
            return facing switch
            {
                Facing.Front => idleFront,
                Facing.Back => idleBack,
                _ => idleSide
            };
        }
    }
}
