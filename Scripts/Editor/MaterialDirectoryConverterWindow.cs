#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace ArtTools.EditorTools
{
    public class MaterialDirectoryConverterWindow : EditorWindow
    {
        private enum RenderPipeline
        {
            Unknown,
            BuiltIn,
            URP,
            HDRP,
            Mixed
        }

        private enum UrpShaderVariant
        {
            Lit,
            SimpleLit
        }

        private struct TextureSlot
        {
            public Texture texture;
            public Vector2 scale;
            public Vector2 offset;
        }

        private sealed class MaterialRecord
        {
            public string assetPath;
            public string shaderName;
            public RenderPipeline pipeline;
        }

        private sealed class ConvertResult
        {
            public RenderPipeline sourcePipeline;
            public RenderPipeline targetPipeline;
            public string targetLabel;
            public int convertedCount;
            public int skippedCount;
            public readonly List<string> warnings = new List<string>();
        }

        private string sourceFolder = "Assets";
        private RenderPipeline activeSourcePipeline = RenderPipeline.BuiltIn;
        private RenderPipeline targetPipeline = RenderPipeline.URP;
        private UrpShaderVariant urpShaderVariant = UrpShaderVariant.Lit;
        private Shader overrideShader;
        private Vector2 scroll;
        private List<MaterialRecord> materials = new List<MaterialRecord>();
        private RenderPipeline detectedPipeline = RenderPipeline.Unknown;
        private ConvertResult lastResult;
        private GUIStyle materialPreviewRowStyle;

        public static void Open()
        {
            MaterialDirectoryConverterWindow window = GetWindow<MaterialDirectoryConverterWindow>("\u76ee\u5f55\u6750\u8d28\u8f6c\u6362");
            window.minSize = new Vector2(460, 420);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(8);

            DrawFolderSelector();
            GUILayout.Space(8);

            DrawAnalysis();
            GUILayout.Space(8);

            DrawSourcePipelineTabs();
            GUILayout.Space(8);

            DrawConvertControls();
            GUILayout.Space(8);

            DrawMaterialList();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            Rect rect = GUILayoutUtility.GetRect(0, 44, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
            GUI.Label(rect, "   \u76ee\u5f55\u6750\u8d28\u8f6c\u6362", new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            });
        }

        private void DrawFolderSelector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("\u626b\u63cf\u76ee\u5f55", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.SelectableLabel(sourceFolder, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button("\u9009\u62e9", GUILayout.Width(70)))
            {
                string selected = EditorUtility.OpenFolderPanel("\u9009\u62e9 Assets \u4e0b\u7684\u6750\u8d28\u76ee\u5f55", Application.dataPath, "");
                string assetPath = FullPathToAssetPath(selected);
                if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.IsValidFolder(assetPath))
                    sourceFolder = assetPath;
                else if (!string.IsNullOrEmpty(selected))
                    EditorUtility.DisplayDialog("\u76ee\u5f55\u65e0\u6548", "\u8bf7\u9009\u62e9\u5f53\u524d\u9879\u76ee Assets \u76ee\u5f55\u4e0b\u7684\u6587\u4ef6\u5939\u3002", "\u786e\u5b9a");
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("\u626b\u63cf\u6750\u8d28", GUILayout.Height(30)))
                ScanMaterials(true);

            EditorGUILayout.EndVertical();
        }

        private void DrawAnalysis()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("\u68c0\u6d4b\u7ed3\u679c", EditorStyles.boldLabel);

            int builtIn = 0;
            int urp = 0;
            int hdrp = 0;
            int unknown = 0;
            foreach (MaterialRecord material in materials)
            {
                switch (material.pipeline)
                {
                    case RenderPipeline.BuiltIn: builtIn++; break;
                    case RenderPipeline.URP: urp++; break;
                    case RenderPipeline.HDRP: hdrp++; break;
                    default: unknown++; break;
                }
            }

            EditorGUILayout.LabelField("\u6750\u8d28\u6570\u91cf", materials.Count.ToString());
            EditorGUILayout.LabelField("\u68c0\u6d4b\u7ba1\u7ebf", detectedPipeline.ToString());
            EditorGUILayout.LabelField($"Built-in: {builtIn}    URP: {urp}    HDRP: {hdrp}    Unknown: {unknown}");

            if (materials.Count == 0)
                EditorGUILayout.HelpBox("\u8bf7\u5148\u626b\u63cf\u4e00\u4e2a Assets \u4e0b\u7684\u76ee\u5f55\u3002", MessageType.Info);
            else if (detectedPipeline == RenderPipeline.Mixed)
                EditorGUILayout.HelpBox("\u68c0\u6d4b\u5230\u6df7\u5408\u7ba1\u7ebf\u6750\u8d28\uff0c\u8bf7\u5728\u4e0b\u65b9\u5206\u7c7b\u9009\u62e9\u9700\u8981\u8f6c\u6362\u7684\u6e90\u7ba1\u7ebf\u3002", MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        private void DrawConvertControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            int materialCount = CountMaterials(activeSourcePipeline);
            string sourceLabel = GetPipelineLabel(activeSourcePipeline);
            EditorGUILayout.LabelField($"当前分类：{sourceLabel} 材质（{materialCount}）", EditorStyles.boldLabel);

            bool sourceSupported = activeSourcePipeline == RenderPipeline.BuiltIn || activeSourcePipeline == RenderPipeline.URP;
            if (sourceSupported)
            {
                targetPipeline = DrawTargetPipelinePopup("转换为", targetPipeline);
                overrideShader = (Shader)EditorGUILayout.ObjectField("指定 Shader（可选）", overrideShader, typeof(Shader), false);
                if (targetPipeline == RenderPipeline.URP && overrideShader == null)
                    urpShaderVariant = (UrpShaderVariant)EditorGUILayout.EnumPopup("URP Shader 类型", urpShaderVariant);

                string targetLabel = GetTargetLabel(targetPipeline, overrideShader, urpShaderVariant);
                string pipelineWarning;
                bool pipelineReady = IsTargetPipelineActive(targetPipeline, out pipelineWarning);
                HashSet<string> projectSelection = GetProjectSelectedPaths(activeSourcePipeline);
                bool hasProjectSelection = projectSelection.Count > 0;
                int operationCount = hasProjectSelection ? projectSelection.Count : materialCount;
                string operationLabel = hasProjectSelection ? $"转换 Project 面板已选 {operationCount} 个材质" : $"转换当前分类全部 {operationCount} 个材质";
                bool canConvertOperation = operationCount > 0 && (overrideShader != null || targetPipeline != activeSourcePipeline);
                using (new EditorGUI.DisabledScope(!canConvertOperation))
                {
                    if (GUILayout.Button($"{operationLabel}为 {targetLabel}", GUILayout.Height(34)))
                        ConvertMaterials(activeSourcePipeline, targetPipeline, operationCount, overrideShader, urpShaderVariant, hasProjectSelection ? projectSelection : null);
                }

                if (overrideShader != null)
                    EditorGUILayout.HelpBox($"已指定 Shader：{overrideShader.name}。转换时会优先使用它，当前分类之外的材质不会被修改。", MessageType.Info);
                else if (targetPipeline == activeSourcePipeline)
                    EditorGUILayout.HelpBox("源管线与目标管线相同，请更改目标管线或指定 Shader。", MessageType.Info);

                if (!pipelineReady)
                    EditorGUILayout.HelpBox(pipelineWarning, MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox($"{sourceLabel} 材质当前仅作分类展示。请选择 Built-in 或 URP 标签进行转换，可避免不兼容的 HDRP / 未知 Shader 被误改。", MessageType.Warning);
            }

            if (lastResult != null)
            {
                EditorGUILayout.LabelField($"上次转换：{GetPipelineLabel(lastResult.sourcePipeline)} → {lastResult.targetLabel}，成功 {lastResult.convertedCount}，跳过 {lastResult.skippedCount}", EditorStyles.miniLabel);
                foreach (string warning in lastResult.warnings)
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMaterialList()
        {
            if (materials.Count == 0)
                return;

            EditorGUILayout.LabelField($"{GetPipelineLabel(activeSourcePipeline)} 材质预览", EditorStyles.boldLabel);
            HashSet<string> projectSelection = GetProjectSelectedPaths(activeSourcePipeline);
            EditorGUILayout.LabelField(
                projectSelection.Count > 0
                    ? $"Project 面板当前选中 {projectSelection.Count} 个当前分类材质，点击转换将只处理这些材质"
                    : "Project 面板未多选当前分类材质，点击转换将处理当前分类全部材质",
                EditorStyles.miniLabel);
            int shown = 0;
            foreach (MaterialRecord material in materials)
            {
                if (material.pipeline != activeSourcePipeline)
                    continue;

                Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2, GUILayout.ExpandWidth(true));
                if (GUI.Button(rowRect, material.assetPath, GetMaterialPreviewRowStyle()))
                    LocateMaterial(material.assetPath);
                shown++;
                if (shown >= 80)
                    break;
            }

            if (shown >= 80)
                EditorGUILayout.LabelField("...\u5217\u8868\u8fc7\u957f\uff0c\u4ec5\u663e\u793a\u524d 80 \u4e2a", EditorStyles.miniLabel);
        }

        private GUIStyle GetMaterialPreviewRowStyle()
        {
            if (materialPreviewRowStyle != null)
                return materialPreviewRowStyle;

            materialPreviewRowStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(2, 2, 0, 0)
            };
            materialPreviewRowStyle.hover.textColor = Color.white;
            return materialPreviewRowStyle;
        }

        private void DrawSourcePipelineTabs()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("材质分类", EditorStyles.boldLabel);
            string[] labels =
            {
                $"Built-in ({CountMaterials(RenderPipeline.BuiltIn)})",
                $"URP ({CountMaterials(RenderPipeline.URP)})",
                $"HDRP ({CountMaterials(RenderPipeline.HDRP)})",
                $"Unknown ({CountMaterials(RenderPipeline.Unknown)})"
            };
            RenderPipeline[] pipelines =
            {
                RenderPipeline.BuiltIn,
                RenderPipeline.URP,
                RenderPipeline.HDRP,
                RenderPipeline.Unknown
            };
            int selected = Array.IndexOf(pipelines, activeSourcePipeline);
            if (selected < 0)
                selected = 0;

            activeSourcePipeline = pipelines[GUILayout.Toolbar(selected, labels)];
            EditorGUILayout.EndVertical();
        }

        private static void LocateMaterial(string assetPath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
                return;

            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
        }

        private void ScanMaterials(bool clearLastResult = true)
        {
            materials.Clear();
            if (clearLastResult)
                lastResult = null;

            if (!AssetDatabase.IsValidFolder(sourceFolder))
            {
                EditorUtility.DisplayDialog("\u76ee\u5f55\u65e0\u6548", "\u8bf7\u9009\u62e9\u5f53\u524d\u9879\u76ee Assets \u76ee\u5f55\u4e0b\u7684\u6587\u4ef6\u5939\u3002", "\u786e\u5b9a");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { sourceFolder });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                string shaderName = material != null && material.shader != null ? material.shader.name : "Unknown";
                materials.Add(new MaterialRecord
                {
                    assetPath = assetPath,
                    shaderName = shaderName,
                    pipeline = DetectShaderPipeline(shaderName)
                });
            }

            detectedPipeline = ResolvePipeline(materials);
        }

        private static RenderPipeline DrawTargetPipelinePopup(string label, RenderPipeline current)
        {
            RenderPipeline[] options = { RenderPipeline.BuiltIn, RenderPipeline.URP, RenderPipeline.HDRP };
            string[] optionLabels = { "Built-in", "URP", "HDRP" };
            int selected = Array.IndexOf(options, current);
            if (selected < 0)
                selected = 0;

            return options[EditorGUILayout.Popup(label, selected, optionLabels)];
        }

        private static string GetTargetLabel(RenderPipeline pipeline, Shader shader, UrpShaderVariant variant)
        {
            if (shader != null)
                return shader.name;

            return pipeline == RenderPipeline.URP ? $"URP/{variant}" : GetPipelineLabel(pipeline);
        }

        private static bool IsTargetPipelineActive(RenderPipeline targetPipeline, out string warning)
        {
            object activeAsset = GetActiveRenderPipelineAsset();
            string activeName = activeAsset == null ? "Built-in" : activeAsset.GetType().Name;

            if (targetPipeline == RenderPipeline.BuiltIn)
            {
                warning = string.Empty;
                return activeAsset == null;
            }

            if (activeAsset == null)
            {
                warning = $"当前工程实际使用 Built-in 管线，不能安全转换为 {GetPipelineLabel(targetPipeline)} Shader；否则材质会变粉。请先在 Graphics Settings / Quality Settings 启用对应的渲染管线资产。";
                return false;
            }

            string typeName = activeAsset.GetType().FullName ?? activeName;
            bool matches = targetPipeline == RenderPipeline.URP
                ? typeName.IndexOf("UniversalRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0
                : typeName.IndexOf("HDRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0;

            warning = matches
                ? string.Empty
                : $"当前工程使用 {activeName}，与目标 {GetPipelineLabel(targetPipeline)} 管线不匹配；继续转换会导致粉色材质。";
            return matches;
        }

        private static object GetActiveRenderPipelineAsset()
        {
            // GraphicsSettings.currentRenderPipeline is unavailable in older Unity.
            // Resolve both the modern and legacy property names without a hard SRP type reference.
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Type graphicsSettingsType = null;
            for (int i = 0; i < assemblies.Length && graphicsSettingsType == null; i++)
                graphicsSettingsType = assemblies[i].GetType("UnityEngine.Rendering.GraphicsSettings");

            if (graphicsSettingsType == null)
                return null;

            PropertyInfo property = graphicsSettingsType.GetProperty("currentRenderPipeline", BindingFlags.Public | BindingFlags.Static)
                ?? graphicsSettingsType.GetProperty("renderPipelineAsset", BindingFlags.Public | BindingFlags.Static);
            return property != null ? property.GetValue(null, null) : null;
        }

        private int CountMaterials(RenderPipeline pipeline)
        {
            int count = 0;
            foreach (MaterialRecord material in materials)
            {
                if (material.pipeline == pipeline)
                    count++;
            }
            return count;
        }

        private HashSet<string> GetProjectSelectedPaths(RenderPipeline pipeline)
        {
            HashSet<string> paths = new HashSet<string>();
            foreach (UnityEngine.Object selectedObject in Selection.objects)
            {
                Material selectedMaterial = selectedObject as Material;
                if (selectedMaterial == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(selectedMaterial);
                foreach (MaterialRecord material in materials)
                {
                    if (material.pipeline == pipeline && material.assetPath == path)
                    {
                        paths.Add(path);
                        break;
                    }
                }
            }
            return paths;
        }

        private static string GetPipelineLabel(RenderPipeline pipeline)
        {
            return pipeline == RenderPipeline.BuiltIn ? "Built-in" : pipeline.ToString();
        }

        private void ConvertMaterials(RenderPipeline sourcePipeline, RenderPipeline targetPipeline, int materialCount, Shader customShader, UrpShaderVariant variant, HashSet<string> selectedPaths = null)
        {
            string pipelineWarning;
            string targetLabel = GetTargetLabel(targetPipeline, customShader, variant);
            IsTargetPipelineActive(targetPipeline, out pipelineWarning);
            string pipelineNotice = string.IsNullOrEmpty(pipelineWarning) ? string.Empty : $"\n\n警告：{pipelineWarning}";
            bool confirmed = EditorUtility.DisplayDialog(
                "\u786e\u8ba4\u8f6c\u6362\u6750\u8d28",
                $"\u8fd9\u4f1a\u5c06\u5f53\u524d\u76ee\u5f55\u4e2d {materialCount} \u4e2a {GetPipelineLabel(sourcePipeline)} \u6750\u8d28\u8f6c\u6362\u4e3a {targetLabel}\u3002{pipelineNotice}\n\n\u5176\u4ed6\u7c7b\u578b\u6750\u8d28\u4e0d\u4f1a\u88ab\u4fee\u6539\u3002\u5efa\u8bae\u5148\u786e\u8ba4\u8d44\u6e90\u5df2\u5907\u4efd\u3002\n\n\u662f\u5426\u7ee7\u7eed\uff1f",
                "\u7ee7\u7eed\u8f6c\u6362",
                "\u53d6\u6d88");

            if (!confirmed)
                return;

            Shader targetShader = customShader != null ? customShader : GetTargetShader(targetPipeline, variant);
            lastResult = new ConvertResult
            {
                sourcePipeline = sourcePipeline,
                targetPipeline = targetPipeline,
                targetLabel = targetLabel
            };
            if (targetShader == null)
            {
                lastResult.warnings.Add("\u627e\u4e0d\u5230\u76ee\u6807\u7ba1\u7ebf Shader: " + targetPipeline);
                return;
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (MaterialRecord record in materials)
                {
                    if (record.pipeline != sourcePipeline)
                        continue;
                    if (selectedPaths != null && !selectedPaths.Contains(record.assetPath))
                        continue;

                    Material material = AssetDatabase.LoadAssetAtPath<Material>(record.assetPath);
                    if (material == null)
                    {
                        lastResult.skippedCount++;
                        continue;
                    }

                    ConvertMaterial(material, targetShader, targetPipeline, variant);
                    EditorUtility.SetDirty(material);
                    lastResult.convertedCount++;
                }
            }
            catch (Exception e)
            {
                lastResult.warnings.Add(e.Message);
                Debug.LogError("\u6750\u8d28\u7ba1\u7ebf\u8f6c\u6362\u5931\u8d25:\n" + e);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            ScanMaterials(false);
        }

        private static RenderPipeline DetectShaderPipeline(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
                return RenderPipeline.Unknown;

            string lower = shaderName.ToLowerInvariant();
            if (lower.Contains("universal render pipeline") || lower.Contains("/universal/") ||
                lower.Contains("shadergraphs") || lower.Contains("urp"))
                return RenderPipeline.URP;

            if (lower.Contains("high definition render pipeline") || lower.Contains("hdrp") ||
                lower.Contains("high definition"))
                return RenderPipeline.HDRP;

            if (lower == "standard" || lower.StartsWith("legacy shaders") ||
                lower.StartsWith("mobile/") || lower.StartsWith("particles/") ||
                lower.StartsWith("sprites/") || lower.StartsWith("nature/") ||
                lower.StartsWith("unlit/"))
                return RenderPipeline.BuiltIn;

            return RenderPipeline.Unknown;
        }

        private static RenderPipeline ResolvePipeline(List<MaterialRecord> records)
        {
            bool hasBuiltIn = false;
            bool hasUrp = false;
            bool hasHdrp = false;

            foreach (MaterialRecord record in records)
            {
                hasBuiltIn |= record.pipeline == RenderPipeline.BuiltIn;
                hasUrp |= record.pipeline == RenderPipeline.URP;
                hasHdrp |= record.pipeline == RenderPipeline.HDRP;
            }

            int active = (hasBuiltIn ? 1 : 0) + (hasUrp ? 1 : 0) + (hasHdrp ? 1 : 0);
            if (active > 1)
                return RenderPipeline.Mixed;
            if (hasBuiltIn)
                return RenderPipeline.BuiltIn;
            if (hasUrp)
                return RenderPipeline.URP;
            if (hasHdrp)
                return RenderPipeline.HDRP;
            return RenderPipeline.Unknown;
        }

        private static Shader GetTargetShader(RenderPipeline pipeline, UrpShaderVariant variant)
        {
            switch (pipeline)
            {
                case RenderPipeline.URP:
                    return variant == UrpShaderVariant.SimpleLit
                        ? Shader.Find("Universal Render Pipeline/Simple Lit")
                        : Shader.Find("Universal Render Pipeline/Lit");
                case RenderPipeline.BuiltIn:
                    return Shader.Find("Standard");
                case RenderPipeline.HDRP:
                    return Shader.Find("HDRP/Lit")
                        ?? Shader.Find("HDRenderPipeline/Lit");
                default:
                    return null;
            }
        }

        private static void ConvertMaterial(Material material, Shader targetShader, RenderPipeline targetPipeline, UrpShaderVariant variant)
        {
            TextureSlot mainTex = GetTextureSlot(material, "_BaseMap", "_BaseColorMap", "_MainTex");
            TextureSlot normalMap = GetTextureSlot(material, "_BumpMap");
            TextureSlot emissionMap = GetTextureSlot(material, "_EmissionMap");
            TextureSlot metallicGlossMap = GetTextureSlot(material, "_MetallicGlossMap", "_SpecGlossMap");
            TextureSlot occlusionMap = GetTextureSlot(material, "_OcclusionMap");
            TextureSlot detailAlbedoMap = GetTextureSlot(material, "_DetailAlbedoMap");
            TextureSlot detailMask = GetTextureSlot(material, "_DetailMask");
            TextureSlot detailNormalMap = GetTextureSlot(material, "_DetailNormalMap");
            TextureSlot parallaxMap = GetTextureSlot(material, "_ParallaxMap");
            Color baseColor = GetColor(material, Color.white, "_BaseColor", "_Color");
            Color emissionColor = GetColor(material, Color.black, "_EmissionColor");
            Color specularColor = GetColor(material, Color.white, "_SpecColor");
            float metallic = GetFloat(material, 0f, "_Metallic");
            float smoothness = GetFloat(material, 0.5f, "_Smoothness", "_Glossiness");
            float bumpScale = GetFloat(material, 1f, "_BumpScale");
            float occlusionStrength = GetFloat(material, 1f, "_OcclusionStrength");
            float detailNormalScale = GetFloat(material, 1f, "_DetailNormalMapScale");
            float parallax = GetFloat(material, 0.02f, "_Parallax");

            material.shader = targetShader;

            if (targetPipeline != RenderPipeline.BuiltIn)
            {
                SetTextureSlot(material, mainTex, "_BaseMap", "_BaseColorMap", "_MainTex");
                SetColor(material, baseColor, "_BaseColor", "_Color");
            }
            else
            {
                SetTextureSlot(material, mainTex, "_MainTex", "_BaseMap", "_BaseColorMap");
                SetColor(material, baseColor, "_Color", "_BaseColor");
            }

            SetTextureSlot(material, normalMap, "_BumpMap");
            SetTextureSlot(material, emissionMap, "_EmissionMap");
            SetTextureSlot(material, occlusionMap, "_OcclusionMap");
            SetTextureSlot(material, detailAlbedoMap, "_DetailAlbedoMap");
            SetTextureSlot(material, detailMask, "_DetailMask");
            SetTextureSlot(material, detailNormalMap, "_DetailNormalMap");
            SetTextureSlot(material, parallaxMap, "_ParallaxMap");
            if (targetPipeline == RenderPipeline.URP && variant == UrpShaderVariant.SimpleLit)
                SetTextureSlot(material, metallicGlossMap, "_SpecGlossMap", "_MetallicGlossMap");
            else
                SetTextureSlot(material, metallicGlossMap, "_MetallicGlossMap", "_SpecGlossMap");
            SetColor(material, emissionColor, "_EmissionColor");
            SetColor(material, specularColor, "_SpecColor");
            SetFloat(material, metallic, "_Metallic");
            SetFloat(material, smoothness, "_Smoothness", "_Glossiness");
            SetFloat(material, bumpScale, "_BumpScale");
            SetFloat(material, occlusionStrength, "_OcclusionStrength");
            SetFloat(material, detailNormalScale, "_DetailNormalMapScale");
            SetFloat(material, parallax, "_Parallax");

            if (normalMap.texture != null)
                material.EnableKeyword("_NORMALMAP");
            float maxEmission = Mathf.Max(emissionColor.r, Mathf.Max(emissionColor.g, emissionColor.b));
            if (emissionMap.texture != null || maxEmission > 0.001f)
                material.EnableKeyword("_EMISSION");
        }

        private static TextureSlot GetTextureSlot(Material material, params string[] names)
        {
            TextureSlot fallback = new TextureSlot { scale = Vector2.one, offset = Vector2.zero };
            bool hasFallback = false;
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    TextureSlot slot = new TextureSlot
                    {
                        texture = material.GetTexture(name),
                        scale = material.GetTextureScale(name),
                        offset = material.GetTextureOffset(name)
                    };

                    if (slot.texture != null)
                        return slot;

                    if (!hasFallback)
                    {
                        fallback = slot;
                        hasFallback = true;
                    }
                }
            }
            return fallback;
        }

        private static Color GetColor(Material material, Color fallback, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                    return material.GetColor(name);
            }
            return fallback;
        }

        private static float GetFloat(Material material, float fallback, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                    return material.GetFloat(name);
            }
            return fallback;
        }

        private static void SetTextureSlot(Material material, TextureSlot slot, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    material.SetTexture(name, slot.texture);
                    material.SetTextureScale(name, slot.scale);
                    material.SetTextureOffset(name, slot.offset);
                }
            }
        }

        private static void SetColor(Material material, Color color, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    material.SetColor(name, color);
                    return;
                }
            }
        }

        private static void SetFloat(Material material, float value, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    material.SetFloat(name, value);
                    return;
                }
            }
        }

        private static string FullPathToAssetPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return "";

            fullPath = fullPath.Replace("\\", "/");
            string dataPath = Application.dataPath.Replace("\\", "/");
            if (!fullPath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                return "";

            return "Assets" + fullPath.Substring(dataPath.Length);
        }
    }
}
#endif
