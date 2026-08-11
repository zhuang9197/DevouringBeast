using UnityEngine;
using UnityEngine.U2D;

namespace DevouringBeast
{
    public enum EnemyContentCategory
    {
        Minion,
        Elite,
        Boss
    }

    /// <summary>
    /// The single Addressables entry for one enemy. Its prefab, data, atlas and animation
    /// dependencies are loaded and released together through the definition handle.
    /// </summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Enemy Content Definition", fileName = "EnemyContent")]
    public sealed class EnemyContentDefinition : ScriptableObject
    {
        [SerializeField] private EnemyArchetype archetype;
        [SerializeField] private EnemyContentCategory category;
        [SerializeField] private GameObject prefab;
        [SerializeField] private EnemyData data;
        [SerializeField] private SpriteAtlas atlas;

        public EnemyArchetype Archetype => archetype;
        public EnemyContentCategory Category => category;
        public GameObject Prefab => prefab;
        public EnemyData Data => data;
        public SpriteAtlas Atlas => atlas;
        public bool IsValid => prefab != null && data != null && atlas != null;
    }
}
