using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DevouringBeast.EditorTools
{
    /// <summary>
    /// 一次性工具：创建 PlayerAnimData 资产并自动填充所有精灵
    /// 执行后可删除此脚本
    /// </summary>
    public static class PlayerAnimDataBuilder
    {
        private const string ASSET_PATH = "Assets/_Project/Settings/PlayerAnimData.asset";

        [MenuItem("DevouringBeast/Build Player Anim Data")]
        public static void Build()
        {
            var data = ScriptableObject.CreateInstance<PlayerAnimData>();
            bool ok = true;

            // 静态精灵
            data.idleFront = LoadSprite("Assets/Art/Sprites/Player/front.png");
            data.idleBack = LoadSprite("Assets/Art/Sprites/Player/back.png");
            data.idleSide = LoadSprite("Assets/Art/Sprites/Player/side.png");
            data.fullFront = LoadSprite("Assets/Art/Sprites/Player/full_front.png");
            data.fullBack = LoadSprite("Assets/Art/Sprites/Player/full_back.png");
            data.fullSide = LoadSprite("Assets/Art/Sprites/Player/full_side.png");

            // 动画帧
            data.frontRun = LoadSortedSprites("Assets/Art/Sprites/Player/Textures/front_run");
            data.frontFullWalk = LoadSortedSprites("Assets/Art/Sprites/Player/Textures/front_full_walk");
            data.frontSuck = LoadSortedSprites("Assets/Art/Sprites/Player/Textures/front_suck");
            data.backRun = LoadSortedSprites("Assets/Art/Sprites/Player/Textures/back_run");
            data.backFullWalk = LoadSortedSprites("Assets/Art/Sprites/Player/Textures/back_full_walk");
            data.backSuck = LoadSortedSprites("Assets/Art/Sprites/Player/Textures/back_suck");
            data.sideRun = LoadSortedSprites("Assets/Art/Sprites/Player/Textures/side_run");
            data.sideFullWalk = LoadSortedSprites("Assets/Art/Sprites/Player/Textures/side_full_walk");
            data.sideSuck = LoadSortedSprites("Assets/Art/Sprites/Player/Textures/side_suck");

            // Suck 分帧点
            data.frontSuckWindupEnd = 14;
            data.backSuckWindupEnd = 10;
            data.sideSuckWindupEnd = 12;

            // 删除旧资产
            if (File.Exists(ASSET_PATH))
            {
                AssetDatabase.DeleteAsset(ASSET_PATH);
            }

            AssetDatabase.CreateAsset(data, ASSET_PATH);

            // 保存所有子精灵引用到 SO
            MarkSprites(data);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PlayerAnimDataBuilder] 创建完成！\n" +
                $"frontRun={data.frontRun.Length} frontFullWalk={data.frontFullWalk.Length} frontSuck={data.frontSuck.Length}\n" +
                $"backRun={data.backRun.Length} backFullWalk={data.backFullWalk.Length} backSuck={data.backSuck.Length}\n" +
                $"sideRun={data.sideRun.Length} sideFullWalk={data.sideFullWalk.Length} sideSuck={data.sideSuck.Length}");
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite[] LoadSortedSprites(string path)
        {
            var allObjs = AssetDatabase.FindAssets("t:Sprite", new[] { path })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(assetPath => AssetDatabase.LoadAssetAtPath<Sprite>(assetPath))
                .Where(sprite => sprite != null).Cast<Object>();
            var list = new List<Sprite>();
            foreach (var o in allObjs)
            {
                if (o is Sprite s) list.Add(s);
            }

            // 冒泡排序：按名称末尾数字
            int n = list.Count;
            for (int pass = 0; pass < n - 1; pass++)
            {
                for (int i = 0; i < n - 1 - pass; i++)
                {
                    if (ExtractNumber(list[i].name) > ExtractNumber(list[i + 1].name))
                    {
                        (list[i], list[i + 1]) = (list[i + 1], list[i]);
                    }
                }
            }
            return list.ToArray();
        }

        private static int ExtractNumber(string name)
        {
            var parts = name.Split('_');
            if (int.TryParse(parts[parts.Length - 1], out int val))
                return val;
            return 0;
        }

        private static void MarkSprites(ScriptableObject so)
        {
            EditorUtility.SetDirty(so);
        }
    }
}
