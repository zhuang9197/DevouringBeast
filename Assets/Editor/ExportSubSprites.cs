using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class SpriteExporter : EditorWindow
{
    [MenuItem("Assets/Sprite Exporter/Export All Slices")]
    static void ExportAllSlices()
    {
        // 1. 获取当前选中的纹理
        Texture2D selectedTexture = Selection.activeObject as Texture2D;
        if (selectedTexture == null)
        {
            Debug.LogError("请选择一张图片!");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(selectedTexture);
        TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        // 2. 确保纹理可读（以防万一）
        if (!textureImporter.isReadable)
        {
            textureImporter.isReadable = true;
            AssetDatabase.ImportAsset(assetPath);
        }

        // 3. 创建输出目录
        string outputDir = Path.Combine(Path.GetDirectoryName(assetPath), selectedTexture.name + "_Slices");
        Directory.CreateDirectory(outputDir);

        // 4. 遍历并导出每个子精灵
        foreach (SpriteMetaData spriteMeta in textureImporter.spritesheet)
        {
            ExportSingleSprite(selectedTexture, spriteMeta, outputDir);
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("导出完成", $"所有子图片已导出到: {outputDir}", "确定");
    }

    static void ExportSingleSprite(Texture2D sourceTexture, SpriteMetaData spriteMeta, string outputDir)
    {
        // 根据元数据创建纹理
        Texture2D spriteTexture = new Texture2D((int)spriteMeta.rect.width, (int)spriteMeta.rect.height);
        Color[] pixels = sourceTexture.GetPixels((int)spriteMeta.rect.x, (int)spriteMeta.rect.y,
                                                 (int)spriteMeta.rect.width, (int)spriteMeta.rect.height);
        spriteTexture.SetPixels(pixels);
        spriteTexture.Apply();

        // 转换为PNG并保存
        byte[] bytes = spriteTexture.EncodeToPNG();
        string fileName = Path.Combine(outputDir, spriteMeta.name + ".png");
        File.WriteAllBytes(fileName, bytes);
    }
}