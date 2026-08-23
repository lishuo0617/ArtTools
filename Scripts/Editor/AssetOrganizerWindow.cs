#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace ArtTools.EditorTools
{

public class AssetOrganizerWindow : EditorWindow
{
    private const string DefaultShaderName = "Universal Render Pipeline/Simple Lit";

    [System.Serializable]
    public class BatchModelItem
    {
        public GameObject model;
        public string customName;
    }

    private GameObject singleFbx;
    private Texture2D texture;
    private string baseAssetName = "NewAsset";
    private Shader selectedShader;

    private bool enableBatchMode = false;
    private List<BatchModelItem> batchModels = new List<BatchModelItem>();

    private string modelsFolderPath = "Assets/Models";
    private string texturesFolderPath = "Assets/Textures";
    private string materialsFolderPath = "Assets/Materials";
    private string prefabsFolderPath = "Assets/Prefabs";

    private bool showAdvanced;
    private bool showFolders;
    private bool showInstructions = true;
    private Vector2 scroll;

    private string instructionsText = "欢迎使用3D资产整理工具！\n\n" +
                                      "单FBX模式：不要勾选批量选项，处理单个FBX模型和贴图！！\n" +
                                      "FBX内有多层嵌套Mesh模式：不要勾选批量选项，直接按照单个模型模式处理！！《每个Mesh的命名要在3D软件中命好！！》\n" +
                                      "批量模式：需要勾选批量FBX模式，处理多个FBX模型，共用同一张贴图和材质！！\n\n" +
                                      "使用前请确保已设置好输出目录。";

    public static void Open()
    {
        var win = GetWindow<AssetOrganizerWindow>("3D资产整理工具");
        win.minSize = new Vector2(560, 650);
    }

    private void OnEnable()
    {
        EnsureDefaultShader();

        modelsFolderPath = EditorPrefs.GetString("AO_Models", modelsFolderPath);
        texturesFolderPath = EditorPrefs.GetString("AO_Textures", texturesFolderPath);
        materialsFolderPath = EditorPrefs.GetString("AO_Materials", materialsFolderPath);
        prefabsFolderPath = EditorPrefs.GetString("AO_Prefabs", prefabsFolderPath);

        showAdvanced = EditorPrefs.GetBool("AO_ShowAdv", false);
        showFolders = EditorPrefs.GetBool("AO_ShowFolders", false);
        showInstructions = EditorPrefs.GetBool("AO_ShowInstructions", true);
    }

    private void OnDisable()
    {
        EditorPrefs.SetString("AO_Models", modelsFolderPath);
        EditorPrefs.SetString("AO_Textures", texturesFolderPath);
        EditorPrefs.SetString("AO_Materials", materialsFolderPath);
        EditorPrefs.SetString("AO_Prefabs", prefabsFolderPath);

        EditorPrefs.SetBool("AO_ShowAdv", showAdvanced);
        EditorPrefs.SetBool("AO_ShowFolders", showFolders);
        EditorPrefs.SetBool("AO_ShowInstructions", showInstructions);
    }

    private void OnGUI()
    {
        DrawHeader();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Space(8);

        DrawInstructionsSection();
        GUILayout.Space(8);

        DrawModeSection();
        GUILayout.Space(8);

        if (!enableBatchMode) DrawSingleModeUI();
        else DrawBatchModeUI();

        GUILayout.Space(10);
        DrawAdvancedSettings();

        GUILayout.Space(16);
        DrawExecuteButton();

        GUILayout.Space(20);
        EditorGUILayout.EndScrollView();
    }

    void DrawHeader()
    {
        Rect rect = GUILayoutUtility.GetRect(0, 44, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
        GUI.Label(rect, "   3D 资产整理工具", new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        });
    }

    void DrawInstructionsSection()
    {
        GUILayout.Space(10);

        GUILayout.BeginVertical(EditorStyles.helpBox);

        showInstructions = EditorGUILayout.Foldout(showInstructions, "工具说明", true);

        if (showInstructions)
        {
            GUILayout.Space(5);

            Rect rect = EditorGUILayout.GetControlRect(false, 120);

            GUIStyle topAlignStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 11,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(8, 8, 8, 8)
            };

            GUI.Label(rect, instructionsText, topAlignStyle);

            GUILayout.Space(10);
            GUILayout.Space(8);
        }

        GUILayout.EndVertical();

        GUILayout.Space(5);
    }

    void DrawModeSection()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("导入模式", EditorStyles.boldLabel);

        enableBatchMode = EditorGUILayout.ToggleLeft(
            "批量 FBX 模式（多个模型，共用材质）",
            enableBatchMode
        );

        GUILayout.EndVertical();
    }

    void DrawSingleModeUI()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("单模型输入", EditorStyles.boldLabel);

        singleFbx = (GameObject)EditorGUILayout.ObjectField("FBX 模型", singleFbx, typeof(GameObject), false);
        texture = (Texture2D)EditorGUILayout.ObjectField("贴图", texture, typeof(Texture2D), false);
        baseAssetName = EditorGUILayout.TextField("预制体名称", baseAssetName);

        GUILayout.EndVertical();
    }

    void DrawBatchModeUI()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("批量模型输入", EditorStyles.boldLabel);

        texture = (Texture2D)EditorGUILayout.ObjectField("共用贴图", texture, typeof(Texture2D), false);
        baseAssetName = EditorGUILayout.TextField("共用材质名称", baseAssetName);

        GUILayout.Space(6);
        GUILayout.Label("FBX 模型列表", EditorStyles.miniBoldLabel);

        int removeIndex = -1;

        for (int i = 0; i < batchModels.Count; i++)
        {
            GUILayout.BeginHorizontal();
            batchModels[i].model = (GameObject)EditorGUILayout.ObjectField(
                batchModels[i].model, typeof(GameObject), false
            );
            batchModels[i].customName = EditorGUILayout.TextField(
                batchModels[i].customName, GUILayout.Width(160)
            );

            if (GUILayout.Button("✕✕", GUILayout.Width(24)))
                removeIndex = i;

            GUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
            batchModels.RemoveAt(removeIndex);

        if (GUILayout.Button("+ 添加模型"))
            batchModels.Add(new BatchModelItem());

        GUILayout.EndVertical();
    }

    void DrawAdvancedSettings()
    {
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "高级设置", true);
        if (!showAdvanced) return;

        EditorGUI.indentLevel++;
        EnsureDefaultShader();
        selectedShader = (Shader)EditorGUILayout.ObjectField("材质 Shader", selectedShader, typeof(Shader), false);

        showFolders = EditorGUILayout.Foldout(showFolders, "输出目录设置", true);
        if (showFolders)
        {
            EditorGUI.indentLevel++;
            DrawFolderField("模型目录", ref modelsFolderPath);
            DrawFolderField("贴图目录", ref texturesFolderPath);
            DrawFolderField("材质目录", ref materialsFolderPath);
            DrawFolderField("预制体目录", ref prefabsFolderPath);
            EditorGUI.indentLevel--;
        }

        EditorGUI.indentLevel--;
    }

    void DrawExecuteButton()
    {
        GUI.enabled = enableBatchMode
            ? batchModels.Count > 0
            : singleFbx != null;

        if (GUILayout.Button("生成资源", GUILayout.Height(36)))
        {
            if (enableBatchMode) ProcessBatch();
            else ProcessSingle();
        }

        GUI.enabled = true;
    }

    

    void ProcessSingle()
    {
        CreateFolders();

        
        GameObject movedModel = MoveModelToFolder(singleFbx);
        Texture2D movedTex = MoveTextureToFolder(texture);

        
        movedModel = RenameAssetFileSafe(movedModel, baseAssetName);
        movedTex = RenameAssetFileSafe(movedTex, baseAssetName);

        
        if (!movedModel)
        {
            Debug.LogError("模型资源为空：请检查FBX是否为Project内资产，且未在重命名过程中发生异常。");
            return;
        }

        
        Material mat = CreateMaterial(baseAssetName, movedTex);

        int meshCount = movedModel.GetComponentsInChildren<MeshRenderer>(true).Length;

        if (meshCount > 1)
            CreatePrefabsFromChildMeshes(movedModel, mat, baseAssetName);
        else
            CreatePrefab(movedModel, mat, baseAssetName);
    }

    void ProcessBatch()
    {
        CreateFolders();

        
        Texture2D movedTex = MoveTextureToFolder(texture);

        
        movedTex = RenameAssetFileSafe(movedTex, baseAssetName);

        
        Material mat = CreateMaterial(baseAssetName, movedTex);

        foreach (var item in batchModels)
        {
            if (!item.model) continue;

            GameObject movedModel = MoveModelToFolder(item.model);

            string finalName = string.IsNullOrEmpty(item.customName)
                ? movedModel.name
                : item.customName;

            
            movedModel = RenameAssetFileSafe(movedModel, finalName);

            if (!movedModel)
            {
                Debug.LogWarning($"批量模式：某个模型重命名/加载失败，已跳过。目标名：{finalName}");
                continue;
            }

            
            CreatePrefab(movedModel, mat, finalName);
        }
    }

    

    GameObject MoveModelToFolder(GameObject model)
    {
        if (!model) return null;

        string srcPath = AssetDatabase.GetAssetPath(model);
        string dstPath = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(modelsFolderPath, Path.GetFileName(srcPath))
        );

        if (srcPath != dstPath)
        {
            AssetDatabase.MoveAsset(srcPath, dstPath);
            AssetDatabase.SaveAssets();
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(dstPath);
    }

    Texture2D MoveTextureToFolder(Texture2D tex)
    {
        if (!tex) return null;

        string srcPath = AssetDatabase.GetAssetPath(tex);
        string dstPath = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(texturesFolderPath, Path.GetFileName(srcPath))
        );

        if (srcPath != dstPath)
        {
            AssetDatabase.MoveAsset(srcPath, dstPath);
            AssetDatabase.SaveAssets();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(dstPath);
    }

    Material CreateMaterial(string name, Texture2D tex)
    {
        EnsureDefaultShader();

        Shader shader = selectedShader
            ?? Shader.Find(DefaultShaderName)
            ?? Shader.Find("Standard");

        Material mat = new Material(shader);

        if (tex)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }

        string path = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(materialsFolderPath, name + ".mat")
        );

        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        return mat;
    }

    void EnsureDefaultShader()
    {
        if (selectedShader) return;

        selectedShader = Shader.Find(DefaultShaderName);
    }

    void CreatePrefab(GameObject model, Material mat, string name)
    {
        GameObject go = Instantiate(model);
        go.name = name;

        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = mat;

        string path = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(prefabsFolderPath, name + ".prefab")
        );

        SavePrefabCompatible(go, path);
        DestroyImmediate(go);

        Debug.Log($"创建预制体: {name}");
    }

    void CreatePrefabsFromChildMeshes(GameObject fbx, Material mat, string baseName)
    {
        GameObject root = Instantiate(fbx);

        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);

        HashSet<Mesh> createdMeshes = new HashSet<Mesh>();

        foreach (var r in renderers)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                continue;

            Mesh mesh = mf.sharedMesh;

            if (createdMeshes.Contains(mesh))
                continue;

            createdMeshes.Add(mesh);

            GameObject go = new GameObject(r.gameObject.name);

            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            MeshFilter newMF = go.AddComponent<MeshFilter>();
            MeshRenderer newMR = go.AddComponent<MeshRenderer>();

            newMF.sharedMesh = mesh;
            newMR.sharedMaterial = mat;

            string prefabName = baseName + "_" + mesh.name;

            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(prefabsFolderPath, prefabName + ".prefab")
            );

            SavePrefabCompatible(go, path);
            DestroyImmediate(go);

            Debug.Log($"创建子Mesh预制体: {prefabName}");
        }

        DestroyImmediate(root);

        Debug.Log($"为模型 {baseName} 创建了 {createdMeshes.Count} 个子Mesh预制体");
    }

    

    T RenameAssetFileSafe<T>(T asset, string newBaseName) where T : Object
    {
        if (!asset) return null;

        string oldPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(oldPath)) return asset;

        string folder = Path.GetDirectoryName(oldPath)?.Replace("\\", "/");
        string ext = Path.GetExtension(oldPath);
        string oldBase = Path.GetFileNameWithoutExtension(oldPath);

        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(ext))
            return asset;

        newBaseName = SanitizeFileName(newBaseName);
        if (string.IsNullOrEmpty(newBaseName))
            return asset;

        if (oldBase == newBaseName)
            return asset;

        
        string desiredPath = Path.Combine(folder, newBaseName + ext).Replace("\\", "/");
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(desiredPath);
        string uniqueBase = Path.GetFileNameWithoutExtension(uniquePath);

        string guid = AssetDatabase.AssetPathToGUID(oldPath);

        string err = AssetDatabase.RenameAsset(oldPath, uniqueBase);
        if (!string.IsNullOrEmpty(err))
        {
            Debug.LogWarning($"重命名失败: {oldPath} -> {uniqueBase}，原因: {err}");
            return asset; 
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string newPath = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(newPath))
            return asset;

        
        T loaded = AssetDatabase.LoadAssetAtPath<T>(newPath);
        if (loaded) return loaded;

        
        Object main = AssetDatabase.LoadMainAssetAtPath(newPath);
        T typedMain = main as T;
        if (typedMain) return typedMain;

        
        return asset;
    }

    static void SavePrefabCompatible(GameObject instance, string path)
    {
#if UNITY_2018_3_OR_NEWER
        PrefabUtility.SaveAsPrefabAsset(instance, path);
#else
        PrefabUtility.CreatePrefab(path, instance, ReplacePrefabOptions.ConnectToPrefab);
#endif
    }

    string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "_");

        return name.Trim();
    }

    

    void CreateFolders()
    {
        CreateFolderIfMissing(modelsFolderPath);
        CreateFolderIfMissing(texturesFolderPath);
        CreateFolderIfMissing(materialsFolderPath);
        CreateFolderIfMissing(prefabsFolderPath);
    }

    void CreateFolderIfMissing(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path);
        if (!AssetDatabase.IsValidFolder(parent))
            CreateFolderIfMissing(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    void DrawFolderField(string label, ref string path)
    {
        GUILayout.BeginHorizontal();
        path = EditorGUILayout.TextField(label, path);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string p = EditorUtility.OpenFolderPanel("选择目录", "Assets", "");
            if (!string.IsNullOrEmpty(p) && p.StartsWith(Application.dataPath))
                path = "Assets" + p.Substring(Application.dataPath.Length);
        }
        GUILayout.EndHorizontal();
    }
}
}
#endif


