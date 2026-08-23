#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArtTools.ImageTools
{
    public class TextureResizeTool : EditorWindow
    {
        private enum SaveMode
        {
            ReplaceOriginal,
            SaveAsCopy
        }

        [Serializable]
        private class TextureItem
        {
            public Texture2D texture;
        }

        private readonly List<TextureItem> textureItems = new List<TextureItem>();
        private Vector2 scrollPos;
        private int targetWidth = 512;
        private int targetHeight = 512;
        private SaveMode saveMode = SaveMode.ReplaceOriginal;
        private readonly string[] saveModeLabels = { "替换原图（默认）", "另存为副本" };
        private DefaultAsset outputFolder;
        private string outputFolderPath = "Assets";

        public static void ShowWindow()
        {
            TextureResizeTool window = GetWindow<TextureResizeTool>("批量修改图片尺寸");
            window.minSize = new Vector2(560, 560);
            window.Show();
        }

        public static void Open()
        {
            ShowWindow();
        }

        private void OnGUI()
        {
            DrawToolGUI();
        }

        public void DrawToolGUI()
        {
            DrawHeader();
            GUILayout.Space(8);

            DrawTextureList();
            GUILayout.Space(8);

            DrawSizeSettings();
            GUILayout.Space(8);

            DrawSaveSettings();
            GUILayout.Space(12);

            DrawActionButtons();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("批量修改图片尺寸", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "把图片拖进列表，或点击“添加当前选中图片”。默认会直接覆盖原图；如需保留原图，请切换为“另存为副本”。",
                MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void DrawTextureList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("图片列表", EditorStyles.boldLabel);

            Rect dropRect = GUILayoutUtility.GetRect(0, 54, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "拖拽 Texture2D 到这里");
            HandleDragAndDrop(dropRect);

            GUILayout.Space(6);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(230));

            if (textureItems.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有待处理图片。", MessageType.None);
            }
            else
            {
                for (int i = 0; i < textureItems.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    textureItems[i].texture = (Texture2D)EditorGUILayout.ObjectField(
                        textureItems[i].texture,
                        typeof(Texture2D),
                        false);

                    if (GUILayout.Button("定位", GUILayout.Width(52)))
                    {
                        PingTexture(textureItems[i].texture);
                    }

                    if (GUILayout.Button("删除", GUILayout.Width(52)))
                    {
                        textureItems.RemoveAt(i);
                        GUIUtility.ExitGUI();
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加当前选中图片", GUILayout.Height(26)))
                AddSelectedTextures();

            if (GUILayout.Button("添加空项", GUILayout.Height(26), GUILayout.Width(90)))
                textureItems.Add(new TextureItem());

            if (GUILayout.Button("清空列表", GUILayout.Height(26), GUILayout.Width(90)))
                textureItems.Clear();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawSizeSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("目标尺寸", EditorStyles.boldLabel);

            targetWidth = Mathf.Max(1, EditorGUILayout.IntField("宽度", targetWidth));
            targetHeight = Mathf.Max(1, EditorGUILayout.IntField("高度", targetHeight));

            EditorGUILayout.EndVertical();
        }

        private void DrawSaveSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("保存方式", EditorStyles.boldLabel);

            saveMode = (SaveMode)EditorGUILayout.Popup("处理结果", (int)saveMode, saveModeLabels);
            EditorGUILayout.HelpBox(
                saveMode == SaveMode.ReplaceOriginal
                    ? "当前模式会直接替换原图文件。处理前请确认资源已提交或已备份。"
                    : "当前模式会保留原图，并在指定目录生成带尺寸后缀的新图片。",
                saveMode == SaveMode.ReplaceOriginal ? MessageType.Warning : MessageType.Info);

            if (saveMode == SaveMode.SaveAsCopy)
                DrawOutputFolderSelector();

            EditorGUILayout.EndVertical();
        }

        private void DrawOutputFolderSelector()
        {
            GUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("输出目录", outputFolder, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck() && outputFolder != null)
            {
                string path = AssetDatabase.GetAssetPath(outputFolder);
                if (AssetDatabase.IsValidFolder(path))
                    outputFolderPath = path;
                else
                    outputFolder = null;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.SelectableLabel(outputFolderPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button("选择目录", GUILayout.Width(90)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择输出目录", Application.dataPath, "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace("\\", "/");
                    selectedPath = selectedPath.Replace("\\", "/");
                    if (selectedPath.StartsWith(projectPath, StringComparison.Ordinal))
                    {
                        outputFolderPath = "Assets" + selectedPath.Substring(projectPath.Length);
                        outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(outputFolderPath);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("目录无效", "请选择当前 Unity 项目 Assets 目录下的文件夹。", "确定");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionButtons()
        {
            using (new EditorGUI.DisabledScope(textureItems.Count == 0))
            {
                Color oldColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.35f, 0.75f, 0.35f);
                if (GUILayout.Button("开始批量修改尺寸", GUILayout.Height(36)))
                    ResizeAllTextures();
                GUI.backgroundColor = oldColor;
            }
        }

        private void HandleDragAndDrop(Rect dropRect)
        {
            Event evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition))
                return;

            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;

            bool hasTexture = false;
            foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
            {
                if (obj is Texture2D)
                {
                    hasTexture = true;
                    break;
                }
            }

            if (!hasTexture)
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                {
                    Texture2D texture = obj as Texture2D;
                    if (texture != null)
                        AddTextureUnique(texture);
                }
            }

            evt.Use();
        }

        private void AddSelectedTextures()
        {
            foreach (UnityEngine.Object obj in Selection.objects)
            {
                Texture2D texture = obj as Texture2D;
                if (texture != null)
                    AddTextureUnique(texture);
            }
        }

        private void AddTextureUnique(Texture2D texture)
        {
            if (texture == null)
                return;

            foreach (TextureItem item in textureItems)
            {
                if (item.texture == texture)
                    return;
            }

            textureItems.Add(new TextureItem { texture = texture });
        }

        private static void PingTexture(Texture2D texture)
        {
            if (texture == null)
                return;

            EditorGUIUtility.PingObject(texture);
            Selection.activeObject = texture;
        }

        private void ResizeAllTextures()
        {
            List<Texture2D> validTextures = GetValidTextures();
            if (validTextures.Count == 0)
            {
                EditorUtility.DisplayDialog("没有可处理图片", "列表中没有有效的 Texture2D。", "确定");
                return;
            }

            if (saveMode == SaveMode.SaveAsCopy && !AssetDatabase.IsValidFolder(outputFolderPath))
            {
                EditorUtility.DisplayDialog("输出目录无效", "请先选择一个有效的项目内输出目录。", "确定");
                return;
            }

            if (saveMode == SaveMode.ReplaceOriginal)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "确认替换原图",
                    $"即将直接覆盖 {validTextures.Count} 张原图，目标尺寸为 {targetWidth} x {targetHeight}。\n\n建议确认资源已提交或已备份。",
                    "替换原图",
                    "取消");

                if (!confirmed)
                    return;
            }

            int successCount = 0;
            try
            {
                for (int i = 0; i < validTextures.Count; i++)
                {
                    Texture2D texture = validTextures[i];
                    EditorUtility.DisplayProgressBar(
                        "批量修改图片尺寸",
                        $"正在处理 {texture.name} ({i + 1}/{validTextures.Count})",
                        (float)(i + 1) / validTextures.Count);

                    if (ResizeSingleTexture(texture))
                        successCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog("处理完成", $"成功处理 {successCount} / {validTextures.Count} 张图片。", "确定");
        }

        private List<Texture2D> GetValidTextures()
        {
            List<Texture2D> textures = new List<Texture2D>();
            foreach (TextureItem item in textureItems)
            {
                if (item != null && item.texture != null && !textures.Contains(item.texture))
                    textures.Add(item.texture);
            }

            return textures;
        }

        private bool ResizeSingleTexture(Texture2D sourceTexture)
        {
            string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogWarning($"跳过 {sourceTexture.name}：找不到资源路径。");
                return false;
            }

            TextureImporter importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"跳过 {sourceTexture.name}：不是可处理的图片资源。");
                return false;
            }

            bool oldReadable = importer.isReadable;
            TextureImporterCompression oldCompression = importer.textureCompression;

            try
            {
                MakeTextureReadable(importer);

                Texture2D resizedTexture = ResizeTexture(sourceTexture, targetWidth, targetHeight);
                try
                {
                    string writeAssetPath = GetWriteAssetPath(sourcePath);
                    byte[] bytes = EncodeTexture(resizedTexture, writeAssetPath);
                    if (bytes == null || bytes.Length == 0)
                    {
                        Debug.LogError($"处理失败 {sourceTexture.name}：图片编码失败。");
                        return false;
                    }

                    string fullWritePath = AssetPathToFullPath(writeAssetPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullWritePath) ?? Application.dataPath);
                    File.WriteAllBytes(fullWritePath, bytes);
                    AssetDatabase.ImportAsset(writeAssetPath, ImportAssetOptions.ForceUpdate);
                }
                finally
                {
                    DestroyImmediate(resizedTexture);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"处理失败 {sourceTexture.name}：\n{e}");
                return false;
            }
            finally
            {
                RestoreImporter(sourcePath, oldReadable, oldCompression);
            }
        }

        private static void MakeTextureReadable(TextureImporter importer)
        {
            bool changed = false;
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        private string GetWriteAssetPath(string sourcePath)
        {
            string sourceExtension = Path.GetExtension(sourcePath).ToLowerInvariant();
            bool canEncodeOriginalFormat = IsSupportedEncodeExtension(sourceExtension);

            if (saveMode == SaveMode.ReplaceOriginal)
            {
                if (!canEncodeOriginalFormat)
                    throw new NotSupportedException($"当前替换模式不支持直接覆盖 {sourceExtension} 格式，请改用“另存为副本”。");

                return sourcePath;
            }

            string writeExtension = canEncodeOriginalFormat ? sourceExtension : ".png";

            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            fileName = $"{fileName}_{targetWidth}x{targetHeight}";

            string folder = outputFolderPath;

            string targetPath = Path.Combine(folder, fileName + writeExtension).Replace("\\", "/");
            return AssetDatabase.GenerateUniqueAssetPath(targetPath);
        }

        private static bool IsSupportedEncodeExtension(string extension)
        {
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tga";
        }

        private static byte[] EncodeTexture(Texture2D texture, string writeAssetPath)
        {
            string extension = Path.GetExtension(writeAssetPath).ToLowerInvariant();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return ImageConversion.EncodeToJPG(texture, 100);
                case ".tga":
#if UNITY_2018_3_OR_NEWER
                    return ImageConversion.EncodeToTGA(texture);
#else
                    throw new NotSupportedException("Unity 2017 不支持直接编码 TGA，请改用 PNG 或 JPG。");
#endif
                default:
                    return ImageConversion.EncodeToPNG(texture);
            }
        }

        private static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;

            try
            {
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;

                Texture2D resizedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                resizedTexture.Apply();
                return resizedTexture;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static void RestoreImporter(string assetPath, bool readable, TextureImporterCompression compression)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            bool changed = false;
            if (importer.isReadable != readable)
            {
                importer.isReadable = readable;
                changed = true;
            }

            if (importer.textureCompression != compression)
            {
                importer.textureCompression = compression;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectPath, assetPath));
        }
    }
}

#endif
