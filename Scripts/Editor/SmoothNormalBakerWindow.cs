using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ArtTools.EditorTools
{
    public class SmoothNormalBakerWindow : EditorWindow
    {
        private Object targetObject;
        private bool bakeToMeshAsset = true;
        private bool overwriteMeshReference = true;
        private bool writeToUV2 = true;
        private bool writeToUV3 = false;
        private bool normalizeResult = true;
        private string saveFolder = "Assets/ArtTools/GeneratedMeshes";
        private Vector2 scroll;

        private float smoothingAngle = 60f;

        public static void Open()
        {
            SmoothNormalBakerWindow window = GetWindow<SmoothNormalBakerWindow>("Smooth Normal Baker");
            window.minSize = new Vector2(560, 560);
            window.Show();
        }

        private void OnGUI()
        {
            DrawWindowHeader();
            GUILayout.Space(8);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawIntroPanel();
            GUILayout.Space(10);

            DrawTargetSection();
            GUILayout.Space(10);

            DrawOptionSection();
            GUILayout.Space(10);

            DrawOutputSection();
            GUILayout.Space(12);

            DrawActionSection();

            GUILayout.Space(10);
            EditorGUILayout.EndScrollView();
        }

        #region UI

        private void DrawWindowHeader()
        {
            Rect rect = EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            // GUILayout.Label("Smooth Normal Baker", EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawIntroPanel()
        {
            EditorGUILayout.BeginVertical("box");

            GUILayout.Space(4);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15
            };

            GUIStyle descStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true
            };

            GUIStyle miniStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                richText = true
            };

            EditorGUILayout.LabelField("模型 Smooth Normal 烘焙工具", titleStyle);
            GUILayout.Space(4);

            EditorGUILayout.LabelField(
                "将模型的 <b>Smooth Normal</b> 写入 UV2 / UV3，供外描边 Pass 使用。",
                descStyle);

            GUILayout.Space(2);

            EditorGUILayout.LabelField(
                "适用于修复硬边模型描边断裂问题。建议保留模型原始法线，仅在描边 Pass 中读取烘焙后的 Smooth Normal 进行外扩。",
                miniStyle);

            GUILayout.Space(6);

            EditorGUILayout.HelpBox(
                "推荐输出为新的 Mesh 资源，避免直接修改原始导入模型。",
                MessageType.Info);

            GUILayout.Space(2);
            EditorGUILayout.EndVertical();
        }

        private void DrawSectionHeader(string title)
        {
            GUILayout.Space(1);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.Space(6);
        }

        private void DrawKeyValueRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(value);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.BeginVertical("box");
            DrawSectionHeader("目标对象");

            targetObject = EditorGUILayout.ObjectField(
                new GUIContent("目标", "支持 Mesh、GameObject、Prefab"),
                targetObject,
                typeof(Object),
                true);

            GUILayout.Space(8);

            if (targetObject == null)
            {
                EditorGUILayout.HelpBox(
                    "请拖入 Mesh、Prefab，或场景中带 MeshFilter / SkinnedMeshRenderer 的对象。",
                    MessageType.Info);
            }
            else
            {
                Mesh previewMesh = ExtractMesh(targetObject);

                if (previewMesh == null)
                {
                    EditorGUILayout.HelpBox("当前对象中没有找到可用的 Mesh。", MessageType.Warning);
                }
                else
                {
                    DrawKeyValueRow("Mesh Name", previewMesh.name);
                    DrawKeyValueRow("Vertex Count", previewMesh.vertexCount.ToString());
                    DrawKeyValueRow("SubMesh Count", previewMesh.subMeshCount.ToString());

                    if (!previewMesh.isReadable)
                    {
                        GUILayout.Space(6);
                        EditorGUILayout.HelpBox(
                            "当前 Mesh 未开启 Read/Write Enabled，无法执行烘焙。",
                            MessageType.Warning);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawOptionSection()
        {
            EditorGUILayout.BeginVertical("box");
            DrawSectionHeader("烘焙选项");

            normalizeResult = EditorGUILayout.ToggleLeft("结果归一化", normalizeResult);

            GUILayout.Space(6);

            smoothingAngle = EditorGUILayout.Slider(
                new GUIContent("平滑角阈值", "超过该角度的法线不会被平均"),
                smoothingAngle,
                0f,
                180f);

            GUILayout.Space(8);

            EditorGUILayout.LabelField("写入通道", EditorStyles.miniBoldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                writeToUV2 = EditorGUILayout.ToggleLeft("写入 UV2", writeToUV2);
                writeToUV3 = EditorGUILayout.ToggleLeft("写入 UV3", writeToUV3);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawOutputSection()
        {
            EditorGUILayout.BeginVertical("box");
            DrawSectionHeader("输出设置");

            bakeToMeshAsset = EditorGUILayout.ToggleLeft(
                new GUIContent("生成新的 Mesh 资源", "推荐开启，避免直接修改原始资源"),
                bakeToMeshAsset);

            GUILayout.Space(6);

            using (new EditorGUI.DisabledScope(!bakeToMeshAsset))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("保存目录");
                saveFolder = EditorGUILayout.TextField(saveFolder);

                if (GUILayout.Button("选择", GUILayout.Width(62), GUILayout.Height(EditorGUIUtility.singleLineHeight + 2)))
                {
                    string folder = EditorUtility.OpenFolderPanel("选择保存目录", Application.dataPath, "");
                    if (!string.IsNullOrEmpty(folder))
                    {
                        if (folder.StartsWith(Application.dataPath))
                        {
                            saveFolder = "Assets" + folder.Substring(Application.dataPath.Length);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("无效目录", "必须保存到当前工程的 Assets 目录内。", "确定");
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(6);

            overwriteMeshReference = EditorGUILayout.ToggleLeft(
                new GUIContent("处理后自动替换当前对象的 Mesh 引用", "对场景对象 / Prefab 实例比较方便"),
                overwriteMeshReference);

            EditorGUILayout.EndVertical();
        }

        private void DrawActionSection()
        {
            EditorGUILayout.BeginVertical("box");
            DrawSectionHeader("执行");

            bool canBake = CanBake(out string blockReason);

            if (!string.IsNullOrEmpty(blockReason))
            {
                EditorGUILayout.HelpBox(blockReason, MessageType.None);
                GUILayout.Space(6);
            }

            using (new EditorGUI.DisabledScope(!canBake))
            {
                Color oldColor = GUI.color;
                GUI.color = canBake ? new Color(0.24f, 0.56f, 0.24f) : oldColor;

                if (GUILayout.Button("生成 Smooth Normal", GUILayout.Height(38)))
                {
                    Bake();
                }

                GUI.color = oldColor;
            }

            GUILayout.Space(6);

            EditorGUILayout.LabelField(
                "说明：工具会基于相同位置顶点平均法线生成 Smooth Normal，并写入选中的 UV 通道。",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Bake

        private bool CanBake(out string reason)
        {
            reason = null;

            if (targetObject == null)
            {
                reason = "请先指定目标对象。";
                return false;
            }

            if (!writeToUV2 && !writeToUV3)
            {
                reason = "请至少选择一个写入通道。";
                return false;
            }

            Mesh sourceMesh = ExtractMesh(targetObject);
            if (sourceMesh == null)
            {
                reason = "当前对象中没有可用的 Mesh。";
                return false;
            }

            if (!sourceMesh.isReadable)
            {
                reason = "当前 Mesh 未开启 Read/Write Enabled。";
                return false;
            }

            return true;
        }

        private void Bake()
        {
            Mesh sourceMesh = ExtractMesh(targetObject);
            if (sourceMesh == null)
            {
                EditorUtility.DisplayDialog("失败", "未找到可用 Mesh。", "确定");
                return;
            }

            if (!sourceMesh.isReadable)
            {
                EditorUtility.DisplayDialog(
                    "Mesh 不可读",
                    "当前 Mesh 未开启 Read/Write，请先在模型导入设置中开启 Read/Write Enabled。",
                    "确定");
                return;
            }

            try
            {
                Mesh bakedMesh = Instantiate(sourceMesh);
                bakedMesh.name = sourceMesh.name + "_SmoothNormal";

                Vector3[] smoothNormals = GenerateSmoothNormals(
                            bakedMesh.vertices,
                            bakedMesh.normals,
                            smoothingAngle,
                            normalizeResult);
                if (writeToUV2)
                {
                    List<Vector4> uv2 = new List<Vector4>(smoothNormals.Length);
                    for (int i = 0; i < smoothNormals.Length; i++)
                    {
                        Vector3 n = smoothNormals[i];
                        uv2.Add(new Vector4(n.x, n.y, n.z, 0f));
                    }
                    bakedMesh.SetUVs(1, uv2);
                }

                if (writeToUV3)
                {
                    List<Vector4> uv3 = new List<Vector4>(smoothNormals.Length);
                    for (int i = 0; i < smoothNormals.Length; i++)
                    {
                        Vector3 n = smoothNormals[i];
                        uv3.Add(new Vector4(n.x, n.y, n.z, 0f));
                    }
                    bakedMesh.SetUVs(2, uv3);
                }

                bakedMesh.RecalculateBounds();

                if (bakeToMeshAsset)
                {
                    EnsureFolderExists(saveFolder);

                    string finalPath = AssetDatabase.GenerateUniqueAssetPath(
                        $"{saveFolder}/{bakedMesh.name}.asset");

                    AssetDatabase.CreateAsset(bakedMesh, finalPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    Mesh newMesh = AssetDatabase.LoadAssetAtPath<Mesh>(finalPath);

                    if (overwriteMeshReference)
                    {
                        TryAssignMesh(targetObject, newMesh);
                    }

                    EditorUtility.DisplayDialog("完成", $"已生成新 Mesh：\n{finalPath}", "确定");
                    Selection.activeObject = newMesh;
                    EditorGUIUtility.PingObject(newMesh);
                }
                else
                {
                    if (overwriteMeshReference)
                    {
                        TryAssignMesh(targetObject, bakedMesh);
                    }

                    EditorUtility.DisplayDialog("完成", "Smooth Normal 已生成并应用到当前对象。", "确定");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("失败", "生成过程中出现错误，请查看 Console。", "确定");
            }
        }

        #endregion

        #region Mesh Helpers

        private static Mesh ExtractMesh(Object obj)
        {
            if (obj == null) return null;

            Mesh mesh = obj as Mesh;
            if (mesh != null)
                return mesh;

            GameObject go = obj as GameObject;
            if (go != null)
            {
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    return mf.sharedMesh;

                SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null)
                    return smr.sharedMesh;
            }

            return null;
        }

        private static void TryAssignMesh(Object obj, Mesh mesh)
        {
            if (obj == null || mesh == null) return;

            GameObject go = obj as GameObject;
            if (go != null)
            {
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    Undo.RecordObject(mf, "Assign Smooth Normal Mesh");
                    mf.sharedMesh = mesh;
                    EditorUtility.SetDirty(mf);
                }

                SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    Undo.RecordObject(smr, "Assign Smooth Normal Mesh");
                    smr.sharedMesh = mesh;
                    EditorUtility.SetDirty(smr);
                }

                EditorUtility.SetDirty(go);
            }
        }

        private static Vector3[] GenerateSmoothNormals(Vector3[] vertices, Vector3[] normals, float smoothingAngle, bool normalize)
        {
            Vector3[] result = new Vector3[vertices.Length];

            Dictionary<VertexKey, List<int>> groups = new Dictionary<VertexKey, List<int>>(vertices.Length);

            for (int i = 0; i < vertices.Length; i++)
            {
                VertexKey key = new VertexKey(vertices[i]);
                if (!groups.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>(4);
                    groups.Add(key, list);
                }
                list.Add(i);
            }

            float cosThreshold = Mathf.Cos(smoothingAngle * Mathf.Deg2Rad);

            foreach (var pair in groups)
            {
                List<int> indices = pair.Value;

                for (int i = 0; i < indices.Count; i++)
                {
                    int currentIndex = indices[i];
                    Vector3 baseNormal = normals[currentIndex];

                    Vector3 avg = Vector3.zero;

                    for (int j = 0; j < indices.Count; j++)
                    {
                        Vector3 compareNormal = normals[indices[j]];

                        float dot = Vector3.Dot(baseNormal.normalized, compareNormal.normalized);

                        if (dot >= cosThreshold)
                        {
                            avg += compareNormal;
                        }
                    }

                    if (avg != Vector3.zero)
                    {
                        if (normalize)
                            avg.Normalize();

                        result[currentIndex] = avg;
                    }
                    else
                    {
                        result[currentIndex] = baseNormal;
                    }
                }
            }

            return result;
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            string[] parts = assetFolderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                return;

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private struct VertexKey
        {
            private readonly long x;
            private readonly long y;
            private readonly long z;

            private const int Tolerance = 100000;

            public VertexKey(Vector3 position)
            {
                x = Quantize(position.x);
                y = Quantize(position.y);
                z = Quantize(position.z);
            }

            private static long Quantize(float value)
            {
                return Mathf.RoundToInt(value * Tolerance);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is VertexKey)) return false;
                VertexKey other = (VertexKey)obj;
                return x == other.x && y == other.y && z == other.z;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = x.GetHashCode();
                    hash = (hash * 397) ^ y.GetHashCode();
                    hash = (hash * 397) ^ z.GetHashCode();
                    return hash;
                }
            }
        }

        #endregion
    }
}

