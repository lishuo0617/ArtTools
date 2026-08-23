using UnityEditor;
using UnityEngine;

namespace ArtTools.EditorTools
{
    internal static class ArtToolsEditorUI
    {
        private static GUIStyle titleStyle;
        private static GUIStyle subtitleStyle;
        private static GUIStyle panelStyle;
        private static GUIStyle sectionTitleStyle;
        private static GUIStyle primaryButtonStyle;
        private static GUIStyle rowStyle;
        private static GUIStyle emptyStateStyle;

        public static GUIStyle RowStyle
        {
            get
            {
                EnsureStyles();
                return rowStyle;
            }
        }

        public static GUIStyle PrimaryButtonStyle
        {
            get
            {
                EnsureStyles();
                return primaryButtonStyle;
            }
        }

        public static void DrawHeader(string title, string subtitle, string iconName)
        {
            EnsureStyles();

            Rect rect = GUILayoutUtility.GetRect(0, 56, GUILayout.ExpandWidth(true));
            Color background = EditorGUIUtility.isProSkin ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.78f, 0.78f, 0.78f);
            Color line = EditorGUIUtility.isProSkin ? new Color(0.09f, 0.09f, 0.09f) : new Color(0.58f, 0.58f, 0.58f);

            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), line);

            Texture icon = GetIcon(iconName);
            Rect iconRect = new Rect(rect.x + 12, rect.y + 12, 30, 30);
            if (icon != null)
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

            float textX = icon != null ? iconRect.xMax + 10 : rect.x + 12;
            GUI.Label(new Rect(textX, rect.y + 9, rect.width - textX - 12, 22), title, titleStyle);
            GUI.Label(new Rect(textX, rect.y + 32, rect.width - textX - 12, 16), subtitle, subtitleStyle);
        }

        public static void BeginPanel(string title)
        {
            EnsureStyles();
            EditorGUILayout.BeginVertical(panelStyle);
            if (!string.IsNullOrEmpty(title))
            {
                EditorGUILayout.LabelField(title, sectionTitleStyle);
                GUILayout.Space(4);
            }
        }

        public static void EndPanel()
        {
            EditorGUILayout.EndVertical();
        }

        public static bool PrimaryButton(string text)
        {
            EnsureStyles();
            return GUILayout.Button(text, primaryButtonStyle);
        }

        public static void EmptyState(string text)
        {
            EnsureStyles();
            GUILayout.Label(text, emptyStateStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(42));
        }

        public static void Summary(string text)
        {
            EnsureStyles();
            EditorGUILayout.HelpBox(text, MessageType.None);
        }

        private static void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.86f, 0.86f, 0.86f) : new Color(0.18f, 0.18f, 0.18f) }
            };

            subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.64f, 0.64f, 0.64f) : new Color(0.36f, 0.36f, 0.36f) }
            };

            panelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 10),
                margin = new RectOffset(10, 10, 8, 0)
            };

            sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 30,
                fontStyle = FontStyle.Bold
            };

            rowStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 4, 0)
            };

            emptyStateStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }

        private static Texture GetIcon(string iconName)
        {
            GUIContent content = EditorGUIUtility.IconContent(iconName);
            return content != null ? content.image : null;
        }
    }
}
