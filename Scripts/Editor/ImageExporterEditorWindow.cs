using UnityEngine;
using UnityEditor;
using System.IO;
using ArtTools.Framework;

namespace ArtTools.ImageTools
{
    public class ImageExporterEditorWindow : EditorWindow
    {
        private Camera cam;
        private Vector2 resolution = new Vector2(1280, 720);
        private int frameCount = 30;

        private enum ImageFormat { PNG, JPG }
        private ImageFormat imageFormat = ImageFormat.PNG;

        private bool isEnabledAlpha;
        private bool isExportSequence;

        private string fileName = "screenShot";
        private string filePath;

        private int rangeStart = 1;
        private int rangeEnd = 100;
        private float progress;

        private ImageExporterController controller;

        private CameraClearFlags cachedClearFlags;
        private Color cachedBackground;

        private bool foldoutBase = true;
        private bool foldoutSequence = true;

        static void Open()
        {
            var window = GetWindow<ImageExporterEditorWindow>("截图与序列图导出");
            window.minSize = new Vector2(320, 520);
        }

        
        static void TakeScreenshotShortcut()
        {
            var window = GetWindow<ImageExporterEditorWindow>();
            window.TakeSingleScreenshot();
        }

        void OnEnable()
        {
            Application.runInBackground = true;
            filePath = Application.dataPath;
        }

        void Update()
        {
            TickSequenceExport();
        }

        public void TickSequenceExport()
        {
            if (!isExportSequence || !EditorApplication.isPlaying || EditorApplication.isPaused)
                return;

            if (Time.frameCount < rangeStart)
                return;

            if (Time.frameCount > rangeEnd)
            {
                StopExport();
                return;
            }

            EnsureController();

            controller.cam = cam;
            controller.resolution = resolution;
            controller.frameCount = frameCount;
            controller.fileName = fileName;
            controller.filePath = filePath;
            controller.isEnabledAlpha = isEnabledAlpha;
            controller.imageFormat = imageFormat == ImageFormat.PNG ? ".png" : ".jpg";

            controller.TakeSequenceScreenShot();

            progress = (Time.frameCount - rangeStart) / (float)(rangeEnd - rangeStart);

            if (EditorUtility.DisplayCancelableProgressBar(
                "正在导出序列图",
                $"进度 {Mathf.RoundToInt(progress * 100)}%",
                progress))
            {
                StopExport();
            }
        }

        void EnsureController()
        {
            if (controller != null)
                return;

            GameObject go = new GameObject("ImageExporterController_Runtime");
            controller = go.AddComponent<ImageExporterController>();
        }

        void StopExport()
        {
            EditorUtility.ClearProgressBar();
            EditorApplication.isPlaying = false;
            isExportSequence = false;

            RestoreCamera();

            if (controller != null)
            {
                DestroyImmediate(controller.gameObject);
                controller = null;
            }
        }

        void CacheCamera()
        {
            if (cam == null) return;
            cachedClearFlags = cam.clearFlags;
            cachedBackground = cam.backgroundColor;
        }

        void RestoreCamera()
        {
            if (cam == null) return;
            cam.clearFlags = cachedClearFlags;
            cam.backgroundColor = cachedBackground;
        }

        void OnGUI()
        {
            DrawToolGUI();
        }

        public void DrawToolGUI()
        {
            if (string.IsNullOrEmpty(filePath))
                filePath = Application.dataPath;

            DrawHeader();
            DrawBaseSetting();
            DrawSequenceSetting();
            DrawBottomButtons();
        }

        #region UI

        void DrawHeader()
        {
            GUILayout.Space(8);

            GUIStyle title = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

            EditorGUILayout.LabelField("截图与序列图导出", title);
            EditorGUILayout.LabelField("透明截图 / 单张截图 / 序列帧导出", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(6);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        void DrawBaseSetting()
        {
            foldoutBase = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutBase, "相机与输出");
            if (!foldoutBase)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUILayout.BeginVertical("box");

            cam = EditorGUILayout.ObjectField("相机", cam, typeof(Camera), true) as Camera;
            if (cam == null) cam = Camera.main;

            imageFormat = (ImageFormat)EditorGUILayout.EnumPopup("格式", imageFormat);
            isEnabledAlpha = imageFormat == ImageFormat.PNG &&
                             EditorGUILayout.Toggle("启用透明通道", isEnabledAlpha);

            resolution = EditorGUILayout.Vector2Field("分辨率", resolution);
            fileName = EditorGUILayout.TextField("文件名前缀", fileName);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("保存路径");
            if (GUILayout.Button("浏览", GUILayout.Width(70)))
                filePath = EditorUtility.OpenFolderPanel("保存路径", filePath, "");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(filePath, MessageType.None);

            GUILayout.Space(10);

            if (GUILayout.Button("截取单张图片", GUILayout.Height(28)))
                TakeSingleScreenshot();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawSequenceSetting()
        {
            foldoutSequence = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutSequence, "序列帧导出");
            if (!foldoutSequence)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUILayout.BeginVertical("box");

            isExportSequence = EditorGUILayout.Toggle("启用序列帧导出", isExportSequence);

            using (new EditorGUI.DisabledScope(!isExportSequence))
            {
                frameCount = EditorGUILayout.IntPopup(
                    "帧率",
                    frameCount,
                    new[] { "12", "30", "60" },
                    new[] { 12, 30, 60 }
                );

                rangeStart = EditorGUILayout.IntField("起始帧", rangeStart);
                rangeEnd = EditorGUILayout.IntField("结束帧", rangeEnd);

                GUILayout.Space(8);

                if (GUILayout.Button("开始导出序列帧", GUILayout.Height(36)))
                {
                    CacheCamera();
                    Time.captureFramerate = frameCount;
                    EditorApplication.isPlaying = true;
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawBottomButtons()
        {
            GUILayout.Space(10);
            if (GUILayout.Button("打开输出目录"))
                EditorUtility.RevealInFinder(filePath);
        }

        #endregion

        #region Screenshot

        void TakeSingleScreenshot()
        {
            if (cam == null)
                cam = Camera.main;

            if (cam == null)
            {
                EditorUtility.DisplayDialog("截图失败", "没有找到可用相机。", "确定");
                return;
            }

            int width = Mathf.RoundToInt(resolution.x);
            int height = Mathf.RoundToInt(resolution.y);

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            TextureFormat texFormat = isEnabledAlpha ? TextureFormat.ARGB32 : TextureFormat.RGB24;
            Texture2D tex = new Texture2D(width, height, texFormat, false);

            CacheCamera();

            if (isEnabledAlpha)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.clear;
            }

            RenderTexture prevRT = RenderTexture.active;
            RenderTexture prevCamRT = cam.targetTexture;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevCamRT;
            RenderTexture.active = prevRT;

            string suffix = imageFormat == ImageFormat.PNG ? ".png" : ".jpg";
            byte[] bytes = imageFormat == ImageFormat.PNG
                ? ImageConversion.EncodeToPNG(tex)
                : ImageConversion.EncodeToJPG(tex);

            string file = Path.Combine(
                filePath,
                $"{fileName}_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}{suffix}"
            );

            File.WriteAllBytes(file, bytes);

            RestoreCamera();

            DestroyImmediate(rt);
            DestroyImmediate(tex);

            EditorWindow focused = focusedWindow;
            if (focused != null)
                focused.ShowNotification(new GUIContent("截图已保存"));

            EditorUtility.RevealInFinder(file);
        }

        #endregion

        void OnDisable()
        {
            EditorUtility.ClearProgressBar();
            RestoreCamera();
        }
    }
}

