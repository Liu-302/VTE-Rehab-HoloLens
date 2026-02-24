using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 从 PoseStreamBus 接收 9 点（以 MidHip 为原点的局部坐标），驱动角色模型的腿部骨骼。
/// - 模型假设 T-Pose，面朝 +Z（Unity 默认）
/// - 稳定基：用 hips.right/hips.up 构造正交基（避免翻转）
/// - 绑定姿态校准：CalibrateBindPose()
/// - 源尺度自动估计（可关）
/// - 脚踝俯仰微调（度数）
/// - 可选位置/旋转平滑
/// </summary>
public class LegModelDriver : MonoBehaviour
{
    [Header("数据源（PoseStreamBus）")]
    public PoseStreamBus bus;

    [Header("必须：骨骼")]
    public Transform hips;

    [Header("左腿骨骼")]
    public Transform leftUpperLeg;
    public Transform leftLowerLeg;
    public Transform leftFoot;

    [Header("右腿骨骼")]
    public Transform rightUpperLeg;
    public Transform rightLowerLeg;
    public Transform rightFoot;

    [Header("可选：大脚趾参考（仅用于绑定校准稳定前向）")]
    public Transform leftToeHint;
    public Transform rightToeHint;

    [Header("运行选项")]
    public bool autoSourceScale = true;
    [Tooltip("若关闭自动估计，可手动调节")]
    public float sourceScale = 1f;
    public bool startRenderingOnEnable = true;

    [Header("脚踝X轴微调（度）")]
    public float leftFootPitchOffsetDeg = 0f;   // 左脚：- 更伸，+ 更勾
    public float rightFootPitchOffsetDeg = 0f;  // 右脚：- 更伸，+ 更勾

    [Header("平滑(0无-1强)")]
    [Range(0f, 1f)] public float posSmoothing = 0.2f;
    [Range(0f, 1f)] public float rotSmoothing = 0.2f;

    [Header("调试")]
    public bool calibrateBindPoseNow = false;
    public bool gizmoDrawChains = false;

    // —— 内部状态 —— //
    private bool isRendering = false;
    private bool scaleCalibrated = false;

    // 绑定姿态修正：bone.rotation ≈ R_pose * C_bind[bone]
    private readonly Dictionary<Transform, Quaternion> C_bind = new Dictionary<Transform, Quaternion>();

    // 上一帧的世界位置（用于平滑）
    private struct WPos
    {
        public Vector3 LHip, LKnee, LAnkle, LToe;
        public Vector3 RHip, RKnee, RAnkle, RToe;
    }
    private WPos wPrev;
    private bool hasPrev = false;

    void OnEnable()
    {
        if (bus == null) bus = FindObjectOfType<PoseStreamBus>();
        if (bus != null) bus.OnPoseDataUpdated += OnPoseDataUpdated;

        CalibrateBindPose();

        if (startRenderingOnEnable) StartRendering();
    }

    void OnDisable()
    {
        if (bus != null) bus.OnPoseDataUpdated -= OnPoseDataUpdated;
        StopRendering();
    }

    void Update()
    {
        if (calibrateBindPoseNow)
        {
            calibrateBindPoseNow = false;
            CalibrateBindPose();
        }
    }

