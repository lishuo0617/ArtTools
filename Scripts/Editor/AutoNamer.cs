using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Object = UnityEngine.Object;

namespace ArtTools.EditorTools
{
    public class AutoNamer : EditorWindow
    {
        private string nameSegment = "Q";
        private bool mainNumberEnabled = true;
        private string mainSeparator = "_";
        private int mainStart = 1;
        private int mainStep = 1;
        private int mainDigits = 2;
        private bool subNumberEnabled = false;
        private string subSeparator = "_";
        private int subStart = 1;
        private int subStep = 1;
        private int subDigits = 2;
        private Vector2 scrollPos;
        private static readonly List<GameObject> sceneSelectionOrder = new List<GameObject>();
        private static readonly List<Object> projectSelectionOrder = new List<Object>();

        private sealed class AssetRenamePlan
        {
            public Object Asset;
            public string OriginalPath;
            public string OriginalName;
            public string TargetName;
            public string TemporaryName;
        }

        public static void ShowWindow()
        {
            AutoNamer window = GetWindow<AutoNamer>("批量命名修改");
            window.minSize = new Vector2(460, 620);
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private static void OnSelectionChanged()
        {
            Object current = Selection.activeObject;
            if (IsValidProjectAsset(current))
            {
                AddProjectSelection(current);

                // Project 框选或 Shift 多选时可能只报告一个 activeObject。
                // 将其余新选中的主资源按 Unity 返回顺序追加，确保都能批量改名。
                foreach (Object selected in Selection.objects)
                {
                    if (IsValidProjectAsset(selected))
                        AddProjectSelection(selected);
                }
            }
            else
            {
                SyncSceneSelection();
            }

            PruneSelectionOrder();
            foreach (AutoNamer window in Resources.FindObjectsOfTypeAll<AutoNamer>())
                window.Repaint();
        }

        private static void AddProjectSelection(Object asset)
        {
            if (!projectSelectionOrder.Contains(asset))
                projectSelectionOrder.Add(asset);
        }

        private static void SyncSceneSelection()
        {
            GameObject[] selectedObjects = Selection.gameObjects
                .Where(obj => obj != null && !EditorUtility.IsPersistent(obj))
                .ToArray();

            HashSet<GameObject> selectedSet = new HashSet<GameObject>(selectedObjects);
            sceneSelectionOrder.RemoveAll(obj => obj == null || !selectedSet.Contains(obj));

            int newlySelectedCount = selectedObjects.Count(obj => !sceneSelectionOrder.Contains(obj));
            if (newlySelectedCount > 1)
            {
                // Shift 区间选择、框选和全选会一次加入多个对象。
                // 此时按 Hierarchy 可见顺序重建，确保预览编号从上到下连续。
                sceneSelectionOrder.Clear();
                sceneSelectionOrder.AddRange(selectedObjects
                    .OrderBy(GetHierarchyOrderKey, StringComparer.Ordinal));
                return;
            }

            GameObject activeObject = Selection.activeGameObject;
            if (activeObject != null &&
                selectedSet.Contains(activeObject) &&
                !sceneSelectionOrder.Contains(activeObject))
            {
                sceneSelectionOrder.Add(activeObject);
            }

            // 某些 Unity 版本在区间只增加一个对象时，activeObject 不一定可靠。
            foreach (GameObject selectedObject in selectedObjects)
            {
                if (!sceneSelectionOrder.Contains(selectedObject))
                    sceneSelectionOrder.Add(selectedObject);
            }
        }

        private static string GetHierarchyOrderKey(GameObject obj)
        {
            var siblingPath = new Stack<int>();
            Transform current = obj.transform;
            while (current != null)
            {
                siblingPath.Push(current.GetSiblingIndex());
                current = current.parent;
            }

            return obj.scene.handle.ToString("D10") + "/" +
                   string.Join("/", siblingPath.Select(index => index.ToString("D10")));
        }

        private static bool IsValidProjectAsset(Object obj)
        {
            if (obj == null || !EditorUtility.IsPersistent(obj) || !AssetDatabase.IsMainAsset(obj))
                return false;

            string path = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                   !AssetDatabase.IsValidFolder(path);
        }

        private void OnGUI()
        {
            PruneSelectionOrder();

            bool hasSceneObjects = sceneSelectionOrder.Count > 0;
            bool hasProjectAssets = projectSelectionOrder.Count > 0;
            bool mixedSelection = hasSceneObjects && hasProjectAssets;

            ArtToolsEditorUI.DrawHeader("批量命名修改", "支持 Hierarchy 场景对象与 Project 工程资源", "d_TextAsset Icon");

            ArtToolsEditorUI.BeginPanel("名称结构");
            DrawNamingStructureFields();
            ArtToolsEditorUI.EndPanel();

            ArtToolsEditorUI.BeginPanel("最终效果预览");
            DrawExamplePreview();
            ArtToolsEditorUI.EndPanel();

            int selectionCount = hasProjectAssets ? projectSelectionOrder.Count : sceneSelectionOrder.Count;
            string selectionSource = hasProjectAssets ? "Project 资源" : "Hierarchy 对象";
            ArtToolsEditorUI.BeginPanel($"所选 {selectionSource}（{selectionCount}）");
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MinHeight(100));
            if (!hasSceneObjects && !hasProjectAssets)
                ArtToolsEditorUI.EmptyState("暂无对象。请在 Hierarchy 或 Project 面板中选择要重命名的内容。");

            if (hasProjectAssets)
            {
                for (int i = 0; i < projectSelectionOrder.Count; i++)
                    DrawSelectionRow(i, projectSelectionOrder[i], false, GetOriginalName(projectSelectionOrder[i]));
            }
            else
            {
                for (int i = 0; i < sceneSelectionOrder.Count; i++)
                    DrawSelectionRow(i, sceneSelectionOrder[i], true, sceneSelectionOrder[i].name);
            }

            EditorGUILayout.EndScrollView();
            ArtToolsEditorUI.EndPanel();

            if (mixedSelection)
                EditorGUILayout.HelpBox("请不要同时选择 Hierarchy 场景对象和 Project 资源。", MessageType.Warning);

            List<string> blockingIssues = GetBlockingPreviewIssues(hasProjectAssets);
            foreach (string issue in blockingIssues.Take(3))
                EditorGUILayout.HelpBox(issue, MessageType.Error);
            if (blockingIssues.Count > 3)
                EditorGUILayout.HelpBox($"还有 {blockingIssues.Count - 3} 个问题，请调整命名规则。", MessageType.Error);

            ArtToolsEditorUI.BeginPanel("操作");
            string primaryButtonText = hasProjectAssets ? "批量修改资源名称" : "命名 + 自动整体排序";
            GUI.enabled = !mixedSelection && blockingIssues.Count == 0 && selectionCount > 0;
            if (ArtToolsEditorUI.PrimaryButton(primaryButtonText))
                RenameAndSortAll();

            if (!hasProjectAssets)
            {
                if (GUILayout.Button("排序调整", GUILayout.Height(26)))
                    SortSelectedObjectsByNumericName();
            }
            else
            {
                EditorGUILayout.HelpBox("Project 资源会保留文件类型和扩展名；Project 面板将按新名称自动排列。", MessageType.Info);
            }

            GUI.enabled = true;
            ArtToolsEditorUI.EndPanel();
        }

