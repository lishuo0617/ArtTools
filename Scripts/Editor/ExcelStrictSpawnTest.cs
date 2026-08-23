#if false
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ArtTools.EditorTools
{

public class ExcelStrictSpawnTest : EditorWindow
{
    [SerializeField] private string excelText = "";
    [SerializeField] private GameObject targetObject;
    [SerializeField] private Transform parentRoot;
    [SerializeField] private Vector3 startPosition = Vector3.zero;
    [SerializeField] private float cellSize = 5f;

    private Vector2 scroll;

    public static void Open()
    {
        GetWindow<ExcelStrictSpawnTest>("Excel涓ユ牸瑙ｆ瀽鐢熸垚");
    }

    private void OnGUI()
    {
        GUILayout.Label("Excel 涓ユ牸瑙ｆ瀽 + 鐢熸垚娴嬭瘯", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        targetObject = (GameObject)EditorGUILayout.ObjectField("瑕佺敓鎴愮殑瀵硅薄", targetObject, typeof(GameObject), false);
        parentRoot = (Transform)EditorGUILayout.ObjectField("鐖惰妭鐐癸紙鍙┖锛?, parentRoot, typeof(Transform), true);
        startPosition = EditorGUILayout.Vector3Field("璧峰浣嶇疆", startPosition);
        cellSize = EditorGUILayout.FloatField("鍥哄畾闂磋窛", cellSize);

        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(220));
        excelText = EditorGUILayout.TextArea(excelText, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("瑙ｆ瀽骞舵墦鍗?, GUILayout.Height(40)))
        {
            PrintParsedTable();
        }

        if (GUILayout.Button("鐢熸垚瀵硅薄", GUILayout.Height(40)))
        {
            GenerateObjects();
        }

        EditorGUILayout.EndHorizontal();
    }

    private List<List<string>> ParseExcelText(string input)
    {
        List<List<string>> table = new List<List<string>>();
        List<string> currentRow = new List<string>();
        System.Text.StringBuilder currentCell = new System.Text.StringBuilder();

        input = input.Replace("\r\n", "\n").Replace("\r", "\n");

        bool inQuotes = false;

        for (int i = 0; i < input.Length; i++)
        {
            char ch = input[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < input.Length && input[i + 1] == '"')
                {
                    currentCell.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == '\t' && !inQuotes)
            {
                currentRow.Add(currentCell.ToString());
                currentCell.Length = 0;
            }
            else if (ch == '\n' && !inQuotes)
            {
                currentRow.Add(currentCell.ToString());
                currentCell.Length = 0;

                table.Add(currentRow);
                currentRow = new List<string>();
            }
            else
            {
                currentCell.Append(ch);
            }
        }

        currentRow.Add(currentCell.ToString());
        table.Add(currentRow);

        while (table.Count > 0)
        {
            bool allEmpty = true;
            for (int i = 0; i < table[table.Count - 1].Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(table[table.Count - 1][i]))
                {
                    allEmpty = false;
                    break;
                }
            }

            if (allEmpty)
                table.RemoveAt(table.Count - 1);
            else
                break;
        }

        return table;
    }

    private void PrintParsedTable()
    {
        if (string.IsNullOrWhiteSpace(excelText))
        {
            Debug.LogError("娌℃湁绮樿创鍐呭锛?);
            return;
        }

        var table = ParseExcelText(excelText);

        Debug.Log($"====== 涓ユ牸瑙ｆ瀽缁撴灉锛氬叡 {table.Count} 琛?======");

        for (int r = 0; r < table.Count; r++)
        {
            Debug.Log($"--- 绗?{r} 琛岋紝鍒楁暟锛歿table[r].Count} ---");

            for (int c = 0; c < table[r].Count; c++)
            {
                string cell = table[r][c];
                string visible = cell.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                Debug.Log($"[{r},{c}] = \"{visible}\"");
            }
        }

        Debug.Log("====== 瑙ｆ瀽瀹屾垚 ======");
    }

    /// <summary>
    
    /// </summary>
    private string CleanCellText(string cellValue)
    {
        if (string.IsNullOrEmpty(cellValue))
            return string.Empty;

        return cellValue
            .Replace("\n", "")
            .Replace("\r", "")
            .Replace("\t", "")
            .Trim()
            .ToUpper();
    }

    /// <summary>
    
    
    /// "B1 COIN=15" => "B"
    
    /// "C1+COIN=20" => "C"
    /// </summary>
    private string GetAreaNameFromCell(string cellValue)
    {
        string cleaned = CleanCellText(cellValue);

        if (string.IsNullOrEmpty(cleaned))
            return string.Empty;

        for (int i = 0; i < cleaned.Length; i++)
        {
            char ch = cleaned[i];
            if (ch >= 'A' && ch <= 'Z')
            {
                return ch.ToString();
            }
        }

        return string.Empty;
    }

    private Transform GetOrCreateAreaParent(Transform root, string areaName, Dictionary<string, Transform> cachedParents)
    {
        if (string.IsNullOrWhiteSpace(areaName))
            return root;

        areaName = areaName.Trim().ToUpper();

        if (cachedParents.TryGetValue(areaName, out Transform cached) && cached != null)
            return cached;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.ToUpper() == areaName)
            {
                cachedParents[areaName] = child;
                return child;
            }
        }

        GameObject areaGo = new GameObject(areaName);
        Undo.RegisterCreatedObjectUndo(areaGo, "Create Area Parent");
        areaGo.transform.SetParent(root);
        areaGo.transform.localPosition = Vector3.zero;
        areaGo.transform.localRotation = Quaternion.identity;
        areaGo.transform.localScale = Vector3.one;

        cachedParents[areaName] = areaGo.transform;
        return areaGo.transform;
    }

    private void GenerateObjects()
    {
        if (targetObject == null)
        {
            Debug.LogError("璇峰厛鎷栧叆瑕佺敓鎴愮殑瀵硅薄锛?);
            return;
        }

        if (string.IsNullOrWhiteSpace(excelText))
        {
            Debug.LogError("璇峰厛绮樿创 Excel 鍐呭锛?);
            return;
        }

        var table = ParseExcelText(excelText);

        Transform root = parentRoot;
        if (root == null)
        {
            GameObject rootGo = new GameObject("ExcelSpawnRoot");
            Undo.RegisterCreatedObjectUndo(rootGo, "Create Excel Spawn Root");
            root = rootGo.transform;
        }

        int count = 0;
        Dictionary<string, Transform> areaParentCache = new Dictionary<string, Transform>();

        for (int r = 0; r < table.Count; r++)
        {
            for (int c = 0; c < table[r].Count; c++)
            {
                string cell = table[r][c];

                if (string.IsNullOrEmpty(cell) || cell.Trim().Length == 0)
                    continue;

                string areaName = GetAreaNameFromCell(cell);
                Transform areaParent = GetOrCreateAreaParent(root, areaName, areaParentCache);

                GameObject go;
                if (ArtToolsUnityCompatibility.IsPrefabAsset(targetObject))
                    go = ArtToolsUnityCompatibility.InstantiatePrefab(targetObject, null);
                else
                    go = Instantiate(targetObject);

                if (go == null)
                    continue;

                Undo.RegisterCreatedObjectUndo(go, "Generate Excel Objects");

                go.transform.SetParent(areaParent);
                go.transform.position = new Vector3(
                    startPosition.x + c * cellSize,
                    startPosition.y,
                    startPosition.z - r * cellSize
                );

                go.name = $"Cell_{r}_{c}_{targetObject.name}";
                count++;
            }
        }

        Debug.Log($"鐢熸垚瀹屾垚锛屽叡鐢熸垚 {count} 涓璞★紝骞跺凡鎸夐瀛楁瘝 A~Z 鍒嗙粍銆?);
    }
}
}


#endif

