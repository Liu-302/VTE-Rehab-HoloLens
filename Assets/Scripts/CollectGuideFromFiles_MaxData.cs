using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 从 Max Data 读取单帧 BODY_25 3D（key.json），并可选推到 PoseStreamBus。
/// 假设 JSON 已经是「以 MidHip 为原点的局部坐标」，不做任何原点/平移处理。
/// 常见用法：
/// - 若你只想让模型由 TargetMaxPosePlayer 驱动，此脚本可不推流（targetBus 留空）；
/// - 若你还想画一条“target 折线”，则把 targetBus 指到 TargetBus，并调用 ShowByKey 推流。
/// </summary>
public class CollectGuideFromFiles_MaxData : MonoBehaviour
{
    [Header("Data Source")]
    [Tooltip("Max Data 根目录：留空或填 \"Max Data\" 时，自动指向 StreamingAssets/Max Data")]
    public string baseFolder = "Max Data";

    [Header("Optional Bus (for Target Lines)")]
    public PoseStreamBus targetBus;   // 可留空；若指定，则 ShowByKey 会把帧发布到该总线

    [Header("Auto Streaming")]
    [Tooltip("ShowByKey 时若未开始推流，则自动 StartStreaming；停止请手调 StopStreaming 或不用此 bus")]
    public bool autoStartStreaming = true;

    // BODY_25 indices（我们只用 9 个点）
    const int MIDHIP = 8; // 仅保留索引，不做平移
    const int RHIP = 9, RKNEE = 10, RANKLE = 11, RBIGTOE = 22;
    const int LHIP = 12, LKNEE = 13, LANKLE = 14, LBIGTOE = 19;

    /// <summary>按 key 加载单帧（key.json）。若绑定了 targetBus，则发布到 bus。</summary>
    public bool ShowByKey(string key)
    {
        try
        {
            string root = ResolveRoot(baseFolder);
            string path = Path.Combine(root, key + ".json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[MaxData] 文件不存在：{path}");
                return false;
            }

            string json = File.ReadAllText(path);
            var arr = ExtractPoseKeypoints3D(json);
            if (arr == null || arr.Length < 25 * 3) return false;

            int step = arr.Length / 25; // 3 或 4
            if (step != 3 && step != 4) return false;

            Vector3 V(int idx)
            {
                int i = idx * step;
                return new Vector3(arr[i + 0], arr[i + 1], arr[i + 2]);
            }

            // ⬇️ 不再减 MidHip；假设数据已经以 MidHip 为原点
            LegPoseFrame f = new LegPoseFrame
            {
                leftHip = V(LHIP),
                leftKnee = V(LKNEE),
                leftAnkle = V(LANKLE),
                leftToe = V(LBIGTOE),

                rightHip = V(RHIP),
                rightKnee = V(RKNEE),
                rightAnkle = V(RANKLE),
                rightToe = V(RBIGTOE),
            };

            if (targetBus)
            {
                if (autoStartStreaming && !targetBus.isStreaming)
                    targetBus.StartStreaming();
                targetBus.PublishFrame(f, overwriteFields: true);
            }

            Debug.Log($"[MaxData] 已加载：{key}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MaxData] 读帧异常（key={key}）：\n{e}");
            return false;
        }
    }

    // --------- 工具：解析 people[0].pose_keypoints_3d ----------
    private static float[] ExtractPoseKeypoints3D(string json)
    {
        int k = json.IndexOf("\"pose_keypoints_3d\"", StringComparison.Ordinal);
        if (k < 0) return null;
        int lb = json.IndexOf('[', k);
        if (lb < 0) return null;

        int depth = 0, rb = -1;
        for (int i = lb; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '[') depth++;
            else if (c == ']')
            {
                depth--;
                if (depth == 0) { rb = i; break; }
            }
        }
        if (rb < 0) return null;

        string slice = json.Substring(lb + 1, rb - lb - 1);
        var parts = slice.Split(new[] { ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var vals = new List<float>(parts.Length);
        foreach (var p in parts)
            if (float.TryParse(p, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v))
                vals.Add(v);
        return vals.ToArray();
    }

    // --------- 根路径解析 ----------
    private static string ResolveRoot(string userInput)
    {
        if (string.IsNullOrEmpty(userInput) || string.Equals(userInput, "Max Data", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(Application.streamingAssetsPath, "Max Data");
        if (Path.IsPathRooted(userInput)) return userInput;

        string maybe = userInput.Replace('\\', '/');
        if (!maybe.ToLowerInvariant().Contains("streamingassets"))
            return Path.Combine(Application.streamingAssetsPath, userInput);

        try { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", userInput)); }
        catch { return Path.GetFullPath(userInput); }
    }
}