        private void DrawNamingStructureFields()
        {
            nameSegment = EditorGUILayout.TextField("类型 / 名称", nameSegment);

            GUILayout.Space(4);
            mainNumberEnabled = EditorGUILayout.ToggleLeft("使用主编号", mainNumberEnabled);
            EditorGUI.BeginDisabledGroup(!mainNumberEnabled);
            mainSeparator = EditorGUILayout.TextField("主编号前连接符", mainSeparator);
            mainStart = EditorGUILayout.IntField("主编号起始值", mainStart);
            mainStep = EditorGUILayout.IntField("主编号每次增加", mainStep);
            mainDigits = EditorGUILayout.IntSlider("主编号数字位数", mainDigits, 1, 8);
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(4);
            EditorGUI.BeginDisabledGroup(!mainNumberEnabled);
            subNumberEnabled = EditorGUILayout.ToggleLeft("使用子编号", subNumberEnabled);
            EditorGUI.EndDisabledGroup();
            if (!mainNumberEnabled)
                subNumberEnabled = false;

            EditorGUI.BeginDisabledGroup(!subNumberEnabled);
            subSeparator = EditorGUILayout.TextField("子编号前连接符", subSeparator);
            subStart = EditorGUILayout.IntField("子编号起始值", subStart);
            subStep = EditorGUILayout.IntField("子编号每次增加", subStep);
            subDigits = EditorGUILayout.IntSlider("子编号数字位数", subDigits, 1, 8);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox("“每次增加”设为 0，可让该编号保持固定。连接符可以填写 _、- 或留空。", MessageType.Info);
        }

