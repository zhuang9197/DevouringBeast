using UnityEngine;

namespace DevouringBeast
{
    [CreateAssetMenu(menuName = "DevouringBeast/Enemy Prefab Catalog", fileName = "EnemyPrefabCatalog")]
    public sealed class EnemyPrefabCatalog : ScriptableObject
    {
        public GameObject[] normalPrefabs;
        public GameObject[] elitePrefabs;
        public GameObject[] bossPrefabs;

        public GameObject[] GetTier(int tier)
        {
            if (normalPrefabs == null || normalPrefabs.Length == 0) return null;
            int start = Mathf.Clamp((tier - 1) * 10, 0, normalPrefabs.Length);
            int count = Mathf.Min(10, normalPrefabs.Length - start);
            if (count <= 0) return null;
            GameObject[] result = new GameObject[count];
            System.Array.Copy(normalPrefabs, start, result, 0, count);
            return result;
        }
    }
}
