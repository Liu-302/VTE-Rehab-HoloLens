using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// 从 All Data/<动作名> 播放 *_keypoints.json 帧序列，推到 PoseStreamBus。
/// 假设 JSON 已经是「以 MidHip 为原点的局部坐标」，不再做任何原点/平移处理。
/// </summary>
public class CollectGuideFromFiles_AllData : MonoBehaviour
{
    [Header("Data Source")]
    [Tooltip("All Data 根目录：留空或填 \"All Data\" 时，自动指向 StreamingAssets/All Data")]
    public string baseFolder = "All Data";

    [Header("Target Bus")]
    public PoseStreamBus bus;   // 建议接到 sourceBus（供折线订阅）

    [Header("Playback")]
    [Tooltip("播放帧率（建议与数据一致 4fps）")]
    public float fps = 4f;

    [Tooltip("循环播放直到 StopPlayback()")]
    public bool loop = true;

    [Tooltip("播放开始时自动 StartStreaming，停止时自动 StopStreaming")]
    public bool autoStartStreaming = true;

    private Coroutine playing;
    private WaitForSeconds wait;

    // BODY_25 indices（我们只用 9 个点）
    const int MIDHIP = 8; // 仅保留索引，不做平移
    const int RHIP = 9, RKNEE = 10, RANKLE = 11, RBIGTOE = 22;
    const int LHIP = 12, LKNEE = 13, LANKLE = 14, LBIGTOE = 19;

    // ------ 外部按钮/入口 ------
    public void PlayFolder(string actionFolderName)
    {
        if (bus == null)
        {
            Debug.LogWarning("[AllData] PoseStreamBus 未绑定。");
            return;
        }

        string root = ResolveRoot(baseFolder);
        string folder = Path.Combine(root, actionFolderName);

        if (!Directory.Exists(folder))
        {
            Debug.LogError($"[AllData] 目录不存在：{folder}");
            return;
        }

        var frames = ListFrameFiles(folder);
        if (frames.Count == 0)
        {
            Debug.LogError($"[AllData] 未在 {folder} 找到 *_keypoints.json");
            return;
        }

        if (playing != null) StopCoroutine(playing);
        wait = new WaitForSeconds(1f / Mathf.Max(1f, fps));

        if (autoStartStreaming && !bus.isStreaming)
            bus.StartStreaming();

        playing = StartCoroutine(PlayRoutine(frames, actionFolderName));
        Debug.Log($"[AllData] 播放动作：{actionFolderName}，帧数={frames.Count}");
    }

    public void StopPlayback()
    {
        if (playing != null)
        {
            StopCoroutine(playing);
            playing = null;
            Debug.Log("[AllData] 停止播放");
        }
        if (autoStartStreaming && bus != null && bus.isStreaming)
            bus.StopStreaming();
    }

    // ------ 播放协程 ------
    private IEnumerator PlayRoutine(List<string> frameFiles, string title)
    {
        do
        {
            for (int i = 0; i < frameFiles.Count; i++)
            {
                if (!PublishFrame(frameFiles[i]))
                    Debug.LogWarning($"[AllData] 解析失败，跳过：{frameFiles[i]}");
                yield return wait;
            }
        } while (loop);

        playing = null;
        if (autoStartStreaming && bus != null && bus.isStreaming)
            bus.StopStreaming();
    }

    // ------ 读取一帧 → 发布到 Bus ------
    private bool PublishFrame(string jsonPath)
    {
        try
        {
            string json = File.ReadAllText(jsonPath);
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

            bus.PublishFrame(f, overwriteFields: true);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AllData] 读帧异常：{jsonPath}\n{e}");
            return false;
        }
    }

    // ------ 工具：列出并排序帧文件 ------
    private static List<string> ListFrameFiles(string folder)
    {
        var paths = Directory.GetFiles(folder, "*_keypoints.json", SearchOption.TopDirectoryOnly);
        var list = new List<string>(paths);
        list.Sort(NaturalCompare);
        return list;
    }

    private static int NaturalCompare(string a, string b)
    {
        string fa = Path.GetFileName(a), fb = Path.GetFileName(b);
        long na = ExtractFirstNumber(fa);
        long nb = ExtractFirstNumber(fb);
        if (na != long.MinValue && nb != long.MinValue && na != nb)
            return na < nb ? -1 : 1;
        return string.Compare(fa, fb, StringComparison.OrdinalIgnoreCase);
    }

    private static long ExtractFirstNumber(string s)
    {
        var m = Regex.Match(s, @"(\d{6,}|\d+)");
        if (m.Success && long.TryParse(m.Groups[1].Value, out long v)) return v;
        return long.MinValue;
    }

    // ------ 工具：解析 people[0].pose_keypoints_3d ------
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
            if (float.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                vals.Add(v);
        return vals.ToArray();
    }

    // ------ 根路径解析 ------
    private static string ResolveRoot(string userInput)
    {
        if (string.IsNullOrEmpty(userInput) || string.Equals(userInput, "All Data", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(Application.streamingAssetsPath, "All Data");
        if (Path.IsPathRooted(userInput)) return userInput;

        string maybe = userInput.Replace('\\', '/');
        if (!maybe.ToLowerInvariant().Contains("streamingassets"))
            return Path.Combine(Application.streamingAssetsPath, userInput);

        try { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", userInput)); }
        catch { return Path.GetFullPath(userInput); }
    }
}