        private void DrawExamplePreview()
        {
            GUIStyle previewStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft
            };

            for (int i = 0; i < 3; i++)
                EditorGUILayout.LabelField($"{i + 1}.  {BuildTargetName(i)}", previewStyle, GUILayout.Height(22));
        }

        private void DrawSelectionRow(int index, Object obj, bool allowSceneObjects, string originalName)
        {
            EditorGUILayout.BeginVertical(ArtToolsEditorUI.RowStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{index + 1}.", GUILayout.Width(24));
            EditorGUILayout.ObjectField(obj, typeof(Object), allowSceneObjects);
            EditorGUILayout.EndHorizontal();
            string targetName = BuildTargetName(index);
            EditorGUILayout.LabelField($"{originalName}  →  {targetName}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private string BuildTargetName(int selectionIndex)
        {
            string result = nameSegment ?? string.Empty;

            if (mainNumberEnabled)
            {
                int mainValue = mainStart + selectionIndex * mainStep;
                result += mainSeparator + FormatNumber(mainValue, mainDigits);

                if (subNumberEnabled)
                {
                    int subValue = subStart + selectionIndex * subStep;
                    result += subSeparator + FormatNumber(subValue, subDigits);
                }
            }

            return result;
        }

        private static string FormatNumber(int value, int digits)
        {
            return value.ToString("D" + Mathf.Clamp(digits, 1, 8));
        }

        private static string GetOriginalName(Object obj)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            return string.IsNullOrEmpty(path) ? obj.name : Path.GetFileNameWithoutExtension(path);
        }

        private List<string> GetBlockingPreviewIssues(bool projectMode)
        {
            List<string> issues = new List<string>();

            if (!projectMode)
            {
                HashSet<string> siblingTargetNames = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < sceneSelectionOrder.Count; i++)
                {
                    string target = BuildTargetName(i);
                    if (string.IsNullOrEmpty(target) || target.Trim().Length == 0)
                        issues.Add($"第 {i + 1} 个对象的新名称为空。");

                    Transform parent = sceneSelectionOrder[i].transform.parent;
                    string key = (parent == null ? "ROOT" : parent.GetInstanceID().ToString()) + "|" + target;
                    if (!siblingTargetNames.Add(key))
                        issues.Add($"同一父节点下会生成重复名称：{target}");
                }

                return issues;
            }

            string invalidReason = ValidateProjectNamePattern();
            if (!string.IsNullOrEmpty(invalidReason))
                issues.Add(invalidReason);

            if (issues.Count == 0 && projectSelectionOrder.Count > 0)
            {
                string conflict = FindExternalTargetConflict(BuildAssetRenamePlans());
                if (!string.IsNullOrEmpty(conflict))
                    issues.Add(conflict);
            }

            return issues;
        }

        private static string ValidateTargetName(string targetName)
        {
            if (string.IsNullOrEmpty(targetName) || targetName.Trim().Length == 0)
                return "名称不能为空。";

            if (targetName == "." || targetName == "..")
                return "不能使用保留名称 “.” 或 “..”。";

            if (targetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                targetName.Contains("/") || targetName.Contains("\\"))
            {
                return "包含 Windows 或 Unity 资源路径不允许的字符。";
            }

            return null;
        }

        private void RenameAndSortAll()
        {
            PruneSelectionOrder();

            if (projectSelectionOrder.Count > 0 && sceneSelectionOrder.Count > 0)
            {
                EditorUtility.DisplayDialog("提示", "请不要同时选择 Hierarchy 场景对象和 Project 资源。", "OK");
                return;
            }

            if (projectSelectionOrder.Count > 0)
            {
                RenameProjectAssets();
                return;
            }

            if (sceneSelectionOrder.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未选择任何对象", "OK");
                return;
            }

            // Hierarchy 原有逻辑保持不变。
            Undo.RecordObjects(sceneSelectionOrder.ToArray(), "Auto Rename");

            Transform parent = sceneSelectionOrder[0].transform.parent;
            for (int i = 0; i < sceneSelectionOrder.Count; i++)
            {
                GameObject obj = sceneSelectionOrder[i];
                obj.name = BuildTargetName(i);
            }

            SortHierarchyByNumericName(parent);
            EditorUtility.DisplayDialog("完成", $"已命名并排序 {sceneSelectionOrder.Count} 个对象", "OK");
        }

        private void RenameProjectAssets()
        {
            string invalidNameReason = ValidateProjectNamePattern();
            if (!string.IsNullOrEmpty(invalidNameReason))
            {
                EditorUtility.DisplayDialog("名称无效", invalidNameReason, "OK");
                return;
            }

            List<AssetRenamePlan> plans = BuildAssetRenamePlans();
            string conflict = FindExternalTargetConflict(plans);
            if (!string.IsNullOrEmpty(conflict))
            {
                EditorUtility.DisplayDialog("无法批量命名", conflict, "OK");
                return;
            }

            try
            {
                // 第一阶段先移到唯一临时名称，避免同目录内名称互换或编号占用。
                foreach (AssetRenamePlan plan in plans)
                {
                    string error = AssetDatabase.RenameAsset(plan.OriginalPath, plan.TemporaryName);
                    if (!string.IsNullOrEmpty(error))
                        throw new InvalidOperationException($"临时重命名失败：{plan.OriginalPath}\n{error}");
                }

                // 第二阶段写入最终名称。RenameAsset 会保留扩展名和 .meta GUID。
                foreach (AssetRenamePlan plan in plans)
                {
                    string currentPath = AssetDatabase.GetAssetPath(plan.Asset);
                    string error = AssetDatabase.RenameAsset(currentPath, plan.TargetName);
                    if (!string.IsNullOrEmpty(error))
                        throw new InvalidOperationException($"重命名失败：{currentPath}\n{error}");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.objects = projectSelectionOrder.Where(IsValidProjectAsset).ToArray();
                EditorUtility.DisplayDialog("完成", $"已批量修改 {plans.Count} 个 Project 资源名称。", "OK");
            }
            catch (Exception exception)
            {
                RollbackProjectAssetNames(plans);
                AssetDatabase.Refresh();
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("批量命名失败", exception.Message + "\n已尝试恢复原名称。", "OK");
            }
        }

        private List<AssetRenamePlan> BuildAssetRenamePlans()
        {
            List<AssetRenamePlan> plans = new List<AssetRenamePlan>();
            for (int i = 0; i < projectSelectionOrder.Count; i++)
            {
                Object asset = projectSelectionOrder[i];
                string path = AssetDatabase.GetAssetPath(asset);
                string originalName = Path.GetFileNameWithoutExtension(path);
                plans.Add(new AssetRenamePlan
                {
                    Asset = asset,
                    OriginalPath = path,
                    OriginalName = originalName,
                    TargetName = BuildTargetName(i),
                    TemporaryName = "__ArtTools_AutoNamer_" + AssetDatabase.AssetPathToGUID(path)
                });
            }

            return plans;
        }

        private static string FindExternalTargetConflict(IEnumerable<AssetRenamePlan> plans)
        {
            List<AssetRenamePlan> planList = plans.ToList();
            HashSet<string> selectedPaths = new HashSet<string>(
                planList.Select(plan => plan.OriginalPath),
                StringComparer.OrdinalIgnoreCase);

            HashSet<string> targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AssetRenamePlan plan in planList)
            {
                string directory = Path.GetDirectoryName(plan.OriginalPath)?.Replace('\\', '/');
                string extension = Path.GetExtension(plan.OriginalPath);
                string targetPath = directory + "/" + plan.TargetName + extension;
                if (!targetPaths.Add(targetPath))
                    return $"本次规则生成了重复名称：\n{targetPath}";

                Object existing = AssetDatabase.LoadMainAssetAtPath(targetPath);
                if (existing != null && !selectedPaths.Contains(targetPath))
                    return $"目标名称已被未选中的资源占用：\n{targetPath}";
            }

            return null;
        }

        private string ValidateProjectNamePattern()
        {
            for (int i = 0; i < projectSelectionOrder.Count; i++)
            {
                string targetName = BuildTargetName(i);
                string error = ValidateTargetName(targetName);
                if (!string.IsNullOrEmpty(error))
                    return $"第 {i + 1} 个资源名称无效：{error}";
            }

            return null;
        }

        private static void RollbackProjectAssetNames(IEnumerable<AssetRenamePlan> plans)
        {
            List<AssetRenamePlan> planList = plans.ToList();

            // 先腾出全部原名称，再逐个恢复，避免回滚时出现名称互相占用。
            for (int i = 0; i < planList.Count; i++)
            {
                AssetRenamePlan plan = planList[i];
                string currentPath = AssetDatabase.GetAssetPath(plan.Asset);
                if (!string.IsNullOrEmpty(currentPath))
                {
                    AssetDatabase.RenameAsset(
                        currentPath,
                        "__ArtTools_Rollback_" + i + "_" + Guid.NewGuid().ToString("N"));
                }
            }

            foreach (AssetRenamePlan plan in planList)
            {
                string currentPath = AssetDatabase.GetAssetPath(plan.Asset);
                if (!string.IsNullOrEmpty(currentPath))
                    AssetDatabase.RenameAsset(currentPath, plan.OriginalName);
            }
        }

        private void SortSelectedObjectsByNumericName()
        {
            PruneSelectionOrder();

            GameObject[] selectedObjects = Selection.gameObjects
                .Where(obj => obj != null && !EditorUtility.IsPersistent(obj))
                .ToArray();

            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先在场景中选择要排序的对象或父节点。", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Sort Selected Objects By Numeric Name");
            int undoGroup = Undo.GetCurrentGroup();
            int sortedCount = 0;

            if (selectedObjects.Length == 1 && selectedObjects[0].transform.childCount > 0)
            {
                Transform parent = selectedObjects[0].transform;
                sortedCount += SortTransformsByNumericName(GetChildren(parent), false);
            }
            else
            {
                foreach (IGrouping<Transform, GameObject> group in selectedObjects.GroupBy(obj => obj.transform.parent))
                    sortedCount += SortTransformsByNumericName(group.Select(obj => obj.transform).ToList(), true);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.DisplayDialog("完成", $"已按编号顺序调整 {sortedCount} 个对象。", "OK");
        }

        private static List<Transform> GetChildren(Transform parent)
        {
            List<Transform> children = new List<Transform>();
            for (int i = 0; i < parent.childCount; i++)
                children.Add(parent.GetChild(i));

            return children;
        }

        private int SortTransformsByNumericName(List<Transform> transforms, bool keepOriginalSlots)
        {
            if (transforms == null || transforms.Count == 0)
                return 0;

            List<Transform> sortedTransforms = transforms
                .Where(transform => transform != null)
                .OrderBy(transform => ExtractMainNumber(transform.name))
                .ThenBy(transform => ExtractSubNumber(transform.name))
                .ThenBy(transform => transform.name)
                .ToList();

            Undo.RecordObjects(sortedTransforms.ToArray(), "Sort By Numeric Name");

            if (keepOriginalSlots)
            {
                List<int> siblingIndexes = transforms
                    .Where(transform => transform != null)
                    .Select(transform => transform.GetSiblingIndex())
                    .OrderBy(index => index)
                    .ToList();

                for (int i = 0; i < sortedTransforms.Count; i++)
                    sortedTransforms[i].SetSiblingIndex(siblingIndexes[i]);

                return sortedTransforms.Count;
            }

            for (int i = 0; i < sortedTransforms.Count; i++)
                sortedTransforms[i].SetSiblingIndex(i);

            return sortedTransforms.Count;
        }

        private void SortHierarchyByNumericName(Transform parent)
        {
            if (parent == null)
                return;

            SortTransformsByNumericName(GetChildren(parent), false);
        }

        private static void PruneSelectionOrder()
        {
            HashSet<GameObject> selectedSceneObjects = new HashSet<GameObject>(
                Selection.gameObjects.Where(obj => obj != null && !EditorUtility.IsPersistent(obj)));
            HashSet<Object> selectedProjectAssets = new HashSet<Object>(
                Selection.objects.Where(IsValidProjectAsset));

            sceneSelectionOrder.RemoveAll(obj => obj == null || !selectedSceneObjects.Contains(obj));
            projectSelectionOrder.RemoveAll(obj => obj == null || !selectedProjectAssets.Contains(obj));
        }

        private int ExtractMainNumber(string name)
        {
            Match match = Regex.Match(name, @"(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
        }

        private int ExtractSubNumber(string name)
        {
            MatchCollection matches = Regex.Matches(name, @"(\d+)");
            return matches.Count >= 2 ? int.Parse(matches[1].Value) : int.MaxValue;
        }
    }
}