    // ================= 绑定姿态校准 =================
    public void CalibrateBindPose()
    {
        C_bind.Clear();
        if (!hips) { Debug.LogWarning("[LegModelDriver] hips 未指定"); return; }

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

        hasPrev = false;
        scaleCalibrated = false;
        Debug.Log("[LegModelDriver] 绑定姿态校准完成");
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

    // ================= 渲染开/关 =================
    public void StartRendering()
    {
        isRendering = true;
        Debug.Log("[LegModelDriver] 开始渲染");
    }

    public void StopRendering()
    {
        isRendering = false;
        Debug.Log("[LegModelDriver] 停止渲染");
    }

    // ================= 帧回调 =================
    private void OnPoseDataUpdated(LegPoseFrame f)
    {
        if (!isRendering || !hips) return;

        // 自动估计源尺度（估一次即可）
        if (autoSourceScale && !scaleCalibrated)
        {
            TryCalibrateSourceScale(f);
        }

        // 局部->世界（以 hips 为原点）
        WPos w = ToWorld(f);

        // 平滑
        if (hasPrev)
        {
            float a = Mathf.Clamp01(posSmoothing);
            w.LHip = Vector3.Lerp(wPrev.LHip, w.LHip, 1f - a);
            w.LKnee = Vector3.Lerp(wPrev.LKnee, w.LKnee, 1f - a);
            w.LAnkle = Vector3.Lerp(wPrev.LAnkle, w.LAnkle, 1f - a);
            w.LToe = Vector3.Lerp(wPrev.LToe, w.LToe, 1f - a);

            w.RHip = Vector3.Lerp(wPrev.RHip, w.RHip, 1f - a);
            w.RKnee = Vector3.Lerp(wPrev.RKnee, w.RKnee, 1f - a);
            w.RAnkle = Vector3.Lerp(wPrev.RAnkle, w.RAnkle, 1f - a);
            w.RToe = Vector3.Lerp(wPrev.RToe, w.RToe, 1f - a);
        }
        wPrev = w; hasPrev = true;

        // 固定 Hip 基向量
        Vector3 modelRight = hips.right;
        Vector3 modelUp = hips.up;

        // 左腿
        ApplySegment(leftUpperLeg, w.LHip, w.LKnee, modelRight, modelUp);
        ApplySegment(leftLowerLeg, w.LKnee, w.LAnkle, modelRight, modelUp);
        ApplyFoot(leftFoot, w.LAnkle, w.LToe, modelRight, modelUp, leftFootPitchOffsetDeg);

        // 右腿
        ApplySegment(rightUpperLeg, w.RHip, w.RKnee, modelRight, modelUp);
        ApplySegment(rightLowerLeg, w.RKnee, w.RAnkle, modelRight, modelUp);
        ApplyFoot(rightFoot, w.RAnkle, w.RToe, modelRight, modelUp, rightFootPitchOffsetDeg);
    }

    private WPos ToWorld(LegPoseFrame f)
    {
        WPos w;
        w.LHip = hips.TransformPoint(sourceScale * f.leftHip);
        w.LKnee = hips.TransformPoint(sourceScale * f.leftKnee);
        w.LAnkle = hips.TransformPoint(sourceScale * f.leftAnkle);
        w.LToe = hips.TransformPoint(sourceScale * f.leftToe);

        w.RHip = hips.TransformPoint(sourceScale * f.rightHip);
        w.RKnee = hips.TransformPoint(sourceScale * f.rightKnee);
        w.RAnkle = hips.TransformPoint(sourceScale * f.rightAnkle);
        w.RToe = hips.TransformPoint(sourceScale * f.rightToe);
        return w;
    }

    // ================= 源尺度自动匹配 =================
    private void TryCalibrateSourceScale(LegPoseFrame fr)
    {
        if (!leftUpperLeg || !leftLowerLeg || !rightUpperLeg || !rightLowerLeg || !leftFoot || !rightFoot) return;

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
        Debug.Log($"[LegModelDriver] 自动 sourceScale = {sourceScale:F3} (thigh {sThigh:F3}, calf {sCalf:F3})");
    }

    // ================= 应用段/脚 =================
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
        if (C_bind.TryGetValue(bone, out var c))
            R = R * c;

        if (rotSmoothing > 0f)
            bone.rotation = Quaternion.Slerp(bone.rotation, R, 1f - Mathf.Clamp01(rotSmoothing));
        else
            bone.rotation = R;
    }

    private void ApplyFoot(Transform foot, Vector3 ankleW, Vector3 toeW, Vector3 modelRight, Vector3 modelUp, float pitchOffsetDeg)
    {
        if (!foot) return;

        Vector3 fwd = toeW - ankleW;
        if (fwd.sqrMagnitude < 1e-10f)
        {
            // 若 Toe 缺失/重合，退化为用脚的 forward 近似
            fwd = foot.forward;
        }
        fwd.Normalize();

        Vector3 lat = modelRight - Vector3.Dot(modelRight, fwd) * fwd;
        if (lat.sqrMagnitude < 1e-10f) lat = Vector3.Cross(modelUp, fwd);
        lat.Normalize();

        Vector3 up = Vector3.Cross(lat, fwd).normalized;

        Quaternion R = Quaternion.LookRotation(fwd, up) * Quaternion.AngleAxis(pitchOffsetDeg, lat);
        if (C_bind.TryGetValue(foot, out var c))
            R = R * c;

        if (rotSmoothing > 0f)
            foot.rotation = Quaternion.Slerp(foot.rotation, R, 1f - Mathf.Clamp01(rotSmoothing));
        else
            foot.rotation = R;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!gizmoDrawChains || !hips) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(hips.position, hips.right * 0.2f);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(hips.position, hips.up * 0.2f);
    }
#endif
}
