using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace ArtTools.EditorTools
{
    public sealed class MissingScriptCheckerWindow : EditorWindow
    {
        private readonly List<Result> results = new List<Result>();
        private Vector2 scrollPosition;
        private string[] lastScanFolders = { "Assets" };
        private string scanFolderPath = "Assets";
        private double lastScanSeconds;

        public static void Open()
        {
            var window = GetWindow<MissingScriptCheckerWindow>("Missing Script 检查");
            window.minSize = new Vector2(413, 440);
            window.maxSize = new Vector2(667, 1600);
            Rect position = window.position;
            position.width = 420;
            window.position = position;
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            scanFolderPath = GetInitialFolderFromSelection();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Missing Script 检查", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("扫描 Prefab 及其全部子节点，不会修改或删除组件。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(5);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("扫描路径", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField(scanFolderPath);
                    EditorGUI.EndDisabledGroup();
                    if (GUILayout.Button("选择…", GUILayout.Width(64))) ChooseScanFolder();
                }
                if (GUILayout.Button("扫描当前路径", GUILayout.Height(34))) Scan(new[] { scanFolderPath });
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("全盘扫描", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("扫描整个 Assets；资源较多时耗时会更长。", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("扫描整个 Assets", GUILayout.Height(28))) Scan(new[] { "Assets" });
            EditorGUILayout.Space(8);

            int missingTotal = results.Sum(item => item.MissingCount);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"异常：{results.Count}    Missing：{missingTotal}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUI.enabled = results.Count > 0;
                if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(72), GUILayout.Height(24))) results.Clear();
                GUI.enabled = true;
            }
            EditorGUILayout.LabelField($"用时：{lastScanSeconds:0.00}s", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("扫描范围：" + string.Join("  |  ", lastScanFolders), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scroll.scrollPosition;
                foreach (Result result in results) DrawResult(result);
            }
        }

        private void DrawResult(Result result)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(result.Name, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Missing: " + result.MissingCount, GUILayout.Width(72));
                }
                EditorGUILayout.SelectableLabel(result.AssetPath, EditorStyles.miniLabel, GUILayout.Height(18));
                EditorGUILayout.LabelField("节点：" + string.Join(", ", result.DisplayNodes), EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("定位并打开 Prefab", GUILayout.Height(24))) Locate(result);
            }
        }

        private void Scan(string[] folders)
        {
            results.Clear();
            lastScanFolders = folders;
            var stopwatch = Stopwatch.StartNew();
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", folders);
            try
            {
                for (int index = 0; index < prefabGuids.Length; index++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
                    if (EditorUtility.DisplayCancelableProgressBar("Missing Script 检查", $"正在扫描 {index + 1}/{prefabGuids.Length}\n{path}", prefabGuids.Length == 0 ? 1f : (float)index / prefabGuids.Length)) break;
                    Result result = InspectPrefab(path);
                    if (result != null) results.Add(result);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                stopwatch.Stop();
                lastScanSeconds = stopwatch.Elapsed.TotalSeconds;
            }

            results.Sort((left, right) => string.CompareOrdinal(left.AssetPath, right.AssetPath));
            Debug.Log($"[Missing Script 检查] 异常 Prefab={results.Count}, Missing={results.Sum(item => item.MissingCount)}, 用时={lastScanSeconds:0.00}s, 范围={string.Join(",", folders)}");
            Repaint();
        }

        public static int[] AuditFolder(string folder)
        {
            int brokenPrefabs = 0;
            int missingScripts = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                Result result = InspectPrefab(AssetDatabase.GUIDToAssetPath(guid));
                if (result == null) continue;
                brokenPrefabs++;
                missingScripts += result.MissingCount;
            }
            return new[] { brokenPrefabs, missingScripts };
        }

        private static Result InspectPrefab(string assetPath)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(assetPath);
                var displayNodes = new List<string>();
                string firstRelativeNode = null;
                int missingCount = 0;
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    int nodeCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if (nodeCount <= 0) continue;
                    string relativePath = GetRelativePath(transform, root.transform);
                    string displayPath = string.IsNullOrEmpty(relativePath) ? root.name : root.name + "/" + relativePath;
                    displayNodes.Add(nodeCount == 1 ? displayPath : $"{displayPath} ({nodeCount})");
                    if (firstRelativeNode == null) firstRelativeNode = relativePath;
                    missingCount += nodeCount;
                }
                return missingCount == 0 ? null : new Result(assetPath, missingCount, displayNodes, firstRelativeNode ?? string.Empty);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Missing Script 检查] 无法读取 Prefab：{assetPath}\n{exception.Message}");
                return null;
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Locate(Result result)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.AssetPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("定位失败", "无法加载 Prefab：\n" + result.AssetPath, "确定");
                return;
            }
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            if (!AssetDatabase.OpenAsset(prefab)) return;
            SelectMissingNodeWhenReady(result.AssetPath, result.FirstRelativeNode, 8);
        }

        private static void SelectMissingNodeWhenReady(string assetPath, string relativeNodePath, int attemptsRemaining)
        {
            EditorApplication.delayCall += () =>
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage == null || stage.assetPath != assetPath)
                {
                    if (attemptsRemaining > 0) SelectMissingNodeWhenReady(assetPath, relativeNodePath, attemptsRemaining - 1);
                    return;
                }
                Transform target = string.IsNullOrEmpty(relativeNodePath) ? stage.prefabContentsRoot.transform : stage.prefabContentsRoot.transform.Find(relativeNodePath);
                if (target == null) return;
                Selection.activeGameObject = target.gameObject;
                EditorGUIUtility.PingObject(target.gameObject);
            };
        }

        private static string GetRelativePath(Transform current, Transform root)
        {
            if (current == root) return string.Empty;
            var names = new Stack<string>();
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private void ChooseScanFolder()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string currentAbsolutePath = Path.GetFullPath(Path.Combine(projectRoot, scanFolderPath));
            string selectedAbsolutePath = EditorUtility.OpenFolderPanel("选择 Prefab 扫描目录", currentAbsolutePath, string.Empty);
            if (string.IsNullOrEmpty(selectedAbsolutePath)) return;

            string normalizedAssetsPath = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedSelectedPath = Path.GetFullPath(selectedAbsolutePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool isAssetsRoot = string.Equals(normalizedSelectedPath, normalizedAssetsPath, StringComparison.OrdinalIgnoreCase);
            bool isInsideAssets = normalizedSelectedPath.StartsWith(normalizedAssetsPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (!isAssetsRoot && !isInsideAssets)
            {
                EditorUtility.DisplayDialog("路径无效", "请选择当前 Unity 项目的 Assets 目录或其子目录。", "确定");
                return;
            }

            scanFolderPath = isAssetsRoot
                ? "Assets"
                : "Assets" + normalizedSelectedPath.Substring(normalizedAssetsPath.Length).Replace('\\', '/');
            Repaint();
        }

        private static string GetInitialFolderFromSelection()
        {
            foreach (Object selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (string.IsNullOrEmpty(path)) continue;
                if (!AssetDatabase.IsValidFolder(path)) path = Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path)) return path;
            }
            return "Assets";
        }

        private sealed class Result
        {
            public readonly string AssetPath;
            public readonly string Name;
            public readonly int MissingCount;
            public readonly List<string> DisplayNodes;
            public readonly string FirstRelativeNode;

            public Result(string assetPath, int missingCount, List<string> displayNodes, string firstRelativeNode)
            {
                AssetPath = assetPath;
                Name = Path.GetFileName(assetPath);
                MissingCount = missingCount;
                DisplayNodes = displayNodes;
                FirstRelativeNode = firstRelativeNode;
            }
        }
    }
}
