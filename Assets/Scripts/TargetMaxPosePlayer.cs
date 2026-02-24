using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 从 Max Data 加载一个目标单帧（BODY_25 3D JSON），可选：
/// 1) 直接驱动人物模型骨骼（把骨骼引用拖到本脚本里即可；脚本不必挂模型上）
/// 2) 发布到 PoseStreamBus（用于其他显示链路）
/// 文件名：key + ".json"
/// 位置：Application.streamingAssetsPath/Max Data/
/// 坐标：以 MidHip 为原点的局部坐标（读入后自动减去 MidHip）
/// </summary>
public class TargetMaxPosePlayer : MonoBehaviour
{
    [Header("可选：推流目标（用于其他系统接入）")]
    public PoseStreamBus targetBus;

    [Header("根目录（留空=StreamingAssets/Max Data）")]
    public string baseFolder = "Max Data";

    [Header("直接驱动模型（把骨骼拖进来即可，不用把脚本挂在模型上）")]
    public bool applyToModel = true;

    [Tooltip("必须：角色 Hips（决定世界原点与模型基坐标）")]
    public Transform hips;

    [Header("左腿骨骼")]
    public Transform leftUpperLeg;
    public Transform leftLowerLeg;
    public Transform leftFoot;

    [Header("右腿骨骼")]
    public Transform rightUpperLeg;
    public Transform rightLowerLeg;
    public Transform rightFoot;

    [Header("可选：大脚趾参考（更稳定的脚前向）")]
    public Transform leftToeHint;
    public Transform rightToeHint;

    [Header("绑定/尺度/微调")]
    public bool calibrateBindPoseNow = false; // 勾一下重新校准
    public bool autoSourceScale = true;
    public float sourceScale = 1f;            // 关闭自动估计时可手调
    [Tooltip("脚踝X轴微调（度）：- 更伸，+ 更勾")]
    public float leftFootPitchOffsetDeg = 0f;
    public float rightFootPitchOffsetDeg = 0f;

    // —— 内部状态 —— //
    private readonly Dictionary<Transform, Quaternion> C_bind = new Dictionary<Transform, Quaternion>();
    private bool scaleCalibrated = false;

    // BODY_25 indices
    const int MIDHIP = 8, RHIP = 9, RKNEE = 10, RANKLE = 11, RBIGTOE = 22, LHIP = 12, LKNEE = 13, LANKLE = 14, LBIGTOE = 19;

    void OnEnable()
    {
        // 开局做一次绑定校准
        CalibrateBindPose();
    }

    void Update()
    {
        if (calibrateBindPoseNow)
        {
            calibrateBindPoseNow = false;
            CalibrateBindPose();
        }
    }

    // =============== 对外接口：按 key 加载并显示一帧 ===============
    public void ShowByKey(string key)
    {
        string root = ResolveRoot(baseFolder);
        string path = Path.Combine(root, key + ".json");
        if (!File.Exists(path))
        {
            Debug.LogError($"[TargetMaxPosePlayer] 找不到文件：{path}");
            return;
        }

        var f = LoadOneFrame(path);
        if (f == null)
        {
            Debug.LogError($"[TargetMaxPosePlayer] 解析失败：{path}");
            return;
        }

        // 1) 可选：推到总线
        if (targetBus)
        {
            if (!targetBus.isStreaming) targetBus.StartStreaming();
            targetBus.PublishFrame(f, overwriteFields: true);
        }

        // 2) 可选：直接驱动人物模型
        if (applyToModel)
        {
            if (autoSourceScale && !scaleCalibrated)
                TryCalibrateSourceScale(f);

            ApplyFrameToModel(f);
        }
    }

