#if UNITY_EDITOR

using UnityEngine;
using ArtTools.Framework;
using ArtTools.EditorTools;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace ArtTools.ImageTools
{

public class TextureCheckerTool : EditorWindow
{
    private class TextureInfo
    {
        public Texture2D texture;
        public string path;
        public int width;
        public int height;
        public long fileSize;
    }

    private List<TextureInfo> largeTextures = new List<TextureInfo>();
    private Vector2 scrollPos;
    private int sizeThreshold = 1024;
    private bool scanning = false;
    private bool presentationMode = false;


    private string scanFolder = "Assets";

    public static void ShowWindow()
    {
        TextureCheckerTool window = GetWindow<TextureCheckerTool>("贴图检查工具");
        window.minSize = new Vector2(520, 460);
        window.Show();
    }

#if !ARTTOOLS_DISABLE_INTERNAL_TOOLS
#endif
    public static void OpenPresentationDemo()
    {
        TextureCheckerTool window = CreateInstance<TextureCheckerTool>();
        window.titleContent = new GUIContent("Video001 · 贴图检查结果");
        window.presentationMode = true;
        window.sizeThreshold = 4096;
        window.scanFolder = "Assets/ArtTools/Demo/Video001_TextureChecker/Textures";
        window.position = new Rect(360, 110, 1200, 820);
        window.ShowUtility();
        window.ScanTextures();
        window.Focus();
    }

    private void OnGUI()
    {
        DrawToolGUI();
    }

    public void DrawToolGUI()
    {
        ArtToolsEditorUI.DrawHeader("贴图检查工具", "扫描超过尺寸阈值的贴图资源", "d_Texture Icon");

        ArtToolsEditorUI.BeginPanel("扫描设置");
        sizeThreshold = EditorGUILayout.IntField("尺寸阈值（像素）", sizeThreshold);
        DrawScanFolderSelector();
        presentationMode = EditorGUILayout.ToggleLeft("Presentation Mode（录制 / 截图）", presentationMode);

        GUILayout.Space(8);
        GUI.enabled = !scanning;
        if (ArtToolsEditorUI.PrimaryButton(scanning ? "正在扫描..." : "扫描贴图"))
        {
            ScanTextures();
        }
        GUI.enabled = true;
        ArtToolsEditorUI.EndPanel();

        ArtToolsEditorUI.BeginPanel("扫描结果");
        if (presentationMode)
        {
            DrawPresentationSummary();
        }
        else
        {
            ArtToolsEditorUI.Summary($"共找到 {largeTextures.Count} 张尺寸达到 {sizeThreshold} x {sizeThreshold} 的贴图");
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (largeTextures.Count == 0)
            ArtToolsEditorUI.EmptyState("暂无结果。点击“扫描贴图”开始检查。");

        foreach (var tex in largeTextures)
        {
            float rowHeight = presentationMode ? 86 : 74;
            EditorGUILayout.BeginHorizontal(ArtToolsEditorUI.RowStyle, GUILayout.MinHeight(rowHeight));

            Texture preview = AssetPreview.GetAssetPreview(tex.texture) ?? tex.texture;
            UnityEngine.Rect previewRect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));


            GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
            EditorGUIUtility.AddCursorRect(previewRect, MouseCursor.Link);


            if (Event.current.type == EventType.MouseDown && previewRect.Contains(Event.current.mousePosition))
            {
                EditorGUIUtility.PingObject(tex.texture);
                Selection.activeObject = tex.texture;
                Event.current.Use();
            }

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical();
            GUILayout.Label(tex.texture.name, EditorStyles.boldLabel);
            if (presentationMode)
            {
                GUIStyle sizeStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    normal = { textColor = new Color(0.275f, 0.78f, 1f) }
                };
                GUILayout.Label($"{tex.width} × {tex.height}", sizeStyle);
                GUILayout.Label($"{FormatBytes(tex.fileSize)}  ·  {ShortPath(tex.path)}", EditorStyles.miniLabel);
            }
            else
            {
                GUILayout.Label($"尺寸：{tex.width} x {tex.height}    大小：{FormatBytes(tex.fileSize)}", EditorStyles.miniLabel);
                GUILayout.Label($"路径：{tex.path}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("选中", GUILayout.Width(60), GUILayout.Height(30)))
            {
                EditorGUIUtility.PingObject(tex.texture);
                Selection.activeObject = tex.texture;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        ArtToolsEditorUI.EndPanel();
    }


    private void DrawScanFolderSelector()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("扫描目录");
        EditorGUILayout.SelectableLabel(scanFolder, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        if (GUILayout.Button("选择目录", GUILayout.Width(80)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择扫描文件夹", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    scanFolder = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", "请选择项目内的文件夹（Assets 下）", "确定");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void ScanTextures()
    {
        scanning = true;
        largeTextures.Clear();


        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { scanFolder });
        int count = guids.Length;
        int progress = 0;

        try
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;

                int width = texture.width;
                int height = texture.height;

                if (width >= sizeThreshold || height >= sizeThreshold)
                {
                    string fullPath = Path.Combine(Application.dataPath, path.Substring(7));
                    long fileSize = new FileInfo(fullPath).Length;

                    largeTextures.Add(new TextureInfo
                    {
                        texture = texture,
                        path = path,
                        width = width,
                        height = height,
                        fileSize = fileSize
                    });
                }

                progress++;
                if (progress % 50 == 0)
                {
                    EditorUtility.DisplayProgressBar("正在扫描贴图...", $"已检查 {progress}/{count}", (float)progress / count);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            scanning = false;
            Repaint();
        }

        Debug.Log($"扫描完成，共发现 {largeTextures.Count} 张超过 {sizeThreshold}px 的贴图。\n扫描路径：{scanFolder}");
    }

    private void DrawPresentationSummary()
    {
        Rect rect = GUILayoutUtility.GetRect(0, 68, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.10f, 0.13f, 0.16f));
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 3, rect.width, 3), new Color(0.275f, 0.78f, 1f));

        GUIStyle numberStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 28,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };
        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.72f, 0.76f, 0.80f) }
        };

        GUI.Label(new Rect(rect.x + 16, rect.y + 5, rect.width - 32, 36), $"发现 {largeTextures.Count} 个异常贴图", numberStyle);
        GUI.Label(new Rect(rect.x + 17, rect.y + 40, rect.width - 34, 20), $"扫描阈值  ≥ {sizeThreshold} px", labelStyle);
    }

    private static string ShortPath(string path)
    {
        const int maxLength = 54;
        if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
            return path;

        string fileName = Path.GetFileName(path);
        string parent = Path.GetFileName(Path.GetDirectoryName(path));
        return $"…/{parent}/{fileName}";
    }

    private string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024f):F1} KB";
        return $"{(bytes / (1024f * 1024f)):F2} MB";
    }
}
}
#endif


