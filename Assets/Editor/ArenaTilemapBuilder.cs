#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class ArenaTilemapBuilder
{
    private const string SourcePath = "Assets/Art/Tilesets/new_map.png";
    private const string TileFolder = "Assets/Resources/Map/Tiles";
    private const string LegacyTileFolder = "Assets/_Project/Tiles/Arena";
    private const string LegacySingleTile = "Assets/Resources/Map/RoomMapTile.asset";
    private const string ScenePath = "Assets/Scenes/GameScene.unity";
    private const string TriggerPath = "Assets/Editor/RebuildRoomMap.trigger";
    private static readonly string[] RequiredSpriteNames =
    {
        "new_map1", // corner, default top-left
        "new_map2", // open door, default bottom
        "new_map3", // wall, default top
        "new_map4", // first floor segment
        "new_map4_1",
        "new_map4_2",
        "new_map4_3",
        "new_map4_4"
    };

    static ArenaTilemapBuilder()
    {
        EditorApplication.delayCall += RunPendingRebuild;
    }

    [MenuItem("Tools/DevouringBeast/Rebuild Room Tilemap and Clean Scene")]
    public static void Rebuild()
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(SourcePath)
            .OfType<Sprite>()
            .ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
        string[] missing = RequiredSpriteNames.Where(name => !sprites.ContainsKey(name)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Missing required sprites in {SourcePath}: {string.Join(", ", missing)}.");

        EnsureFolder(TileFolder);
        foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { TileFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!RequiredSpriteNames.Contains(Path.GetFileNameWithoutExtension(path), StringComparer.Ordinal))
                AssetDatabase.DeleteAsset(path);
        }

        foreach (string spriteName in RequiredSpriteNames)
        {
            string path = $"{TileFolder}/{spriteName}.asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = spriteName;
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprites[spriteName];
            tile.color = Color.white;
            tile.transform = Matrix4x4.identity;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(LegacySingleTile) != null)
            AssetDatabase.DeleteAsset(LegacySingleTile);
        if (AssetDatabase.IsValidFolder(LegacyTileFolder))
            AssetDatabase.DeleteAsset(LegacyTileFolder);

        CleanGameScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ArenaTilemapBuilder] Built corner/open-door/wall and continuous floor-strip tiles from new_map.png.");
    }

    private static void CleanGameScene()
    {
        Scene scene = SceneManager.GetActiveScene().path == ScenePath
            ? SceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        string[] obsoleteNames = { "ArenaGrid", "--- SPAWN POINTS ---" };
        foreach (string obsoleteName in obsoleteNames)
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == obsoleteName);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/');
        string parent = path.Substring(0, split);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
    }

    private static void RunPendingRebuild()
    {
        string absoluteTrigger = Path.GetFullPath(TriggerPath);
        if (!File.Exists(absoluteTrigger)) return;
        try
        {
            Rebuild();
            File.Delete(absoluteTrigger);
            string triggerMeta = absoluteTrigger + ".meta";
            if (File.Exists(triggerMeta)) File.Delete(triggerMeta);
            AssetDatabase.Refresh();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
#endif
