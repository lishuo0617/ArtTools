#if false
using ArtTools.Framework;
using UnityEditor;
using UnityEngine;

namespace ArtTools.ImageTools
{
    public class ArtToolsImageHubWindow : EditorWindow
    {
        private readonly string[] 页签列表 =
        {
            "总览",
            "截图导出"
        };

        private int 当前页签;
        private Vector2 总览滚动位置;
        private ImageExporterEditorWindow 截图导出工具;

        public static void 打开窗口()
        {
            ArtToolsImageHubWindow 窗口 = GetWindow<ArtToolsImageHubWindow>("图片处理中心");
            窗口.minSize = new Vector2(620, 620);
            窗口.Show();
        }

        public static void Open()
        {
            打开窗口();
        }

        private void OnEnable()
        {
            确保工具实例();
        }

        private void OnDisable()
        {
            if (截图导出工具 != null)
                DestroyImmediate(截图导出工具);
        }

        private void Update()
        {
            if (截图导出工具 != null)
                截图导出工具.TickSequenceExport();
        }

        private void OnGUI()
        {
            确保工具实例();
            绘制标题栏();

            当前页签 = GUILayout.Toolbar(当前页签, 页签列表, GUILayout.Height(28));
            GUILayout.Space(8);

            if (当前页签 == 0)
                绘制总览();
            else
                截图导出工具.DrawToolGUI();
        }

        private void 确保工具实例()
        {
            if (截图导出工具 == null)
                截图导出工具 = CreateInstance<ImageExporterEditorWindow>();
        }

        private void 绘制标题栏()
        {
            Rect 区域 = GUILayoutUtility.GetRect(0, 42, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(区域, new Color(0.16f, 0.16f, 0.16f));

            GUIStyle 标题样式 = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(区域.x + 12, 区域.y, 区域.width - 24, 区域.height), "图片处理中心", 标题样式);
        }

        private void 绘制总览()
        {
            总览滚动位置 = EditorGUILayout.BeginScrollView(总览滚动位置);

            绘制分区("导出");
            绘制页签按钮("截图与序列图导出", 1);
            结束分区();

            绘制分区("检查");
            绘制直接按钮("贴图检查工具", TextureCheckerTool.ShowWindow);
            绘制直接按钮("查找未被引用的贴图", UnusedTextureFinder.ShowWindow);
            结束分区();

            绘制分区("尺寸处理");
            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.Button("修改尺寸工具（待迁移）", GUILayout.Height(30));
            }
            结束分区();

            EditorGUILayout.HelpBox(
                "图片处理中心会优先承载图片相关工具。当前部分旧工具仍以独立窗口打开，后续可以继续迁移为同一个窗口里的完整页签。",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        private void 绘制分区(string 标题)
        {
            GUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(标题, EditorStyles.boldLabel);
            GUILayout.Space(4);
        }

        private void 结束分区()
        {
            EditorGUILayout.EndVertical();
        }

        private void 绘制页签按钮(string 按钮名, int 目标页签)
        {
            if (GUILayout.Button(按钮名, GUILayout.Height(30)))
                当前页签 = 目标页签;
        }

        private void 绘制直接按钮(string 按钮名, System.Action 打开方法)
        {
            if (GUILayout.Button(按钮名, GUILayout.Height(30)))
                打开方法();
        }
    }
}

#endif

