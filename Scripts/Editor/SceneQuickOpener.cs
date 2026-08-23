using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

namespace ArtTools.EditorTools
{

public class SceneQuickOpener : EditorWindow
{
    [System.Serializable]
    private class SceneData
    {
        public List<string> sceneGuids = new List<string>();
        public List<bool> sceneDoneFlags = new List<bool>();
    }

    private const string SETTINGS_KEY = "SceneQuickOpener_Data";
    private SceneData sceneData;
    private Vector2 scrollPos;
    private List<SceneAsset> sceneAssets = new List<SceneAsset>();


    private int draggingIndex = -1;
    private int targetIndex = -1;
    private bool isDragging = false;
    private Rect[] itemRects;

    public static void ShowWindow()
    {
        var window = GetWindow<SceneQuickOpener>("快速打开场景");
        window.minSize = new Vector2(520, 420);
        window.Show();
    }

    private void OnEnable()
    {
        LoadData();
    }

    private void LoadData()
    {
        string json = EditorPrefs.GetString(SETTINGS_KEY, "{}");
        sceneData = JsonUtility.FromJson<SceneData>(json);

        if (sceneData.sceneGuids == null) sceneData.sceneGuids = new List<string>();
        if (sceneData.sceneDoneFlags == null) sceneData.sceneDoneFlags = new List<bool>();

        sceneAssets.Clear();

        for (int i = 0; i < sceneData.sceneGuids.Count; i++)
        {
            string guid = sceneData.sceneGuids[i];
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (!string.IsNullOrEmpty(path))
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                sceneAssets.Add(scene);
            }
            else
                sceneAssets.Add(null);

            
            if (i >= sceneData.sceneDoneFlags.Count)
                sceneData.sceneDoneFlags.Add(false);
        }

        SaveData();
    }

    private void SaveData()
    {
        if (sceneData == null)
            sceneData = new SceneData();

        sceneData.sceneGuids.Clear();

        for (int i = 0; i < sceneAssets.Count; i++)
        {
            SceneAsset scene = sceneAssets[i];

            if (scene != null)
            {
                string path = AssetDatabase.GetAssetPath(scene);
                string guid = AssetDatabase.AssetPathToGUID(path);
                sceneData.sceneGuids.Add(guid);
            }
            else
                sceneData.sceneGuids.Add("");
        }

        
        while (sceneData.sceneDoneFlags.Count < sceneAssets.Count)
            sceneData.sceneDoneFlags.Add(false);

        while (sceneData.sceneDoneFlags.Count > sceneAssets.Count)
            sceneData.sceneDoneFlags.RemoveAt(sceneData.sceneDoneFlags.Count - 1);

        string json = JsonUtility.ToJson(sceneData);
        EditorPrefs.SetString(SETTINGS_KEY, json);
    }

    private void OnGUI()
    {
        ArtToolsEditorUI.DrawHeader("快速打开场景", "管理常用场景并一键打开", "d_SceneAsset Icon");

        if (itemRects == null || itemRects.Length != sceneAssets.Count)
            itemRects = new Rect[sceneAssets.Count];

        ArtToolsEditorUI.BeginPanel("场景列表");
        ArtToolsEditorUI.Summary($"已保存场景：{sceneAssets.Count} 个");
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        bool hasChanges = false;

        if (sceneAssets.Count == 0)
            ArtToolsEditorUI.EmptyState("暂无场景。点击下方“添加场景槽位”开始配置。");

        for (int i = 0; i < sceneAssets.Count; i++)
        {
            Rect itemRect = EditorGUILayout.BeginVertical(ArtToolsEditorUI.RowStyle);
            itemRects[i] = itemRect;

            HandleDragEvents(i, itemRect);

            
            if (sceneData.sceneDoneFlags[i])
                EditorGUI.DrawRect(itemRect, new Color(0.12f, 0.42f, 0.18f, 0.35f));

            EditorGUILayout.BeginHorizontal();
            {
                Rect toggleRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));

                EditorGUI.BeginChangeCheck();
                bool newFlag = GUI.Toggle(toggleRect, sceneData.sceneDoneFlags[i], GUIContent.none);
                if (EditorGUI.EndChangeCheck())
                {
                    sceneData.sceneDoneFlags[i] = newFlag;
                    SaveData();
                    Repaint();
                }


                EditorGUILayout.LabelField("≡", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(16));
                EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(25));

