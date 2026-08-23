#if ARTTOOLS_EXPERIMENTAL_TRANSFORM_RESET

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArtTools.EditorTools
{
    public sealed class TransformResetTool : EditorWindow
    {
        private const string UndoLabel = "Reset Transform (Preserve Appearance)";
        private const float ScaleTolerance = 0.0001f;
        private Vector2 scrollPosition;

        public static void ShowWindow()
        {
            TransformResetTool window = GetWindow<TransformResetTool>("Transform \u5f52\u96f6");
            window.minSize = new Vector2(500, 360);
            window.Show();
        }

        public static void Open()
        {
            ShowWindow();
        }

        private void OnGUI()
        {
            ArtToolsEditorUI.DrawHeader(
                "Transform \u5f52\u96f6",
                "\u6e05\u7a7a\u6839\u5bf9\u8c61 Transform\uff0c\u4e0d\u521b\u5efa\u65b0\u7236\u8282\u70b9\u3002",
                "d_MoveTool");

            ArtToolsEditorUI.BeginPanel("\u5de5\u4f5c\u65b9\u5f0f");
            EditorGUILayout.HelpBox(
                "\u5de5\u5177\u4f1a\u5c06\u6839\u5bf9\u8c61\u7684\u4f4d\u7f6e\u3001\u65cb\u8f6c\u4e0e\u7b49\u6bd4\u7f29\u653e\u4e0b\u63a8\u7ed9\u5176\u76f4\u5c5e\u5b50\u8282\u70b9\uff0c\u518d\u628a\u6839\u5bf9\u8c61\u8bbe\u4e3a Position (0, 0, 0)\u3001Rotation (0, 0, 0)\u3001Scale (1, 1, 1)\u3002\u4e0d\u4f1a\u65b0\u589e\u5c42\u7ea7\uff0c\u6a21\u578b\u5916\u89c2\u4fdd\u6301\u4e0d\u53d8\u3002",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "\u4ec5\u652f\u6301\u573a\u666f\u4e2d\u7684\u6a21\u578b\u6839\u8282\u70b9\u4e0e\u6b63\u5411\u7b49\u6bd4\u7f29\u653e\u3002\u975e\u7b49\u6bd4\u7f29\u653e\u3001UI\u3001\u6839\u8282\u70b9\u81ea\u8eab\u5e26\u6e32\u67d3\u5668\u6216\u78b0\u649e\u4f53\u7684\u5bf9\u8c61\u4e0d\u4f1a\u88ab\u5904\u7406\uff0c\u4ee5\u907f\u514d\u5916\u89c2\u53d8\u5f62\u3002\u652f\u6301 Ctrl/Cmd + Z \u64a4\u9500\u3002",
                MessageType.None);
            ArtToolsEditorUI.EndPanel();

            List<GameObject> targets = GetTopLevelEligibleSelection();
            ArtToolsEditorUI.BeginPanel($"\u5f85\u5904\u7406\u5bf9\u8c61\uff08{targets.Count}\uff09");
            if (targets.Count == 0)
            {
                string reason = GetFirstIneligibilityReason();
                ArtToolsEditorUI.EmptyState(reason ?? "\u8bf7\u5728\u573a\u666f\u4e2d\u9009\u62e9\u4e00\u4e2a\u6a21\u578b\u6839\u8282\u70b9\u3002");
            }
            else
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(90));
                foreach (GameObject target in targets)
                {
                    EditorGUILayout.ObjectField(target, typeof(GameObject), true);
                }

                EditorGUILayout.EndScrollView();
            }

            ArtToolsEditorUI.EndPanel();

            using (new EditorGUI.DisabledScope(targets.Count == 0))
            {
                if (ArtToolsEditorUI.PrimaryButton("\u5f52\u96f6 Transform\uff08\u4e0d\u65b0\u589e\u5c42\u7ea7\uff09"))
                {
                    ResetTransforms(targets);
                }
            }
        }

        private static List<GameObject> GetTopLevelEligibleSelection()
        {
            HashSet<GameObject> selected = new HashSet<GameObject>(Selection.gameObjects.Where(IsEligible));
            return selected
                .Where(target => !selected.Any(other => other != target && target.transform.IsChildOf(other.transform)))
                .OrderBy(target => target.transform.GetSiblingIndex())
                .ToList();
        }

        private static bool IsEligible(GameObject target)
        {
            return GetIneligibilityReason(target) == null;
        }

        private static string GetFirstIneligibilityReason()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null ? null : GetIneligibilityReason(selected);
        }

        private static string GetIneligibilityReason(GameObject target)
        {
            if (target == null || !target.scene.IsValid() || EditorUtility.IsPersistent(target))
            {
                return "\u8bf7\u9009\u62e9\u573a\u666f\u4e2d\u7684\u5bf9\u8c61\uff0c\u800c\u4e0d\u662f Project \u8d44\u6e90\u3002";
            }

            if (target.transform is RectTransform)
            {
                return "UI \u5bf9\u8c61\u7684 RectTransform \u4e0d\u652f\u6301\u6b64\u65b9\u5f0f\u3002";
            }

            if (CanBakeStaticMesh(target))
            {
                return null;
            }

            if (CanApplyImportedModelScale(target))
            {
                return null;
            }

            if (target.transform.childCount == 0)
            {
                return "\u6240\u9009\u5bf9\u8c61\u6ca1\u6709\u5b50\u8282\u70b9\uff0c\u65e0\u6cd5\u5728\u4e0d\u4fee\u6539\u7f51\u683c\u7684\u60c5\u51b5\u4e0b\u4fdd\u6301\u5916\u89c2\u3002";
            }

            if (target.GetComponent<Renderer>() != null || target.GetComponent<Collider>() != null)
            {
                return "\u6839\u8282\u70b9\u81ea\u8eab\u5e26\u6709\u6e32\u67d3\u5668\u6216\u78b0\u649e\u4f53\uff0c\u9700\u8981\u7f51\u683c\u70d8\u7119\u624d\u80fd\u4fdd\u8bc1\u5916\u89c2\u4e0d\u53d8\u3002";
            }

            Vector3 scale = target.transform.localScale;
            if (scale.x <= 0f || Mathf.Abs(scale.x - scale.y) > ScaleTolerance || Mathf.Abs(scale.x - scale.z) > ScaleTolerance)
            {
                return "\u4ec5\u652f\u6301\u6b63\u5411\u7b49\u6bd4\u7f29\u653e\uff08X\u3001Y\u3001Z \u76f8\u7b49\u4e14\u5927\u4e8e 0\uff09\uff0c\u4ee5\u907f\u514d\u975e\u7b49\u6bd4\u7f29\u653e\u5bfc\u81f4\u526a\u5207\u53d8\u5f62\u3002";
            }

            for (int index = 0; index < target.transform.childCount; index++)
            {
                if (target.transform.GetChild(index) is RectTransform)
                {
                    return "\u6839\u8282\u70b9\u5305\u542b UI \u5b50\u8282\u70b9\uff0c\u4e0d\u652f\u6301\u6b64\u65b9\u5f0f\u3002";
                }
            }

            return null;
        }

        private static bool CanBakeStaticMesh(GameObject target)
        {
            if (target == null || target.transform.childCount != 0 || target.GetComponent<SkinnedMeshRenderer>() != null)
            {
                return false;
            }

            MeshFilter meshFilter = target.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh == null || mesh.blendShapeCount > 0)
            {
                return false;
            }

            return target.GetComponents<Collider>().All(collider => collider is MeshCollider);
        }

        private static bool CanApplyImportedModelScale(GameObject target)
        {
            if (target == null || target.transform.childCount != 0)
            {
                return false;
            }

            Transform transform = target.transform;
            Vector3 scale = transform.localScale;
            bool hasUniformPositiveScale = scale.x > 0f
                && Mathf.Abs(scale.x - scale.y) <= ScaleTolerance
                && Mathf.Abs(scale.x - scale.z) <= ScaleTolerance;
            if (!hasUniformPositiveScale || transform.localPosition.sqrMagnitude > ScaleTolerance * ScaleTolerance
                || Quaternion.Angle(transform.localRotation, Quaternion.identity) > ScaleTolerance)
            {
                return false;
            }

            string assetPath = ArtToolsUnityCompatibility.GetNearestPrefabAssetPath(target);
            return AssetImporter.GetAtPath(assetPath) is ModelImporter;
        }

        private static void ResetTransforms(IList<GameObject> targets)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoLabel);

            foreach (GameObject target in targets)
            {
                Transform targetTransform = target.transform;
                if (CanBakeStaticMesh(target))
                {
                    BakeStaticMeshTransform(targetTransform);
                    continue;
                }

                if (CanApplyImportedModelScale(target))
                {
                    ApplyImportedModelScale(targetTransform);
                    continue;
                }

                Vector3 originalPosition = targetTransform.localPosition;
                Quaternion originalRotation = targetTransform.localRotation;
                float scaleFactor = targetTransform.localScale.x;

                Undo.RecordObject(targetTransform, UndoLabel);
                for (int index = 0; index < targetTransform.childCount; index++)
                {
                    Transform child = targetTransform.GetChild(index);
                    Undo.RecordObject(child, UndoLabel);
                    child.localPosition = originalPosition + originalRotation * (child.localPosition * scaleFactor);
                    child.localRotation = originalRotation * child.localRotation;
                    child.localScale *= scaleFactor;
                }

                targetTransform.localPosition = Vector3.zero;
                targetTransform.localRotation = Quaternion.identity;
                targetTransform.localScale = Vector3.one;
                EditorSceneManager.MarkSceneDirty(target.scene);
            }

            Undo.CollapseUndoOperations(undoGroup);
            Selection.objects = targets.Cast<Object>().ToArray();
        }

        private static void BakeStaticMeshTransform(Transform targetTransform)
        {
            MeshFilter meshFilter = targetTransform.GetComponent<MeshFilter>();
            Mesh sourceMesh = meshFilter.sharedMesh;
            if (!EnsureMeshIsReadable(sourceMesh))
            {
                EditorUtility.DisplayDialog(
                    "Transform \u5f52\u96f6",
                    "\u65e0\u6cd5\u8bfb\u53d6\u6b64\u7f51\u683c\u3002\u8bf7\u5728\u6a21\u578b\u5bfc\u5165\u8bbe\u7f6e\u4e2d\u5f00\u542f Read/Write Enabled \u540e\u91cd\u8bd5\u3002",
                    "\u786e\u5b9a");
                return;
            }

            sourceMesh = meshFilter.sharedMesh;
            if (sourceMesh == null || !sourceMesh.isReadable)
            {
                return;
            }

            Matrix4x4 transformMatrix = Matrix4x4.TRS(
                targetTransform.localPosition,
                targetTransform.localRotation,
                targetTransform.localScale);
            Mesh bakedMesh = Instantiate(sourceMesh);
            bakedMesh.name = $"{sourceMesh.name}_TransformBaked";

            Vector3[] vertices = bakedMesh.vertices;
            for (int index = 0; index < vertices.Length; index++)
            {
                vertices[index] = transformMatrix.MultiplyPoint3x4(vertices[index]);
            }

            bakedMesh.vertices = vertices;
            BakeNormalsAndTangents(bakedMesh, transformMatrix);
            bakedMesh.RecalculateBounds();

            string assetPath = CreateBakedMeshAsset(bakedMesh, targetTransform.name);
            Undo.RegisterCreatedObjectUndo(bakedMesh, UndoLabel);
            Undo.RecordObject(meshFilter, UndoLabel);
            meshFilter.sharedMesh = bakedMesh;

            foreach (MeshCollider meshCollider in targetTransform.GetComponents<MeshCollider>())
            {
                Undo.RecordObject(meshCollider, UndoLabel);
                meshCollider.sharedMesh = bakedMesh;
            }

            Undo.RecordObject(targetTransform, UndoLabel);
            targetTransform.localPosition = Vector3.zero;
            targetTransform.localRotation = Quaternion.identity;
            targetTransform.localScale = Vector3.one;
            EditorSceneManager.MarkSceneDirty(targetTransform.gameObject.scene);
            Debug.Log($"Transform baked into mesh: {assetPath}", targetTransform.gameObject);
        }

        private static bool EnsureMeshIsReadable(Mesh mesh)
        {
            if (mesh != null && mesh.isReadable)
            {
                return true;
            }

            string assetPath = AssetDatabase.GetAssetPath(mesh);
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            importer.isReadable = true;
            importer.SaveAndReimport();
            return mesh.isReadable;
        }

        private static void BakeNormalsAndTangents(Mesh mesh, Matrix4x4 transformMatrix)
        {
            Matrix4x4 normalMatrix = transformMatrix.inverse.transpose;
            Vector3[] normals = mesh.normals;
            if (normals != null && normals.Length > 0)
            {
                for (int index = 0; index < normals.Length; index++)
                {
                    normals[index] = normalMatrix.MultiplyVector(normals[index]).normalized;
                }

                mesh.normals = normals;
            }

            Vector4[] tangents = mesh.tangents;
            if (tangents != null && tangents.Length > 0)
            {
                for (int index = 0; index < tangents.Length; index++)
                {
                    Vector3 tangent = transformMatrix.MultiplyVector(new Vector3(tangents[index].x, tangents[index].y, tangents[index].z)).normalized;
                    tangents[index] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[index].w);
                }

                mesh.tangents = tangents;
            }
        }

        private static string CreateBakedMeshAsset(Mesh mesh, string objectName)
        {
            const string folderPath = "Assets/ArtTools/GeneratedMeshes";
            if (!AssetDatabase.IsValidFolder("Assets/ArtTools"))
            {
                AssetDatabase.CreateFolder("Assets", "ArtTools");
            }

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/ArtTools", "GeneratedMeshes");
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{objectName}_TransformBaked.asset");
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        private static void ApplyImportedModelScale(Transform targetTransform)
        {
            string assetPath = ArtToolsUnityCompatibility.GetNearestPrefabAssetPath(targetTransform.gameObject);
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            float scaleFactor = targetTransform.localScale.x;
            Undo.RegisterCompleteObjectUndo(importer, UndoLabel);
            Undo.RecordObject(targetTransform, UndoLabel);
            importer.globalScale *= scaleFactor;
            targetTransform.localScale = Vector3.one;
            importer.SaveAndReimport();
            EditorSceneManager.MarkSceneDirty(targetTransform.gameObject.scene);
        }
    }
}

#endif
