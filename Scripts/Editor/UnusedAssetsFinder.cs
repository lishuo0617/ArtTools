#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using ArtTools.Framework;
using ArtTools.EditorTools;
using System.Collections.Generic;
using System.IO;

namespace ArtTools.ImageTools
{

public class UnusedTextureFinder : EditorWindow
{
    private List<string> unusedTextures = new List<string>();
    private string scanFolder = "Assets";
    private Vector2 scrollPos;
    private int hoveredIndex = -1;

    private readonly string[] textureExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff" };

    public static void ShowWindow()
    {
        UnusedTextureFinder window = GetWindow<UnusedTextureFinder>("查找未被引用的贴图");
        window.minSize = new Vector2(520, 460);
        window.Show();
    }

    void OnGUI()
    {
        DrawToolGUI();
    }

    public void DrawToolGUI()
    {
        ArtToolsEditorUI.DrawHeader("查找未被引用的贴图", "扫描未被材质属性引用的贴图资源", "d_Search Icon");

        ArtToolsEditorUI.BeginPanel("扫描设置");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("扫描文件夹");
        scanFolder = EditorGUILayout.TextField(scanFolder);
        if (GUILayout.Button("选择文件夹", GUILayout.Width(100)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择要扫描的文件夹", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (selectedPath.StartsWith(Application.dataPath))
                    scanFolder = "Assets" + selectedPath.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        if (ArtToolsEditorUI.PrimaryButton("开始扫描"))
        {
            ScanUnusedTextures();
        }
        ArtToolsEditorUI.EndPanel();

        ArtToolsEditorUI.BeginPanel("扫描结果");
        ArtToolsEditorUI.Summary($"找到 {unusedTextures.Count} 张未被材质引用的贴图");

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (Event.current.type == EventType.MouseMove)
            Repaint();

        if (unusedTextures.Count == 0)
            ArtToolsEditorUI.EmptyState("暂无结果。点击“开始扫描”检查指定目录。");

        for (int i = 0; i < unusedTextures.Count; i++)
        {
            string asset = unusedTextures[i];
            UnityEngine.Rect rowRect = EditorGUILayout.GetControlRect(false, 26, ArtToolsEditorUI.RowStyle, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
                ArtToolsEditorUI.RowStyle.Draw(rowRect, GUIContent.none, false, false, false, false);

            if (rowRect.Contains(Event.current.mousePosition))
            {
                hoveredIndex = i;

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(asset);
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                    Event.current.Use();
                }
            }

            if (hoveredIndex == i)
                EditorGUI.DrawRect(rowRect, new Color(0.75f, 0.75f, 0.75f, 0.25f));


            UnityEngine.Rect iconRect = new UnityEngine.Rect(rowRect.x + 4, rowRect.y + 2, 18, 18);
            Texture2D icon = AssetPreview.GetMiniThumbnail(AssetDatabase.LoadAssetAtPath<Texture>(asset));
            if (icon != null)
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

            UnityEngine.Rect labelRect = new UnityEngine.Rect(rowRect.x + 28, rowRect.y + 3, rowRect.width - 28, rowRect.height - 6);
            EditorGUI.LabelField(labelRect, asset);
        }

        EditorGUILayout.EndScrollView();
        ArtToolsEditorUI.EndPanel();
    }

    void ScanUnusedTextures()
    {
        unusedTextures.Clear();


        string[] allMaterialPaths = AssetDatabase.FindAssets("t:Material");
        HashSet<string> usedTexturePaths = new HashSet<string>();

        foreach (string guid in allMaterialPaths)
        {
            string matPath = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) continue;


            Shader shader = mat.shader;
            int count = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    string propName = ShaderUtil.GetPropertyName(shader, i);
                    Texture tex = mat.GetTexture(propName);
                    if (tex != null)
                    {
                        string texPath = AssetDatabase.GetAssetPath(tex);
                        if (!string.IsNullOrEmpty(texPath))
                            usedTexturePaths.Add(texPath);
                    }
                }
            }
        }


        string[] allAssets = AssetDatabase.FindAssets("t:Texture", new[] { scanFolder });
        foreach (string guid in allAssets)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!HasTextureExtension(path)) continue;

            if (!usedTexturePaths.Contains(path))
                unusedTextures.Add(path);
        }

        Debug.Log($"扫描完成：{unusedTextures.Count} 张未被材质引用的贴图。");
    }

    bool HasTextureExtension(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        foreach (string targetExt in textureExtensions)
        {
            if (ext == targetExt)
                return true;
        }
        return false;
    }
}
}
#endif


