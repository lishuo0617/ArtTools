using UnityEngine;
using UnityEditor;
using System.IO;
using ArtTools.Framework;
#if UNITY_2019_1_OR_NEWER
using UnityEditor.ShortcutManagement;
#endif

namespace ArtTools.ImageTools
{
    public class ImageExporterEditorWindow : EditorWindow
    {
#if UNITY_2019_1_OR_NEWER
        private const string ScreenshotShortcutId = "ArtTools/截图与序列图导出/截取单张图片";
#endif

        [SerializeField] private Camera cam;
        [SerializeField] private Vector2 resolution = new Vector2(1280, 720);
        [SerializeField] private int frameCount = 30;

        private enum ImageFormat { PNG, JPG }
        [SerializeField] private ImageFormat imageFormat = ImageFormat.PNG;

        [SerializeField] private bool isEnabledAlpha;
        [SerializeField] private bool isExportSequence;

        [SerializeField] private string fileName = "screenShot";
        [SerializeField] private string filePath;

        [SerializeField] private int rangeStart = 1;
        [SerializeField] private int rangeEnd = 100;
        private float progress;

        private ImageExporterController controller;

        [SerializeField] private CameraClearFlags cachedClearFlags;
        [SerializeField] private Color cachedBackground;

        private bool foldoutBase = true;
        private bool foldoutSequence = true;

#if UNITY_2019_1_OR_NEWER
        private KeyCode shortcutKey = KeyCode.S;
        private bool shortcutAction = true;
        private bool shortcutShift = true;
        private bool shortcutAlt;
        private string shortcutStatus;
        private MessageType shortcutStatusType = MessageType.Info;
#endif

        static void Open()
        {
            var window = GetWindow<ImageExporterEditorWindow>("截图与序列图导出");
            window.minSize = new Vector2(320, 620);
        }

#if UNITY_2019_1_OR_NEWER
        [Shortcut(
            ScreenshotShortcutId,
            KeyCode.S,
            ShortcutModifiers.Action | ShortcutModifiers.Shift)]
#endif
        static void TakeScreenshotShortcut()
        {
            var window = GetWindow<ImageExporterEditorWindow>();
            window.Show();
            if (window.cam == null)
                window.cam = Camera.main;
            window.TakeSingleScreenshot();
        }

        void OnEnable()
        {
            Application.runInBackground = true;
            if (string.IsNullOrEmpty(filePath))
                filePath = Application.dataPath;
            if (cam == null)
                cam = Camera.main;

#if UNITY_2019_1_OR_NEWER
            ShortcutManager.instance.shortcutBindingChanged -= OnShortcutBindingChanged;
            ShortcutManager.instance.shortcutBindingChanged += OnShortcutBindingChanged;
            RefreshShortcutFields();
            RefreshShortcutConflictStatus();
#endif
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
                StopExport(true);
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
                StopExport(false);
            }
        }

        void EnsureController()
        {
            if (controller != null)
                return;

            GameObject go = new GameObject("ImageExporterController_Runtime");
            controller = go.AddComponent<ImageExporterController>();
        }

        void StopExport(bool completed)
        {
            EditorUtility.ClearProgressBar();
            string lastSavedFile = completed && controller != null
                ? controller.LastSavedFile
                : null;
            EditorApplication.isPlaying = false;
            isExportSequence = false;

            RestoreCamera();

            if (controller != null)
            {
                DestroyImmediate(controller.gameObject);
                controller = null;
            }

            if (!string.IsNullOrEmpty(lastSavedFile) && File.Exists(lastSavedFile))
                EditorUtility.RevealInFinder(lastSavedFile);
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

            DrawShortcutSettings();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawShortcutSettings()
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("单张截图快捷键", EditorStyles.boldLabel);

#if UNITY_2019_1_OR_NEWER
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("当前快捷键");
            EditorGUILayout.SelectableLabel(
                GetCurrentShortcutText(),
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            shortcutAction = EditorGUILayout.ToggleLeft("Ctrl / Cmd", shortcutAction, GUILayout.Width(82));
            shortcutShift = EditorGUILayout.ToggleLeft("Shift", shortcutShift, GUILayout.Width(55));
            shortcutAlt = EditorGUILayout.ToggleLeft("Alt", shortcutAlt, GUILayout.Width(45));
            shortcutKey = (KeyCode)EditorGUILayout.EnumPopup(shortcutKey);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(shortcutKey == KeyCode.None))
            {
                if (GUILayout.Button("应用快捷键"))
                    ApplyShortcutBinding();
            }

            if (GUILayout.Button("恢复默认"))
                ResetShortcutBinding();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(shortcutStatus))
                EditorGUILayout.HelpBox(shortcutStatus, shortcutStatusType);
            else
                EditorGUILayout.HelpBox(
                    "快捷键只在 Unity 处于激活状态时生效，也可以在 Edit > Shortcuts 中修改。",
                    MessageType.None);
#else
            EditorGUILayout.HelpBox(
                "动态快捷键需要 Unity 2019.1 或更高版本。当前版本仍可使用“截取单张图片”按钮。",
                MessageType.Info);
#endif
        }