                SceneAsset oldAsset = sceneAssets[i];
                sceneAssets[i] = (SceneAsset)EditorGUILayout.ObjectField(sceneAssets[i], typeof(SceneAsset), false);
                if (oldAsset != sceneAssets[i])
                    SaveData();

                GUI.enabled = sceneAssets[i] != null;
                if (GUILayout.Button("打开", EditorStyles.miniButtonLeft, GUILayout.Width(54)))
                    OpenScene(sceneAssets[i]);
                GUI.enabled = true;

                if (GUILayout.Button("删除", EditorStyles.miniButtonRight, GUILayout.Width(54)))
                {
                    sceneAssets.RemoveAt(i);
                    sceneData.sceneDoneFlags.RemoveAt(i);
                    SaveData();
                    break;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (sceneAssets[i] != null)
            {
                string path = AssetDatabase.GetAssetPath(sceneAssets[i]);
                EditorGUILayout.LabelField($"路径：{path}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }


        if (isDragging && targetIndex >= 0)
            DrawDropIndicator(targetIndex);

        EditorGUILayout.EndScrollView();
        ArtToolsEditorUI.EndPanel();

        ArtToolsEditorUI.BeginPanel("操作");
        EditorGUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("添加场景槽位", ArtToolsEditorUI.PrimaryButtonStyle))
            {
                sceneAssets.Add(null);
                sceneData.sceneDoneFlags.Add(false);
                hasChanges = true;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("清空所有", GUILayout.Width(88), GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("确认清空", "确定要清空所有场景吗？", "是", "否"))
                {
                    sceneAssets.Clear();
                    sceneData.sceneDoneFlags.Clear();
                    hasChanges = true;
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        ArtToolsEditorUI.EndPanel();

        if (hasChanges)
            SaveData();
    }

    private void HandleDragEvents(int index, Rect rect)
    {
        Event evt = Event.current;

        if (rect.Contains(evt.mousePosition))
        {
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                Rect dragHandle = new Rect(rect.x, rect.y, 35, rect.height);
                if (dragHandle.Contains(evt.mousePosition))
                {
                    draggingIndex = index;
                    isDragging = true;
                    evt.Use();
                }
            }

            if (isDragging && evt.type == EventType.MouseDrag)
            {
                for (int i = 0; i < itemRects.Length; i++)
                {
                    if (itemRects[i].Contains(evt.mousePosition))
                    {
                        float center = itemRects[i].y + itemRects[i].height / 2;
                        targetIndex = evt.mousePosition.y < center ? i : i + 1;
                        targetIndex = Mathf.Clamp(targetIndex, 0, sceneAssets.Count);
                        Repaint();
                        break;
                    }
                }
            }
        }

        if (isDragging && evt.type == EventType.MouseUp)
        {
            if (draggingIndex >= 0 && targetIndex >= 0 && draggingIndex != targetIndex)
            {
                MoveItem(draggingIndex, targetIndex);
                SaveData();
            }

            isDragging = false;
            draggingIndex = -1;
            targetIndex = -1;
        }
    }

    private void MoveItem(int from, int to)
    {
        var asset = sceneAssets[from];
        bool flag = sceneData.sceneDoneFlags[from];

        sceneAssets.RemoveAt(from);
        sceneData.sceneDoneFlags.RemoveAt(from);

        if (to > from) to--;
        sceneAssets.Insert(to, asset);
        sceneData.sceneDoneFlags.Insert(to, flag);
    }

    private void DrawDropIndicator(int index)
    {
        float y;
        if (index >= itemRects.Length)
            y = itemRects[itemRects.Length - 1].yMax + 2;
        else
            y = itemRects[index].y - 2;

        EditorGUI.DrawRect(new Rect(0, y, position.width, 2), Color.blue);
    }

    private void OpenScene(SceneAsset sceneAsset)
    {
        if (sceneAsset == null)
        {
            EditorUtility.DisplayDialog("错误", "无效的场景文件！", "确定");
            return;
        }

        string path = AssetDatabase.GetAssetPath(sceneAsset);
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(path);
    }

    private void OnDestroy()
    {
        SaveData();
    }
}
}



