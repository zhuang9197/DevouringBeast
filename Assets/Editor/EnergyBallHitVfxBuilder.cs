using System;
using System.IO;
using System.Linq;
using DevouringBeast;
using UnityEditor;
using UnityEngine;

public static class EnergyBallHitVfxBuilder
{
    private const string BallEffectPath = "Assets/Art/VFX/ball_effect.png";
    private const string BombPath = "Assets/Art/VFX/bomb.png";
    private const string PoisonBombPath = "Assets/Art/VFX/poison_bomb.png";
    private const string PoisonCloudPath = "Assets/Art/VFX/poison_cloud.png";
    private const string CatalogPath = "Assets/Resources/System/EnergyBallHitVfxCatalog.asset";
    private const string ParticleMaterialPath = "Assets/_Project/Materials/EnergyBallHitParticles.mat";
    private const string PoisonCloudMaterialPath = "Assets/_Project/Materials/PoisonCloudDissolveDistortion.mat";

    [MenuItem("Tools/DevouringBeast/Rebuild Energy Ball Hit VFX")]
    public static void Rebuild()
    {
        Sprite[] ballSprites = LoadSprites(BallEffectPath);
        Sprite[] bombFrames = LoadFrames(BombPath, "bomb_");
        Sprite[] poisonBombFrames = LoadFrames(PoisonBombPath, "poison_bomb_");
        Sprite poisonCloud = AssetDatabase.LoadAssetAtPath<Sprite>(PoisonCloudPath);

        Sprite normal = FindSprite(ballSprites, "ball_effect_normal");
        Sprite fire = FindSprite(ballSprites, "ball_effect_fire");
        Sprite poison = FindSprite(ballSprites, "ball_effect_poison");
        if (normal == null || fire == null || poison == null || poisonCloud == null ||
            bombFrames.Length == 0 || poisonBombFrames.Length == 0)
        {
            throw new InvalidOperationException("Energy-ball VFX sprites are missing or incorrectly sliced.");
        }

        Material particleMaterial = GetOrCreateMaterial(
            ParticleMaterialPath, "Universal Render Pipeline/Particles/Unlit");
        particleMaterial.SetTexture("_BaseMap", normal.texture);
        if (particleMaterial.HasProperty("_BaseColor"))
            particleMaterial.SetColor("_BaseColor", Color.white);
        if (particleMaterial.HasProperty("_Surface"))
            particleMaterial.SetFloat("_Surface", 1f);
        if (particleMaterial.HasProperty("_Blend"))
            particleMaterial.SetFloat("_Blend", 0f);

        Material poisonCloudMaterial = GetOrCreateMaterial(
            PoisonCloudMaterialPath, "DevouringBeast/VFX/PoisonCloudDissolveDistortion");
        poisonCloudMaterial.SetTexture("_BaseMap", poisonCloud.texture);
        poisonCloudMaterial.SetColor("_BaseColor", new Color(0.12f, 0.42f, 0.1f, 0.88f));
        poisonCloudMaterial.SetFloat("_NoiseScale", 5.5f);
        poisonCloudMaterial.SetFloat("_NoiseSpeed", 0.28f);
        poisonCloudMaterial.SetFloat("_DissolveSoftness", 0.14f);
        poisonCloudMaterial.SetFloat("_DistortionStrength", 0.012f);

        EnergyBallHitVfxCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EnergyBallHitVfxCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<EnergyBallHitVfxCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.normalParticle = normal;
        catalog.fireParticle = fire;
        catalog.poisonParticle = poison;
        catalog.particleMaterial = particleMaterial;
        catalog.fireExplosionFrames = bombFrames;
        catalog.poisonExplosionFrames = poisonBombFrames;
        catalog.fireExplosionScale = 1.25f;
        catalog.poisonExplosionScale = 1.35f;
        catalog.poisonCloud = poisonCloud;
        catalog.poisonCloudMaterial = poisonCloudMaterial;

        EditorUtility.SetDirty(particleMaterial);
        EditorUtility.SetDirty(poisonCloudMaterial);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EnergyBallHitVfxBuilder] Built catalog with {bombFrames.Length} fire and " +
            $"{poisonBombFrames.Length} poison explosion frames.");
    }

    private static Material GetOrCreateMaterial(string path, string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
            throw new InvalidOperationException($"Shader not found: {shaderName}");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            material.shader = shader;
            return material;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
        material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Sprite[] LoadSprites(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();

    private static Sprite FindSprite(Sprite[] sprites, string name) =>
        sprites.FirstOrDefault(sprite => sprite.name == name);

    private static Sprite[] LoadFrames(string path, string prefix)
    {
        return LoadSprites(path)
            .Where(sprite => sprite.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                (path == BombPath && sprite.name == "poision_bomb_0"))
            .OrderBy(sprite => ParseFrameIndex(sprite.name))
            .ToArray();
    }

    private static int ParseFrameIndex(string name)
    {
        int separator = name.LastIndexOf('_');
        return separator >= 0 && int.TryParse(name[(separator + 1)..], out int index)
            ? index
            : int.MaxValue;
    }
}
