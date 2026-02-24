#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpSkeletonInfo
{
    [MenuItem("Tools/Skeleton/Dump Selected Root Skeleton...")]
    private static void DumpSelected()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogError("请选择骨骼根节点（含 hips/humanoid root）");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Save Skeleton CSV", Application.dataPath, go.name + "_skeleton.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        var sb = new StringBuilder();
        sb.AppendLine("Path,Name,Depth,LocalPos.x,LocalPos.y,LocalPos.z,LocalRot.x,LocalRot.y,LocalRot.z,LocalRot.w,LocalEuler.x,LocalEuler.y,LocalEuler.z");

        Traverse(go.transform, "", 0, (t, p, d) =>
        {
            Vector3 lp = t.localPosition;
            Quaternion lr = t.localRotation;
            Vector3 le = t.localEulerAngles;
            sb.AppendLine($"{p},{t.name},{d},{lp.x:F6},{lp.y:F6},{lp.z:F6},{lr.x:F6},{lr.y:F6},{lr.z:F6},{lr.w:F6},{le.x:F3},{le.y:F3},{le.z:F3}");
        });

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Debug.Log("骨骼信息已导出到: " + path);
    }

    private static void Traverse(Transform t, string path, int depth, System.Action<Transform, string, int> onNode)
    {
        string cur = string.IsNullOrEmpty(path) ? t.name : path + "/" + t.name;
        onNode(t, cur, depth);
        for (int i = 0; i < t.childCount; i++)
            Traverse(t.GetChild(i), cur, depth + 1, onNode);
    }
}
#endif
