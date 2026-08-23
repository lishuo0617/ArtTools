#if false
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace ArtTools.EditorTools
{

public static class RemoveMissingScriptsTool
{
    [MenuItem("GameObject/鍒犻櫎閫変腑鐗╀綋鑴氭湰", false, 49)]
    private static void RemoveMissingScriptsFromSelected()
    {
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("鎻愮ず", "璇峰厛鍦?Hierarchy 涓€夋嫨鐗╀綋銆?, "纭畾");
            return;
        }

        int totalCheckedCount = 0;
        int totalRemovedCount = 0;

        HashSet<GameObject> allTargets = new HashSet<GameObject>();

        foreach (GameObject root in selectedObjects)
        {
            if (root == null) continue;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in children)
            {
                if (t != null && t.gameObject != null)
                {
                    allTargets.Add(t.gameObject);
                }
            }
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("鍒犻櫎閫変腑鐗╀綋鑴氭湰");

        foreach (GameObject go in allTargets)
        {
            totalCheckedCount++;
            totalRemovedCount += RemoveMissingScripts(go);
        }

        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

        EditorUtility.DisplayDialog(
            "鍒犻櫎瀹屾垚",
            string.Format("澶勭悊瀹屾垚锛乗n\n鍏辨鏌ョ墿浣擄細{0} 涓猏n鍏卞垹闄や涪澶辫剼鏈細{1} 涓?, totalCheckedCount, totalRemovedCount),
            "纭畾"
        );

        Debug.Log(string.Format("鍒犻櫎閫変腑鐗╀綋鑴氭湰瀹屾垚锛氬叡妫€鏌?{0} 涓墿浣擄紝鍒犻櫎 {1} 涓涪澶辫剼鏈€?, totalCheckedCount, totalRemovedCount));
    }

    [MenuItem("GameObject/鍒犻櫎閫変腑鐗╀綋鑴氭湰", true)]
    private static bool ValidateRemoveMissingScriptsFromSelected()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }

    private static int RemoveMissingScripts(GameObject go)
    {
        if (go == null) return 0;

        int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);

        if (missingCount > 0)
        {
            Undo.RegisterCompleteObjectUndo(go, "鍒犻櫎涓㈠け鑴氭湰");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            EditorUtility.SetDirty(go);
        }

        return missingCount;
    }
}
}


#endif

