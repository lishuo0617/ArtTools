using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ArtTools.EditorTools
{

public class MissingMaterialChecker : EditorWindow
{
    private class MissingMaterialIssue
    {
        public GameObject sceneObject;
        public string assetPath;
        public string hierarchyPath;
        public string rendererType;
        public int materialSlot;
        public bool isPrefabAsset;
    }

    private Vector2 resultScroll;
    private List<MissingMaterialIssue> results = new List<MissingMaterialIssue>();
    private DefaultAsset targetFolderAsset;
    private string targetFolderPath = "Assets";

    public static void Open()
    {
        var window = GetWindow<MissingMaterialChecker>();
        window.titleContent = new GUIContent("缺失材质检测");
        window.ConfigureWindowSize();
        window.Show();
    }

    private void OnEnable()
    {
        ConfigureWindowSize();
        EnsureTargetFolderAsset();
    }

    private void ConfigureWindowSize()
    {
        minSize = new Vector2(360, 440);
        maxSize = new Vector2(640, 2000);
    }

    private void OnGUI()
    {
        ArtToolsEditorUI.DrawHeader("缺失材质检测", "扫描目标路径下 Prefab 的 None / Missing 材质槽位", "d_Material Icon");
        DrawToolbar();
        DrawResultList();
    }

    private void DrawToolbar()
    {
        ArtToolsEditorUI.BeginPanel("检测设置");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("目标路径");
        EditorGUI.BeginChangeCheck();
        targetFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField(targetFolderAsset, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
            ApplyFolderAsset();

        if (GUILayout.Button("选择路径", GUILayout.Width(88)))
            PickFolder();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        if (ArtToolsEditorUI.PrimaryButton("开始扫描"))
            Scan();

        ArtToolsEditorUI.EndPanel();
    }

    private void DrawResultList()
    {
        ArtToolsEditorUI.BeginPanel("检测结果");
        ArtToolsEditorUI.Summary($"检测到 {results.Count} 个 None / Missing 材质槽位");
        // Only the result cards scroll horizontally. The setting panel stays responsive
        // and no artificial blank area is introduced for the whole window.
        resultScroll = EditorGUILayout.BeginScrollView(resultScroll, true, true);
        EditorGUILayout.BeginVertical(GUILayout.MinWidth(560));

        if (results.Count == 0)
            ArtToolsEditorUI.EmptyState("暂无结果。选择目标路径后开始扫描。");

        for (int i = 0; i < results.Count; i++)
            DrawItem(results[i], i);

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
        ArtToolsEditorUI.EndPanel();
    }

    private void DrawItem(MissingMaterialIssue issue, int index)
    {
        Rect rect = EditorGUILayout.BeginHorizontal(ArtToolsEditorUI.RowStyle, GUILayout.MinHeight(48), GUILayout.ExpandWidth(true));

        if (Event.current.type == EventType.Repaint && index % 2 == 0)
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.08f));

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            LocateIssue(issue);

        EditorGUILayout.LabelField($"{index + 1}.", GUILayout.Width(32));

        EditorGUILayout.BeginVertical();
        string source = issue.isPrefabAsset ? issue.assetPath : "场景对象";
        EditorGUILayout.LabelField($"{issue.hierarchyPath}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{source}    {issue.rendererType} / Materials[{issue.materialSlot}]", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUILayout.Width(58), GUILayout.ExpandHeight(true));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(58), GUILayout.Height(20)))
            LocateIssue(issue);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void Scan()
    {
        results.Clear();

        bool scannedAny = false;

        if (!string.IsNullOrEmpty(targetFolderPath) && AssetDatabase.IsValidFolder(targetFolderPath))
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { targetFolderPath });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabRoot == null)
                    continue;

                ScanRoot(prefabRoot, prefabPath, true);
            }

            scannedAny = true;
        }

        if (!scannedAny)
        {
            Debug.LogWarning("请选择 Assets 下的目标路径。");
            return;
        }

        Debug.Log($"扫描完成，发现 {results.Count} 个 None / Missing 材质槽位。");
    }

    private void ScanRoot(GameObject root, string assetPath, bool isPrefabAsset)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null)
                continue;

            for (int i = 0; i < materials.Length; i++)
            {
                if (!IsMissingMaterial(materials[i]))
                    continue;

                results.Add(new MissingMaterialIssue
                {
                    sceneObject = isPrefabAsset ? null : renderer.gameObject,
                    assetPath = assetPath,
                    hierarchyPath = GetHierarchyPath(root.transform, renderer.transform),
                    rendererType = renderer.GetType().Name,
                    materialSlot = i,
                    isPrefabAsset = isPrefabAsset
                });
            }
        }
    }

    private static bool IsMissingMaterial(Material material)
    {
        if (material == null)
            return true;

        return material.name.IndexOf("missing", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void LocateIssue(MissingMaterialIssue issue)
    {
        if (!issue.isPrefabAsset)
        {
            if (issue.sceneObject == null)
                return;

            Selection.activeObject = issue.sceneObject;
            EditorGUIUtility.PingObject(issue.sceneObject);

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            return;
        }

        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(issue.assetPath);
        if (prefabRoot == null)
            return;

        AssetDatabase.OpenAsset(prefabRoot);

        EditorApplication.delayCall += () =>
        {
            GameObject prefabContentsRoot = GetCurrentPrefabContentsRoot();
            if (prefabContentsRoot == null)
            {
                EditorGUIUtility.PingObject(prefabRoot);
                Selection.activeObject = prefabRoot;
                return;
            }

            Transform target = FindByHierarchyPath(prefabContentsRoot.transform, issue.hierarchyPath);
            if (target == null)
                target = prefabContentsRoot.transform;

            Selection.activeObject = target.gameObject;
            EditorGUIUtility.PingObject(target.gameObject);

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        };
    }

    private static GameObject GetCurrentPrefabContentsRoot()
    {
        // PrefabStage was experimental in Unity 2018.3-2020.x, moved namespaces
        // in 2021.2, and does not exist in Unity 2017. Reflection keeps one binary
        // source compatible across all of those editor versions.
        string[] utilityTypeNames =
        {
            "UnityEditor.SceneManagement.PrefabStageUtility",
            "UnityEditor.Experimental.SceneManagement.PrefabStageUtility"
        };

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int nameIndex = 0; nameIndex < utilityTypeNames.Length; nameIndex++)
        {
            Type utilityType = null;
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length && utilityType == null; assemblyIndex++)
                utilityType = assemblies[assemblyIndex].GetType(utilityTypeNames[nameIndex]);

            if (utilityType == null)
                continue;

            MethodInfo getCurrentStage = utilityType.GetMethod("GetCurrentPrefabStage", BindingFlags.Public | BindingFlags.Static);
            if (getCurrentStage == null)
                continue;

            object stage = getCurrentStage.Invoke(null, null);
            if (stage == null)
                return null;

            PropertyInfo contentsRootProperty = stage.GetType().GetProperty("prefabContentsRoot", BindingFlags.Public | BindingFlags.Instance);
            return contentsRootProperty != null ? contentsRootProperty.GetValue(stage, null) as GameObject : null;
        }

        return null;
    }

    private void ApplyFolderAsset()
    {
        if (targetFolderAsset == null)
            return;

        string path = AssetDatabase.GetAssetPath(targetFolderAsset);
        if (AssetDatabase.IsValidFolder(path))
        {
            targetFolderPath = path;
            return;
        }

        EditorUtility.DisplayDialog("路径无效", "请选择 Assets 下的文件夹。", "确定");
        targetFolderAsset = null;
    }

    private void PickFolder()
    {
        string selectedPath = EditorUtility.OpenFolderPanel("选择检测路径", Application.dataPath, "");
        if (string.IsNullOrEmpty(selectedPath))
            return;

        if (!selectedPath.StartsWith(Application.dataPath))
        {
            EditorUtility.DisplayDialog("路径无效", "请选择项目 Assets 下的文件夹。", "确定");
            return;
        }

        targetFolderPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
        targetFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(targetFolderPath);
    }

    private void EnsureTargetFolderAsset()
    {
        if (targetFolderAsset == null && AssetDatabase.IsValidFolder(targetFolderPath))
            targetFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(targetFolderPath);
    }

    private static string GetHierarchyPath(Transform root, Transform target)
    {
        if (root == null || target == null)
            return string.Empty;

        List<string> names = new List<string>();
        Transform current = target;
        while (current != null)
        {
            names.Add(current.name);
            if (current == root)
                break;

            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names.ToArray());
    }

    private static Transform FindByHierarchyPath(Transform root, string hierarchyPath)
    {
        if (root == null || string.IsNullOrEmpty(hierarchyPath))
            return null;

        string[] parts = hierarchyPath.Split('/');
        int startIndex = parts.Length > 0 && parts[0] == root.name ? 1 : 0;
        Transform current = root;

        for (int i = startIndex; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null)
                return null;
        }

        return current;
    }
}
}
