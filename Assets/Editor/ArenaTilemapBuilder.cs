using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class ArenaTilemapBuilder
{
    private const string SourcePath = "Assets/Art/Tilesets/bg.png";
    private const string TileFolder = "Assets/_Project/Tiles/Arena";
    private const string ScenePath = "Assets/Scenes/GameScene.unity";
    private const string GridName = "ArenaGrid";
    private const int Width = 56;
    private const int Height = 32;
    private static readonly Vector3 GridOrigin = new(12f, 24f, 0f);

    [MenuItem("Tools/DevouringBeast/Rebuild Arena Tilemap")]
    public static void Rebuild()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new InvalidOperationException($"Open {ScenePath} before rebuilding the arena tilemap.");

        ConfigureAtlasImporter();
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(SourcePath)
            .OfType<Sprite>()
            .OrderBy(sprite => ParseIndex(sprite.name))
            .ToArray();
        if (sprites.Length != 36)
            throw new InvalidOperationException($"Expected 36 sprites in {SourcePath}, found {sprites.Length}.");

        Tile[] tiles = CreateOrUpdateTiles(sprites);
        ReplaceSceneTilemap(scene, tiles);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[ArenaTilemapBuilder] Painted {Width}x{Height} arena using {sprites.Length} atlas sprites.");
    }

    private static void ConfigureAtlasImporter()
    {
        TextureImporter importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"TextureImporter not found for {SourcePath}.");

        bool changed = !Mathf.Approximately(importer.spritePixelsPerUnit, 128f) ||
            importer.filterMode != FilterMode.Bilinear;
        importer.spritePixelsPerUnit = 128f;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

#pragma warning disable CS0618
        SpriteMetaData[] metadata = importer.spritesheet;
        for (int i = 0; i < metadata.Length; i++)
        {
            if (metadata[i].alignment == (int)SpriteAlignment.Center &&
                metadata[i].pivot == new Vector2(0.5f, 0.5f))
                continue;

            metadata[i].alignment = (int)SpriteAlignment.Center;
            metadata[i].pivot = new Vector2(0.5f, 0.5f);
            changed = true;
        }
        importer.spritesheet = metadata;
#pragma warning restore CS0618

        if (changed)
            importer.SaveAndReimport();
    }

    private static Tile[] CreateOrUpdateTiles(Sprite[] sprites)
    {
        EnsureFolder("Assets/_Project/Tiles");
        EnsureFolder(TileFolder);
        Tile[] tiles = new Tile[sprites.Length];

        for (int i = 0; i < sprites.Length; i++)
        {
            string path = $"{TileFolder}/{sprites[i].name}.asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = sprites[i].name;
                AssetDatabase.CreateAsset(tile, path);
            }

            tile.sprite = sprites[i];
            tile.color = Color.white;
            tile.transform = Matrix4x4.identity;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            tiles[i] = tile;
        }

        return tiles;
    }

    private static void ReplaceSceneTilemap(Scene scene, Tile[] tiles)
    {
        GameObject oldGrid = scene.GetRootGameObjects().FirstOrDefault(root => root.name == GridName);
        if (oldGrid != null)
            UnityEngine.Object.DestroyImmediate(oldGrid);

        GameObject gridObject = new(GridName, typeof(Grid));
        gridObject.transform.position = GridOrigin;
        Grid grid = gridObject.GetComponent<Grid>();
        grid.cellSize = Vector3.one;
        grid.cellGap = Vector3.zero;
        grid.cellLayout = GridLayout.CellLayout.Rectangle;
        grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

        CreateBackdrop(gridObject.transform, tiles[18].sprite);

        GameObject terrainObject = new("Terrain", typeof(Tilemap), typeof(TilemapRenderer));
        terrainObject.transform.SetParent(gridObject.transform, false);
        Tilemap tilemap = terrainObject.GetComponent<Tilemap>();
        tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
        tilemap.orientation = Tilemap.Orientation.XY;
        tilemap.SetTilesBlock(new BoundsInt(0, 0, 0, Width, Height, 1), BuildLayout(tiles));
        tilemap.CompressBounds();

        TilemapRenderer renderer = terrainObject.GetComponent<TilemapRenderer>();
        renderer.mode = TilemapRenderer.Mode.Chunk;
        renderer.sortOrder = TilemapRenderer.SortOrder.BottomLeft;
        renderer.sortingOrder = -20;
    }

    private static void CreateBackdrop(Transform parent, Sprite sprite)
    {
        GameObject backdropObject = new("SeamBackdrop", typeof(SpriteRenderer));
        backdropObject.transform.SetParent(parent, false);
        backdropObject.transform.localPosition = new Vector3(Width * 0.5f, Height * 0.5f, 0f);
        backdropObject.transform.localScale = new Vector3(Width, Height, 1f);
        SpriteRenderer renderer = backdropObject.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = -21;
        renderer.color = new Color(0.82f, 0.88f, 0.56f, 1f);
    }

    private static TileBase[] BuildLayout(Tile[] tiles)
    {
        TileBase[] layout = new TileBase[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float center = Height * 0.5f + Mathf.Sin((x - 4f) * 0.18f) * 2.2f;
                float signedDistance = y + 0.5f - center;
                float distance = Mathf.Abs(signedDistance);
                int index;

                if (distance < 1.05f)
                {
                    // Pair the atlas edge tiles so the pale road remains continuous.
                    index = signedDistance >= 0f
                        ? (((x + y) & 1) == 0 ? 10 : 11)
                        : (((x + y) & 1) == 0 ? 4 : 5);
                }
                else
                {
                    // Two nearly identical grass tiles break repetition without creating a checkerboard.
                    bool upperMeadow = y > 23 && x < 38;
                    bool drySouthWest = y < 10 && x < 18;
                    bool goldenEast = x > 42 && y < 21;
                    if (upperMeadow)
                        index = ((x * 5 + y * 3) % 9 == 0) ? 27 : 18;
                    else if (drySouthWest)
                        index = ((x + y * 2) % 7 == 0) ? 20 : 19;
                    else if (goldenEast)
                        index = ((x * 3 + y) % 8 == 0) ? 30 : 24;
                    else
                        index = ((x * 13 + y * 7) % 11 == 0) ? 34 : 33;
                }

                layout[x + y * Width] = tiles[index];
            }
        }

        return layout;
    }

    private static int ParseIndex(string name)
    {
        int separator = name.LastIndexOf('_');
        return separator >= 0 && int.TryParse(name[(separator + 1)..], out int index)
            ? index
            : int.MaxValue;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int separator = path.LastIndexOf('/');
        string parent = path[..separator];
        string name = path[(separator + 1)..];
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
