#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace ArtTools.EditorTools
{

public class DecorationTool : EditorWindow
{
    [System.Serializable]
    public class DecorationPrefab
    {
        [InspectorName("预制体")]
        public GameObject prefab;
        [InspectorName("最大数量")]
        public int maxCount = 50;
        [HideInInspector] public int generatedCount = 0;
    }

    [System.Serializable]
    public class PrefabReplacePair
    {
        [InspectorName("旧 Prefab")]
        public GameObject oldPrefab;
        [InspectorName("新 Prefab")]
        public GameObject newPrefab;
    }

    // --- UI Tabs ---
    private int tabIndex = 0;
    private readonly string[] tabs = new[] { "生成器", "随机器", "批量Prefab替换器" };
    private Vector2 mainScroll;
    private Vector2 spawnerScroll;


    public GameObject targetObject;
    [InspectorName("装饰物列表")]
    public List<DecorationPrefab> decorations = new List<DecorationPrefab>();


    public Vector3 posRange = new Vector3(1, 0, 1);
    public Vector2 rotXRange = new Vector2(0, 0);
    public Vector2 rotYRange = new Vector2(0, 0);
    public Vector2 rotZRange = new Vector2(0, 0);
    public Vector2 scaleXZ = new Vector2(0.8f, 1.2f);
    public Vector2 scaleY = new Vector2(0.8f, 1.2f);

    [InspectorName("替换映射列表")]
    public List<PrefabReplacePair> replacePairs = new List<PrefabReplacePair>();
    public bool replaceInSelectionOnly = true;
    public GameObject selectedReplacementPrefab;

    public static void ShowWindow()
    {
        DecorationTool window = GetWindow<DecorationTool>("装饰物工具");
        window.minSize = new Vector2(560, 500);
        window.Show();
    }

    private void OnEnable()
    {
        LoadPrefs();
    }

    private void OnDisable()
    {
        SavePrefs();
    }

    private void OnGUI()
    {
        ArtToolsEditorUI.DrawHeader("装饰物工具", "生成装饰物、随机调整并批量替换 Prefab", "d_TerrainInspector.TerrainToolPlants");

        ArtToolsEditorUI.BeginPanel("");
        tabIndex = GUILayout.Toolbar(tabIndex, tabs, GUILayout.Height(24));
        ArtToolsEditorUI.EndPanel();

        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

        switch (tabIndex)
        {
            case 0:
                DrawSpawnerTab();
                break;
            case 1:
                DrawRandomizerTab();
                break;
            case 2:
                DrawPrefabReplacerTab();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    #region

private void DrawSpawnerTab()
{
    ArtToolsEditorUI.BeginPanel("生成设置");
    targetObject = (GameObject)EditorGUILayout.ObjectField("目标对象", targetObject, typeof(GameObject), true);

    GUILayout.Space(8);
    EditorGUILayout.LabelField("装饰物 Prefab 列表（含数量限制）", EditorStyles.boldLabel);

    spawnerScroll = EditorGUILayout.BeginScrollView(spawnerScroll, GUILayout.Height(300));
    SerializedObject so = new SerializedObject(this);
    SerializedProperty listProp = so.FindProperty("decorations");
    EditorGUILayout.PropertyField(listProp, true);
    so.ApplyModifiedProperties();
    EditorGUILayout.EndScrollView();
    ArtToolsEditorUI.EndPanel();

    ArtToolsEditorUI.BeginPanel("操作");
    if (ArtToolsEditorUI.PrimaryButton("生成装饰物"))
    {
        if (targetObject == null || decorations.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先设置目标对象和 Prefab 列表。", "确定");
            return;
        }
        SpawnDecorations();
    }
    ArtToolsEditorUI.EndPanel();
}

private void SpawnDecorations()
{
    foreach (var deco in decorations) deco.generatedCount = 0;

    Undo.SetCurrentGroupName("Generate Decorations");
    int undoGroup = Undo.GetCurrentGroup();


    GameObject decorationRoot = GameObject.Find("装饰物");
    if (decorationRoot == null)
    {
        decorationRoot = new GameObject("装饰物");
        decorationRoot.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(decorationRoot, "Create Decoration Root");
    }


    Dictionary<DecorationPrefab, GameObject> categoryParents = new Dictionary<DecorationPrefab, GameObject>();
    foreach (var deco in decorations)
    {
        if (deco.prefab == null) continue;
        GameObject catRoot = new GameObject(deco.prefab.name);
        catRoot.transform.SetParent(decorationRoot.transform);
        catRoot.transform.localPosition = Vector3.zero;
        categoryParents[deco] = catRoot;
        Undo.RegisterCreatedObjectUndo(catRoot, "Create Category Root");
    }


    List<Vector3> allVertices = new List<Vector3>();
    MeshFilter[] meshFilters = targetObject.GetComponentsInChildren<MeshFilter>();
    foreach (var mf in meshFilters)
    {
        if (mf.sharedMesh == null) continue;
        foreach (var v in mf.sharedMesh.vertices)
            allVertices.Add(mf.transform.TransformPoint(v));
    }

    if (allVertices.Count == 0)
    {
        Debug.LogWarning("未找到任何 Mesh 顶点。");
        return;
    }


    for (int i = 0; i < allVertices.Count; i++)
    {
        int r = Random.Range(i, allVertices.Count);
        (allVertices[i], allVertices[r]) = (allVertices[r], allVertices[i]);
    }

    int total = 0;
    foreach (var vert in allVertices)
    {
        var valid = decorations.FindAll(d => d.prefab != null && d.generatedCount < d.maxCount);
        if (valid.Count == 0) break;

        var deco = valid[Random.Range(0, valid.Count)];
        deco.generatedCount++;

        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(deco.prefab);
        Undo.RegisterCreatedObjectUndo(inst, "Spawn Decoration");

        inst.transform.position = vert;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;


        if (categoryParents.TryGetValue(deco, out GameObject parent))
            inst.transform.SetParent(parent.transform);
        else
            inst.transform.SetParent(decorationRoot.transform);

        total++;
    }

    Undo.CollapseUndoOperations(undoGroup);
    Debug.Log($"生成完成：{total} 个装饰物（按类别分组放入装饰物节点）");
}
#endregion


    #region
    private void DrawRandomizerTab()
    {
        ArtToolsEditorUI.BeginPanel("位置随机");
        posRange = EditorGUILayout.Vector3Field("范围 (±)", posRange);
        if (ArtToolsEditorUI.PrimaryButton("位置随机"))
            RandomizePosition();
        ArtToolsEditorUI.EndPanel();

        ArtToolsEditorUI.BeginPanel("旋转随机");
        rotXRange = EditorGUILayout.Vector2Field("X 轴 (min, max)", rotXRange);
        rotYRange = EditorGUILayout.Vector2Field("Y 轴 (min, max)", rotYRange);
        rotZRange = EditorGUILayout.Vector2Field("Z 轴 (min, max)", rotZRange);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("旋转随机", ArtToolsEditorUI.PrimaryButtonStyle))
            RandomizeRotation();

        if (GUILayout.Button("重置", GUILayout.Width(84), GUILayout.Height(30)))
        {
            rotXRange = Vector2.zero;
            rotYRange = Vector2.zero;
            rotZRange = Vector2.zero;
            SavePrefs();
        }
        EditorGUILayout.EndHorizontal();
        ArtToolsEditorUI.EndPanel();

        ArtToolsEditorUI.BeginPanel("缩放随机");
        scaleXZ = EditorGUILayout.Vector2Field("XZ 轴 (min, max)", scaleXZ);
        scaleY = EditorGUILayout.Vector2Field("Y 轴 (min, max)", scaleY);
        if (ArtToolsEditorUI.PrimaryButton("缩放随机"))
            RandomizeScale();
        ArtToolsEditorUI.EndPanel();
    }

    private void RandomizePosition()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            Undo.RecordObject(obj.transform, "Randomize Position");
            obj.transform.position += new Vector3(
                Random.Range(-posRange.x, posRange.x),
                Random.Range(-posRange.y, posRange.y),
                Random.Range(-posRange.z, posRange.z)
            );
        }
    }

    private void RandomizeRotation()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            Undo.RecordObject(obj.transform, "Randomize Rotation");

            float rx = Random.Range(rotXRange.x, rotXRange.y);
            float ry = Random.Range(rotYRange.x, rotYRange.y);
            float rz = Random.Range(rotZRange.x, rotZRange.y);

            obj.transform.rotation = Quaternion.Euler(rx, ry, rz);
        }
    }

    private void RandomizeScale()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            Undo.RecordObject(obj.transform, "Randomize Scale");
            float xz = Random.Range(scaleXZ.x, scaleXZ.y);
            float y = Random.Range(scaleY.x, scaleY.y);
            obj.transform.localScale = new Vector3(xz, y, xz);
        }
    }
    #endregion

    #region
    private void DrawPrefabReplacerTab()
    {
        ArtToolsEditorUI.BeginPanel("选中对象快速替换");
        selectedReplacementPrefab = (GameObject)EditorGUILayout.ObjectField(
            "替换为 Prefab",
            selectedReplacementPrefab,
            typeof(GameObject),
            false);

        if (ArtToolsEditorUI.PrimaryButton("用该 Prefab 替换当前选中对象"))
            ReplaceSelectedObjectsWithPrefab();

        EditorGUILayout.HelpBox(
            "先拖入一个 Prefab 资源，再在场景中选中任意模型或 Prefab 实例，点击按钮即可替换。会保留原物体的位置、旋转、缩放、父级、层级顺序和激活状态。",
            MessageType.Info);
        ArtToolsEditorUI.EndPanel();

        ArtToolsEditorUI.BeginPanel("按 Prefab 映射批量替换");
        replaceInSelectionOnly = EditorGUILayout.Toggle("仅替换选中对象", replaceInSelectionOnly);

        SerializedObject so = new SerializedObject(this);
        SerializedProperty pairsProp = so.FindProperty("replacePairs");
        EditorGUILayout.PropertyField(pairsProp, new GUIContent("替换映射列表"), true);
        so.ApplyModifiedProperties();

        GUILayout.Space(10);

        if (ArtToolsEditorUI.PrimaryButton("执行替换"))
            ExecuteReplacement();

        EditorGUILayout.HelpBox(
            "说明：\n" +
            "- 在上方添加要替换的 Prefab 对（旧 -> 新）\n" +
            "- 可选择仅替换选中对象或整个场景\n" +
            "- 替换会保留原物体的位置、旋转、缩放与父级\n" +
            "- 支持 Undo 撤销", MessageType.Info);
        ArtToolsEditorUI.EndPanel();
    }

    private void ReplaceSelectedObjectsWithPrefab()
    {
        if (selectedReplacementPrefab == null)
        {
            EditorUtility.DisplayDialog("提示", "请先拖入用于替换的 Prefab。", "确定");
            return;
        }

        if (!ArtToolsUnityCompatibility.IsPrefabAsset(selectedReplacementPrefab))
        {
            EditorUtility.DisplayDialog("提示", "替换目标必须是 Prefab 资源。", "确定");
            return;
        }

        List<GameObject> targets = GetTopLevelSceneSelection();
        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在场景中选择要替换的模型或 Prefab 实例。", "确定");
            return;
        }

        Undo.SetCurrentGroupName("Replace Selected Objects With Prefab");
        int undoGroup = Undo.GetCurrentGroup();

        List<GameObject> createdObjects = new List<GameObject>();
        foreach (GameObject target in targets)
        {
            Transform oldTransform = target.transform;
            Transform parent = oldTransform.parent;
            int siblingIndex = oldTransform.GetSiblingIndex();
            bool activeSelf = target.activeSelf;

            GameObject newObj = ArtToolsUnityCompatibility.InstantiatePrefab(selectedReplacementPrefab, parent);
            Undo.RegisterCreatedObjectUndo(newObj, "Replace Selected Object");

            newObj.transform.SetPositionAndRotation(oldTransform.position, oldTransform.rotation);
            newObj.transform.localScale = oldTransform.localScale;
            newObj.transform.SetSiblingIndex(siblingIndex);
            newObj.name = selectedReplacementPrefab.name;
            newObj.SetActive(activeSelf);

            Undo.DestroyObjectImmediate(target);
            createdObjects.Add(newObj);
        }

        Undo.CollapseUndoOperations(undoGroup);
        Selection.objects = createdObjects.ToArray();
        Debug.Log($"选中对象替换完成，共替换 {createdObjects.Count} 个对象。");
    }

    private List<GameObject> GetTopLevelSceneSelection()
    {
        HashSet<GameObject> selectedSet = new HashSet<GameObject>(Selection.gameObjects);
        List<GameObject> result = new List<GameObject>();

        foreach (GameObject selected in Selection.gameObjects)
        {
            if (selected == null || EditorUtility.IsPersistent(selected))
                continue;

            bool hasSelectedParent = false;
            Transform parent = selected.transform.parent;
            while (parent != null)
            {
                if (selectedSet.Contains(parent.gameObject))
                {
                    hasSelectedParent = true;
                    break;
                }

                parent = parent.parent;
            }

            if (!hasSelectedParent)
                result.Add(selected);
        }

        return result;
    }

    private void ExecuteReplacement()
    {
        if (replacePairs.Count == 0)
        {
            Debug.LogWarning("请先设置要替换的 Prefab 映射。");
            return;
        }

        int replaceCount = 0;
        List<GameObject> targets = new List<GameObject>();

        if (replaceInSelectionOnly)
            targets.AddRange(Selection.gameObjects);
        else
            targets.AddRange(FindObjectsOfType<GameObject>());

        Undo.SetCurrentGroupName("Batch Prefab Replace");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var pair in replacePairs)
        {
            if (pair.oldPrefab == null || pair.newPrefab == null) continue;

            foreach (GameObject target in targets)
            {
                GameObject prefabRoot = GetPrefabSourceCompatible(target);
                if (prefabRoot == pair.oldPrefab)
                {
                    Transform t = target.transform;

                    GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(pair.newPrefab);
                    newObj.transform.SetParent(t.parent, true);
                    newObj.transform.SetPositionAndRotation(t.position, t.rotation);
                    newObj.transform.localScale = t.localScale;
                    newObj.name = pair.newPrefab.name;

                    Undo.RegisterCreatedObjectUndo(newObj, "Replace Prefab");
                    Undo.DestroyObjectImmediate(target);

                    replaceCount++;
                }
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"替换完成，共替换 {replaceCount} 个 Prefab。");
    }

    private static GameObject GetPrefabSourceCompatible(GameObject target)
    {
#if UNITY_2018_2_OR_NEWER
        return PrefabUtility.GetCorrespondingObjectFromSource(target);
#else
        return PrefabUtility.GetPrefabParent(target) as GameObject;
#endif
    }
    #endregion

    #region
    private void SavePrefs()
    {
        EditorPrefs.SetFloat("DecorationTool_posRangeX", posRange.x);
        EditorPrefs.SetFloat("DecorationTool_posRangeY", posRange.y);
        EditorPrefs.SetFloat("DecorationTool_posRangeZ", posRange.z);

        EditorPrefs.SetFloat("DecorationTool_rotXMin", rotXRange.x);
        EditorPrefs.SetFloat("DecorationTool_rotXMax", rotXRange.y);
        EditorPrefs.SetFloat("DecorationTool_rotYMin", rotYRange.x);
        EditorPrefs.SetFloat("DecorationTool_rotYMax", rotYRange.y);
        EditorPrefs.SetFloat("DecorationTool_rotZMin", rotZRange.x);
        EditorPrefs.SetFloat("DecorationTool_rotZMax", rotZRange.y);

        EditorPrefs.SetFloat("DecorationTool_scaleXZMin", scaleXZ.x);
        EditorPrefs.SetFloat("DecorationTool_scaleXZMax", scaleXZ.y);
        EditorPrefs.SetFloat("DecorationTool_scaleYMin", scaleY.x);
        EditorPrefs.SetFloat("DecorationTool_scaleYMax", scaleY.y);
    }

    private void LoadPrefs()
    {
        posRange = new Vector3(
            EditorPrefs.GetFloat("DecorationTool_posRangeX", 1f),
            EditorPrefs.GetFloat("DecorationTool_posRangeY", 0f),
            EditorPrefs.GetFloat("DecorationTool_posRangeZ", 1f)
        );

        rotXRange = new Vector2(
            EditorPrefs.GetFloat("DecorationTool_rotXMin", 0f),
            EditorPrefs.GetFloat("DecorationTool_rotXMax", 0f)
        );
        rotYRange = new Vector2(
            EditorPrefs.GetFloat("DecorationTool_rotYMin", 0f),
            EditorPrefs.GetFloat("DecorationTool_rotYMax", 0f)
        );
        rotZRange = new Vector2(
            EditorPrefs.GetFloat("DecorationTool_rotZMin", 0f),
            EditorPrefs.GetFloat("DecorationTool_rotZMax", 0f)
        );

        scaleXZ = new Vector2(
            EditorPrefs.GetFloat("DecorationTool_scaleXZMin", 0.8f),
            EditorPrefs.GetFloat("DecorationTool_scaleXZMax", 1.2f)
        );
        scaleY = new Vector2(
            EditorPrefs.GetFloat("DecorationTool_scaleYMin", 0.8f),
            EditorPrefs.GetFloat("DecorationTool_scaleYMax", 1.2f)
        );
    }
    #endregion
}
}
#endif