#if UNITY_2019_1_OR_NEWER
        void RefreshShortcutFields()
        {
            ShortcutBinding binding = ShortcutManager.instance.GetShortcutBinding(ScreenshotShortcutId);
            foreach (KeyCombination combination in binding.keyCombinationSequence)
            {
                shortcutKey = combination.keyCode;
                shortcutAction = combination.action || combination.control;
                shortcutShift = combination.shift;
                shortcutAlt = combination.alt;
                return;
            }

            shortcutKey = KeyCode.None;
            shortcutAction = false;
            shortcutShift = false;
            shortcutAlt = false;
        }

        string GetCurrentShortcutText()
        {
            ShortcutBinding binding = ShortcutManager.instance.GetShortcutBinding(ScreenshotShortcutId);
            string text = binding.ToString();
            return string.IsNullOrEmpty(text) ? "未设置" : text;
        }

        void ApplyShortcutBinding()
        {
            ShortcutModifiers modifiers = ShortcutModifiers.None;
            if (shortcutAction) modifiers |= ShortcutModifiers.Action;
            if (shortcutShift) modifiers |= ShortcutModifiers.Shift;
            if (shortcutAlt) modifiers |= ShortcutModifiers.Alt;

            ShortcutBinding newBinding = new ShortcutBinding(
                new KeyCombination(shortcutKey, modifiers));
            string conflictId = FindShortcutConflict(newBinding);

            if (!string.IsNullOrEmpty(conflictId))
            {
                shortcutStatus = "快捷键与“" + conflictId + "”冲突，请更换组合。";
                shortcutStatusType = MessageType.Warning;
                return;
            }

            try
            {
                ShortcutManager.instance.RebindShortcut(ScreenshotShortcutId, newBinding);
                shortcutStatus = "快捷键已更新为 " + GetCurrentShortcutText() + "。";
                shortcutStatusType = MessageType.Info;
            }
            catch (System.Exception exception)
            {
                shortcutStatus = "快捷键设置失败：" + exception.Message;
                shortcutStatusType = MessageType.Error;
            }
            Repaint();
        }

        void ResetShortcutBinding()
        {
            try
            {
                ShortcutManager.instance.ClearShortcutOverride(ScreenshotShortcutId);
                RefreshShortcutFields();
                RefreshShortcutConflictStatus();
                if (string.IsNullOrEmpty(shortcutStatus))
                {
                    shortcutStatus = "已恢复默认快捷键：" + GetCurrentShortcutText() + "。";
                    shortcutStatusType = MessageType.Info;
                }
            }
            catch (System.Exception exception)
            {
                shortcutStatus = "恢复默认快捷键失败：" + exception.Message;
                shortcutStatusType = MessageType.Error;
            }
            Repaint();
        }

        void RefreshShortcutConflictStatus()
        {
            if (shortcutKey == KeyCode.None)
            {
                shortcutStatus = "当前没有设置快捷键。";
                shortcutStatusType = MessageType.Info;
                return;
            }

            ShortcutBinding currentBinding =
                ShortcutManager.instance.GetShortcutBinding(ScreenshotShortcutId);
            string conflictId = FindShortcutConflict(currentBinding);

            if (!string.IsNullOrEmpty(conflictId))
            {
                shortcutStatus = "当前快捷键与“" + conflictId + "”冲突，请重新设置。";
                shortcutStatusType = MessageType.Warning;
            }
            else
            {
                shortcutStatus = null;
            }
        }

        string FindShortcutConflict(ShortcutBinding candidate)
        {
            foreach (string shortcutId in ShortcutManager.instance.GetAvailableShortcutIds())
            {
                if (shortcutId == ScreenshotShortcutId)
                    continue;

                ShortcutBinding binding = ShortcutManager.instance.GetShortcutBinding(shortcutId);
                if (candidate.Equals(binding))
                    return shortcutId;
            }

            return null;
        }

        void OnShortcutBindingChanged(ShortcutBindingChangedEventArgs args)
        {
            if (args.shortcutId != ScreenshotShortcutId)
                return;

            RefreshShortcutFields();
            RefreshShortcutConflictStatus();
            Repaint();
        }
#endif

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
#if UNITY_2019_1_OR_NEWER
            ShortcutManager.instance.shortcutBindingChanged -= OnShortcutBindingChanged;
#endif
            EditorUtility.ClearProgressBar();
            RestoreCamera();
        }
    }
}

