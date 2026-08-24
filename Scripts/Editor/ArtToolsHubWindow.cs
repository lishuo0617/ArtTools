using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ArtTools.EditorTools
{
    public class ArtToolsHubWindow : EditorWindow
    {
        private sealed class ToolEntry
        {
            public readonly string Name;
            public readonly string Description;
            public readonly string TypeName;
            public readonly string IconName;
            public readonly string[] PreferredStaticMethods;

            public ToolEntry(string name, string description, string typeName, string iconName, params string[] preferredStaticMethods)
            {
                Name = name;
                Description = description;
                TypeName = typeName;
                IconName = iconName;
                PreferredStaticMethods = preferredStaticMethods;
            }
        }

        private Vector2 scrollPosition;
        private bool imageToolsExpanded = true;
        private bool assetToolsExpanded = true;
        private bool prefabSceneExpanded = true;
        private bool modelCheckExpanded = true;

        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle toolRowStyle;
        private GUIStyle toolNameStyle;
        private GUIStyle toolDescriptionStyle;
        private GUIStyle openButtonStyle;
        private GUIStyle footerStyle;

        private static readonly ToolEntry[] ImageTools =
        {
            new ToolEntry("截图与序列图导出", "导出编辑器截图、序列图和图示文档。", "ArtTools.ImageTools.ImageExporterEditorWindow", "d_Camera Icon"),
            new ToolEntry("贴图检查工具", "按尺寸阈值扫描项目贴图，快速定位过大资源。", "ArtTools.ImageTools.TextureCheckerTool", "d_Texture Icon", "ShowWindow", "Open"),
            new ToolEntry("查找未被引用的贴图", "扫描未被材质或资源引用的贴图，辅助清理项目。", "ArtTools.ImageTools.UnusedTextureFinder", "d_Search Icon", "ShowWindow", "Open"),
            new ToolEntry("批量修改图片尺寸", "批量处理图片导入尺寸和资源设置。", "ArtTools.ImageTools.TextureResizeTool", "d_Texture Icon", "ShowWindow", "Open")
        };

        private static readonly ToolEntry[] AssetTools =
        {
            new ToolEntry("3D资产整理工具", "整理 3D 资产目录、预制体和相关资源。", "ArtTools.EditorTools.AssetOrganizerWindow", "d_Folder Icon", "Open", "ShowWindow"),
            new ToolEntry("批量命名修改", "按选择顺序批量重命名，并可按编号调整层级排序。", "ArtTools.EditorTools.AutoNamer", "d_TextAsset Icon", "ShowWindow", "Open"),
            new ToolEntry("Missing Script 检查", "批量扫描 Prefab 缺失脚本，并定位到具体子节点。", "ArtTools.EditorTools.MissingScriptCheckerWindow", "d_console.warnicon", "Open"),
            new ToolEntry("缺失材质检测", "检查目标对象及子物体上缺失的材质槽位。", "ArtTools.EditorTools.MissingMaterialChecker", "d_Material Icon", "Open", "ShowWindow"),
            new ToolEntry("目录材质转换", "对指定目录内的材质进行批量转换。", "ArtTools.EditorTools.MaterialDirectoryConverterWindow", "d_PreMatCube", "Open")
        };

        private static readonly ToolEntry[] PrefabSceneTools =
        {
            new ToolEntry("装饰物工具", "生成装饰物、随机变换并批量替换 Prefab。", "ArtTools.EditorTools.DecorationTool", "d_TerrainInspector.TerrainToolPlants", "ShowWindow", "Open"),
            new ToolEntry("快速打开场景", "保存常用场景列表，一键打开并支持拖拽排序。", "ArtTools.EditorTools.SceneQuickOpener", "d_SceneAsset Icon", "ShowWindow", "Open")
        };

        private static readonly ToolEntry[] ModelCheckTools =
        {
            new ToolEntry("Smooth Normal Baker", "烘焙模型平滑法线数据。", "ArtTools.EditorTools.SmoothNormalBakerWindow", "d_Mesh Icon", "Open", "ShowWindow")
        };

        [MenuItem("Art Tools/美术工具中心", priority = 0)]
        public static void 打开窗口()
        {
            ArtToolsHubWindow window = GetWindow<ArtToolsHubWindow>("美术工具中心");
            window.minSize = new Vector2(620, 560);
            window.maxSize = new Vector2(1100, 1200);
            window.Show();
        }

        public static void Open()
        {
            打开窗口();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("美术工具中心", GetBuiltinIcon("d_ToolHandleCenter"));
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawHeader();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawSection("图片处理", "贴图、截图和图片资源处理", ref imageToolsExpanded, ImageTools);
            DrawSection("资产整理", "命名、材质和资产目录维护", ref assetToolsExpanded, AssetTools);
            DrawSection("预制体与场景", "Prefab 放置、装饰物和场景入口", ref prefabSceneExpanded, PrefabSceneTools);
            DrawSection("模型检查", "模型法线处理工具", ref modelCheckExpanded, ModelCheckTools);
            DrawFooter();

            EditorGUILayout.EndScrollView();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.86f, 0.86f, 0.86f) : new Color(0.18f, 0.18f, 0.18f) }
            };

            subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.63f, 0.63f, 0.63f) : new Color(0.36f, 0.36f, 0.36f) }
            };

            sectionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 10),
                margin = new RectOffset(10, 10, 8, 0)
            };

            toolRowStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 7, 7),
                margin = new RectOffset(0, 0, 5, 0)
            };

            toolNameStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };

            toolDescriptionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.62f, 0.62f, 0.62f) : new Color(0.38f, 0.38f, 0.38f) }
            };

            openButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fixedWidth = 72,
                fixedHeight = 24
            };

            footerStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(10, 10, 10, 10)
            };
        }

        private void DrawHeader()
        {
            Rect rect = GUILayoutUtility.GetRect(0, 58, GUILayout.ExpandWidth(true));
            Color background = EditorGUIUtility.isProSkin ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.78f, 0.78f, 0.78f);
            Color line = EditorGUIUtility.isProSkin ? new Color(0.09f, 0.09f, 0.09f) : new Color(0.58f, 0.58f, 0.58f);

            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), line);

            Texture icon = GetBuiltinIcon("d_ToolHandleCenter");
            Rect iconRect = new Rect(rect.x + 12, rect.y + 13, 32, 32);
            if (icon != null)
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

            float textX = icon != null ? iconRect.xMax + 10 : rect.x + 12;
            GUI.Label(new Rect(textX, rect.y + 10, rect.width - textX - 12, 22), "美术工具中心", titleStyle);
            GUI.Label(new Rect(textX, rect.y + 34, rect.width - textX - 12, 16), "ArtTools / Editor Utilities", subtitleStyle);
        }

        private void DrawSection(string title, string description, ref bool expanded, ToolEntry[] tools)
        {
            EditorGUILayout.BeginVertical(sectionStyle);

            EditorGUILayout.BeginHorizontal();
            expanded = EditorGUILayout.Foldout(expanded, title);
            GUILayout.FlexibleSpace();
            GUILayout.Label(description, subtitleStyle);
            EditorGUILayout.EndHorizontal();

            if (expanded)
            {
                GUILayout.Space(2);
                for (int i = 0; i < tools.Length; i++)
                    DrawToolRow(tools[i]);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawToolRow(ToolEntry tool)
        {
            EditorGUILayout.BeginHorizontal(toolRowStyle, GUILayout.MinHeight(48));

            Texture icon = GetBuiltinIcon(tool.IconName);
            Rect iconRect = GUILayoutUtility.GetRect(28, 28, GUILayout.Width(32), GUILayout.Height(32));
            if (icon != null)
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

            EditorGUILayout.BeginVertical();
            GUILayout.Label(tool.Name, toolNameStyle);
            GUILayout.Label(tool.Description, toolDescriptionStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);
            if (GUILayout.Button("打开", openButtonStyle))
                OpenTool(tool.TypeName, tool.Name, tool.PreferredStaticMethods);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginVertical(footerStyle);
            EditorGUILayout.LabelField("提示", EditorStyles.boldLabel);
            GUILayout.Label(
                "这里是统一的美术工具入口。工具中心会在点击按钮时通过完整命名空间查找并打开工具；即使某个工具脚本缺失或被屏蔽，也不会直接拖垮整个项目编译。",
                toolDescriptionStyle);
            EditorGUILayout.EndVertical();
        }

        private static void OpenTool(string toolTypeName, string displayName, params string[] preferredStaticMethodNames)
        {
            Type toolType = FindType(toolTypeName);
            if (toolType == null)
            {
                EditorUtility.DisplayDialog(
                    "工具未找到",
                    string.Format("找不到工具类：{0}\n\n请确认对应脚本已放入项目，并且没有被 #if false 屏蔽。", toolTypeName),
                    "OK");
                return;
            }

            for (int i = 0; i < preferredStaticMethodNames.Length; i++)
            {
                MethodInfo method = toolType.GetMethod(
                    preferredStaticMethodNames[i],
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null);

                if (method == null)
                    continue;

                method.Invoke(null, null);
                return;
            }

            if (typeof(EditorWindow).IsAssignableFrom(toolType))
            {
                EditorWindow window = GetWindow(toolType, false, displayName);
                window.Show();
                return;
            }

            EditorUtility.DisplayDialog(
                "无法打开工具",
                string.Format("工具类不是 EditorWindow，也没有可用的 Open/ShowWindow 方法：{0}", toolTypeName),
                "OK");
        }

        private static Type FindType(string typeName)
        {
            Type type = Type.GetType(typeName);
            if (type != null)
                return type;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static Texture GetBuiltinIcon(string iconName)
        {
            GUIContent content = EditorGUIUtility.IconContent(iconName);
            return content != null ? content.image : null;
        }
    }
}
