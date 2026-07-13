using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DevouringBeast.EditorTools
{
    /// <summary>
    /// 批量创建/修复敌人 AnimatorController
    /// 为 Anims1~Anims10 创建带 IsMoving/IsAttacking 参数的状态机
    /// </summary>
    public static class EnemyAnimControllerBuilder
    {
        private const string ANIM_ROOT = "Assets/Art/Enemy/Animations";

        [MenuItem("DevouringBeast/Build Enemy Anim Controllers")]
        public static void Build()
        {
            for (int i = 1; i <= 10; i++)
            {
                string folder = ANIM_ROOT + "/Anims" + i;

                // 加载3个动画剪辑
                var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/idle.anim");
                var move = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/move.anim");
                var attack = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/attack.anim");

                if (idle == null || move == null || attack == null)
                {
                    Debug.LogWarning($"Anims{i}: missing clips, skip");
                    continue;
                }

                // 确定 controller 名称（与现有命名一致）
                string ctrlName;
                if (i == 1) ctrlName = "Character (1)";
                else if (i == 2) ctrlName = "Character (11)";
                else if (i == 7) ctrlName = "Character (70)";
                else if (i == 8) ctrlName = "Character (80)";
                else if (i == 9) ctrlName = "Character (81)";
                else if (i == 10) ctrlName = "Character (100)";
                else ctrlName = "Character (" + ((i - 1) * 10 + 1) + ")";

                string ctrlPath = folder + "/" + ctrlName + ".controller";

                // 删除旧的
                if (System.IO.File.Exists(ctrlPath))
                    AssetDatabase.DeleteAsset(ctrlPath);

                // 创建新的
                var controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);

                // 添加参数
                controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
                controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);

                var sm = controller.layers[0].stateMachine;

                // 创建3个状态
                var idleState = sm.AddState("idle");
                idleState.motion = idle;

                var moveState = sm.AddState("move");
                moveState.motion = move;

                var attackState = sm.AddState("attack");
                attackState.motion = attack;

                sm.defaultState = idleState;

                // 转换：idle <-> move (IsMoving)
                var t1 = idleState.AddTransition(moveState);
                t1.hasExitTime = false; t1.duration = 0.1f;
                t1.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");

                var t2 = moveState.AddTransition(idleState);
                t2.hasExitTime = false; t2.duration = 0.1f;
                t2.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

                // 转换：任意 -> attack (IsAttacking)
                var t3 = idleState.AddTransition(attackState);
                t3.hasExitTime = false; t3.duration = 0.05f;
                t3.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");

                var t4 = moveState.AddTransition(attackState);
                t4.hasExitTime = false; t4.duration = 0.05f;
                t4.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");

                // 转换：attack -> idle (!IsAttacking)
                var t5 = attackState.AddTransition(idleState);
                t5.hasExitTime = true; t5.exitTime = 0.8f; t5.duration = 0.1f;
                t5.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");

                // 设置动画循环
                SetLoop(idle, true);
                SetLoop(move, true);
                SetLoop(attack, false);

                EditorUtility.SetDirty(controller);
                Debug.Log($"Created controller: {ctrlPath}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyAnimControllerBuilder] All controllers created!");
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }
    }
}