    // =============== 读单帧 JSON → LegPoseFrame（以 MidHip 为原点） ===============
    private LegPoseFrame LoadOneFrame(string jsonPath)
    {
        try
        {
            string json = File.ReadAllText(jsonPath);
            float[] arr = ExtractPoseKeypoints3D(json);
            if (arr == null || arr.Length < 25 * 3) return null;

            int step = arr.Length / 25; // 3 或 4
            Vector3 V(int idx)
            {
                int i = idx * step;
                return new Vector3(arr[i + 0], arr[i + 1], arr[i + 2]);
            }

            Vector3 mid = V(MIDHIP);
            return new LegPoseFrame
            {
                leftHip = V(LHIP) - mid,
                leftKnee = V(LKNEE) - mid,
                leftAnkle = V(LANKLE) - mid,
                leftToe = V(LBIGTOE) - mid,

                rightHip = V(RHIP) - mid,
                rightKnee = V(RKNEE) - mid,
                rightAnkle = V(RANKLE) - mid,
                rightToe = V(RBIGTOE) - mid
            };
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TargetMaxPosePlayer] 读帧异常：{jsonPath}\n{e}");
            return null;
        }
    }

    // =============== 绑定姿态校准（用 hips.right/hips.up 构造稳定基） ===============
    public void CalibrateBindPose()
    {
        C_bind.Clear();
        scaleCalibrated = false;

        if (!hips)
        {
            Debug.LogWarning("[TargetMaxPosePlayer] hips 未指定，无法校准绑定姿态。");
            return;
        }

        Vector3 modelRight = hips.right;
        Vector3 modelUp = hips.up;

        // 左腿
        CalibBind(leftUpperLeg, leftLowerLeg ? leftLowerLeg.position : (leftUpperLeg ? leftUpperLeg.position + leftUpperLeg.forward : Vector3.zero), modelRight, modelUp);
        CalibBind(leftLowerLeg, leftFoot ? leftFoot.position : (leftLowerLeg ? leftLowerLeg.position + leftLowerLeg.forward : Vector3.zero), modelRight, modelUp);
        CalibBindFoot(leftFoot, leftToeHint ? leftToeHint.position : (leftFoot ? leftFoot.position + leftFoot.forward : Vector3.zero), modelRight, modelUp);

        // 右腿
        CalibBind(rightUpperLeg, rightLowerLeg ? rightLowerLeg.position : (rightUpperLeg ? rightUpperLeg.position + rightUpperLeg.forward : Vector3.zero), modelRight, modelUp);
        CalibBind(rightLowerLeg, rightFoot ? rightFoot.position : (rightLowerLeg ? rightLowerLeg.position + rightLowerLeg.forward : Vector3.zero), modelRight, modelUp);
        CalibBindFoot(rightFoot, rightToeHint ? rightToeHint.position : (rightFoot ? rightFoot.position + rightFoot.forward : Vector3.zero), modelRight, modelUp);

        Debug.Log("[TargetMaxPosePlayer] 绑定姿态校准完成。");
    }

    private void CalibBind(Transform bone, Vector3 childPosWorld, Vector3 modelRight, Vector3 modelUp)
    {
        if (!bone) return;
        Vector3 fwd = childPosWorld - bone.position;
        if (fwd.sqrMagnitude < 1e-10f) return;
        fwd.Normalize();

        Vector3 lat = modelRight - Vector3.Dot(modelRight, fwd) * fwd;
        if (lat.sqrMagnitude < 1e-10f) lat = Vector3.Cross(modelUp, fwd);
        lat.Normalize();

        Vector3 up = Vector3.Cross(lat, fwd).normalized;
        Quaternion R_bind = Quaternion.LookRotation(fwd, up);
        C_bind[bone] = Quaternion.Inverse(R_bind) * bone.rotation;
    }

    private void CalibBindFoot(Transform foot, Vector3 toePosWorld, Vector3 modelRight, Vector3 modelUp)
    {
        if (!foot) return;
        Vector3 fwd = toePosWorld - foot.position;
        if (fwd.sqrMagnitude < 1e-10f) return;
        fwd.Normalize();

        Vector3 lat = modelRight - Vector3.Dot(modelRight, fwd) * fwd;
        if (lat.sqrMagnitude < 1e-10f) lat = Vector3.Cross(modelUp, fwd);
        lat.Normalize();

        Vector3 up = Vector3.Cross(lat, fwd).normalized;
        Quaternion R_bind = Quaternion.LookRotation(fwd, up);
        C_bind[foot] = Quaternion.Inverse(R_bind) * foot.rotation;
    }

    // =============== 源尺度自动匹配（基于大腿/小腿平均长度） ===============
    private void TryCalibrateSourceScale(LegPoseFrame fr)
    {
        if (!leftUpperLeg || !leftLowerLeg || !rightUpperLeg || !rightLowerLeg || !leftFoot || !rightFoot || !hips) return;

        float srcThighL = (fr.leftKnee - fr.leftHip).magnitude;
        float srcThighR = (fr.rightKnee - fr.rightHip).magnitude;
        float srcCalfL = (fr.leftAnkle - fr.leftKnee).magnitude;
        float srcCalfR = (fr.rightAnkle - fr.rightKnee).magnitude;

        float dstThighL = (leftLowerLeg.position - leftUpperLeg.position).magnitude;
        float dstThighR = (rightLowerLeg.position - rightUpperLeg.position).magnitude;
        float dstCalfL = (leftFoot.position - leftLowerLeg.position).magnitude;
        float dstCalfR = (rightFoot.position - rightLowerLeg.position).magnitude;

        float srcThigh = Mathf.Max(1e-5f, 0.5f * (srcThighL + srcThighR));
        float srcCalf = Mathf.Max(1e-5f, 0.5f * (srcCalfL + srcCalfR));
        float dstThigh = 0.5f * (dstThighL + dstThighR);
        float dstCalf = 0.5f * (dstCalfL + dstCalfR);

        float sThigh = dstThigh / srcThigh;
        float sCalf = dstCalf / srcCalf;

        sourceScale = 0.5f * (sThigh + sCalf);
        scaleCalibrated = true;
        Debug.Log($"[TargetMaxPosePlayer] 自动 sourceScale = {sourceScale:F3} (thigh {sThigh:F3}, calf {sCalf:F3})");
    }

    // =============== 应用一帧到模型 ===============
    private void ApplyFrameToModel(LegPoseFrame f)
    {
        if (!hips) { Debug.LogWarning("[TargetMaxPosePlayer] hips 未指定，无法驱动模型。"); return; }

        // 局部→世界
        Vector3 LHip = hips.TransformPoint(sourceScale * f.leftHip);
        Vector3 LKnee = hips.TransformPoint(sourceScale * f.leftKnee);
        Vector3 LAnkle = hips.TransformPoint(sourceScale * f.leftAnkle);
        Vector3 LToe = hips.TransformPoint(sourceScale * f.leftToe);

        Vector3 RHip = hips.TransformPoint(sourceScale * f.rightHip);
        Vector3 RKnee = hips.TransformPoint(sourceScale * f.rightKnee);
        Vector3 RAnkle = hips.TransformPoint(sourceScale * f.rightAnkle);
        Vector3 RToe = hips.TransformPoint(sourceScale * f.rightToe);

        Vector3 modelRight = hips.right; // +X
        Vector3 modelUp = hips.up;    // +Y

        // 左腿
        ApplySegment(leftUpperLeg, LHip, LKnee, modelRight, modelUp);
        ApplySegment(leftLowerLeg, LKnee, LAnkle, modelRight, modelUp);
        ApplyFoot(leftFoot, LAnkle, LToe, modelRight, modelUp, leftFootPitchOffsetDeg);

        // 右腿
        ApplySegment(rightUpperLeg, RHip, RKnee, modelRight, modelUp);
        ApplySegment(rightLowerLeg, RKnee, RAnkle, modelRight, modelUp);
        ApplyFoot(rightFoot, RAnkle, RToe, modelRight, modelUp, rightFootPitchOffsetDeg);
    }

    private void ApplySegment(Transform bone, Vector3 parentW, Vector3 childW, Vector3 modelRight, Vector3 modelUp)
    {
        if (!bone) return;

        Vector3 fwd = childW - parentW;
        if (fwd.sqrMagnitude < 1e-10f) return;
        fwd.Normalize();

        Vector3 lat = modelRight - Vector3.Dot(modelRight, fwd) * fwd;
        if (lat.sqrMagnitude < 1e-10f) lat = Vector3.Cross(modelUp, fwd);
        lat.Normalize();

        Vector3 up = Vector3.Cross(lat, fwd).normalized;

        Quaternion R = Quaternion.LookRotation(fwd, up);
        if (C_bind.TryGetValue(bone, out var c)) R = R * c;
        bone.rotation = R;
    }

    private void ApplyFoot(Transform foot, Vector3 ankleW, Vector3 toeW, Vector3 modelRight, Vector3 modelUp, float pitchOffsetDeg)
    {
        if (!foot) return;

        Vector3 fwd = toeW - ankleW;
        if (fwd.sqrMagnitude < 1e-10f) fwd = foot.forward;
        fwd.Normalize();

        Vector3 lat = modelRight - Vector3.Dot(modelRight, fwd) * fwd;
        if (lat.sqrMagnitude < 1e-10f) lat = Vector3.Cross(modelUp, fwd);
        lat.Normalize();

        Vector3 up = Vector3.Cross(lat, fwd).normalized;

        Quaternion R = Quaternion.LookRotation(fwd, up) * Quaternion.AngleAxis(pitchOffsetDeg, lat);
        if (C_bind.TryGetValue(foot, out var c)) R = R * c;
        foot.rotation = R;
    }

    // =============== 轻量 JSON 解析（people[0].pose_keypoints_3d） ===============
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

    // =============== 根路径解析 ===============
    private static string ResolveRoot(string userInput)
    {
        if (string.IsNullOrEmpty(userInput) || string.Equals(userInput, "Max Data", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(Application.streamingAssetsPath, "Max Data");
        if (Path.IsPathRooted(userInput)) return userInput;

        string maybe = userInput.Replace('\\', '/').ToLowerInvariant();
        if (!maybe.Contains("streamingassets"))
            return Path.Combine(Application.streamingAssetsPath, userInput);

        try { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", userInput)); }
        catch { return Path.GetFullPath(userInput); }
    }
}
