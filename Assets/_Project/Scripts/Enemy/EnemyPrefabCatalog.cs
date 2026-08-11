using UnityEngine;

namespace DevouringBeast
{
    [CreateAssetMenu(menuName = "DevouringBeast/Enemy Prefab Catalog", fileName = "EnemyPrefabCatalog")]
    public sealed class EnemyPrefabCatalog : ScriptableObject
    {
        public GameObject[] normalPrefabs;
        public GameObject[] elitePrefabs;
        public GameObject[] bossPrefabs;

        public GameObject Find(EnemyArchetype archetype)
        {
            GameObject[] all = normalPrefabs ?? System.Array.Empty<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && System.Enum.TryParse(all[i].name, out EnemyArchetype parsed) && parsed == archetype) return all[i];
            }
            all = elitePrefabs ?? System.Array.Empty<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && System.Enum.TryParse(all[i].name, out EnemyArchetype parsed) && parsed == archetype) return all[i];
            }
            all = bossPrefabs ?? System.Array.Empty<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && System.Enum.TryParse(all[i].name, out EnemyArchetype parsed) && parsed == archetype) return all[i];
            }
            return null;
        }

        public GameObject[] GetTier(int tier)
        {
            return normalPrefabs;
        }
    }
}
