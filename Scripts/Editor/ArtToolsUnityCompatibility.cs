using UnityEditor;
using UnityEngine;

namespace ArtTools.EditorTools
{
    internal static class ArtToolsUnityCompatibility
    {
        public static bool IsPrefabAsset(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

#if UNITY_2018_3_OR_NEWER
            return PrefabUtility.IsPartOfPrefabAsset(gameObject);
#else
            return PrefabUtility.GetPrefabType(gameObject) == PrefabType.Prefab;
#endif
        }

        public static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null && parent != null)
                instance.transform.SetParent(parent, true);
            return instance;
        }

        public static string GetNearestPrefabAssetPath(GameObject gameObject)
        {
            if (gameObject == null)
                return string.Empty;

#if UNITY_2018_3_OR_NEWER
            return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
#else
            Object source = PrefabUtility.GetPrefabParent(gameObject);
            return source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
#endif
        }
    }
}
