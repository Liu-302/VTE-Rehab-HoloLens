using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 绝对重定向（无 Extremum 依赖）
/// - 假设模型绑定为 T-Pose 且面朝 +Z（Unity 默认：Hips.up≈+Y, Hips.right≈+X, Hips.forward≈+Z）
/// - 全程用 Hips 的本地基向量（Right/Up/Forward）构造正交基，确保大小腿/脚不会“忽正忽反”
/// - 绑定姿态校准 C_bind 修正模型轴向
/// - 自动源尺度匹配（可关）
/// - StartTraining/StopTraining 控制渲染开关
/// </summary>
public class TargetPoseRenderer : MonoBehaviour
{
    [Header("数据源（以 Hips 为原点的本地坐标）")]
    public LegPoseData poseDataSource;

    [Header("必须：角色骨骼")]
    public Transform hips;

    [Header("左腿骨骼")]
    public Transform leftUpperLeg;
    public Transform leftLowerLeg;
    public Transform leftFoot;

    [Header("右腿骨骼")]
    public Transform rightUpperLeg;
    public Transform rightLowerLeg;
    public Transform rightFoot;

    [Header("可选：大脚趾（若有）提高脚部前向稳定性")]
    public Transform leftToeHint;
    public Transform rightToeHint;

    [Header("绑定姿态校准")]
    public bool calibrateBindPoseNow = false; // 勾一次：重算 C_bind（不看坐标）

    [Header("源坐标缩放（自动估计后也可手调）")]
    public bool autoSourceScale = true;
    public float sourceScale = 1f;

    [Header("脚踝X轴微校正（度）")]
    public float leftFootPitchOffsetDeg = 0f;   // 左脚踝：- 倾向脚背下压，+ 勾脚
    public float rightFootPitchOffsetDeg = 0f;  // 右脚踝：- 倾向脚背下压，+ 勾脚


    // —— 内部状态 —— //
    private bool isRendering = false;
    private bool hasFrame = false;
    private bool scaleCalibrated = false;

    // 绑定姿态修正：bone.rotation ≈ R_bind * C_bind
    private readonly Dictionary<Transform, Quaternion> C_bind = new Dictionary<Transform, Quaternion>();

    // 一帧数据
    private struct Frame
    {
        public Vector3 LHip, LKnee, LAnkle, LToe;
        public Vector3 RHip, RKnee, RAnkle, RToe;
    }
    private Frame _last;

    // —— 生命周期 —— //
    void OnEnable()
    {
        if (!poseDataSource)
            poseDataSource = FindObjectOfType<LegPoseData>();
        if (poseDataSource != null)
            poseDataSource.OnPoseDataUpdated += OnPoseDataUpdated;
    }

    void OnDisable()
    {
        if (poseDataSource != null)
            poseDataSource.OnPoseDataUpdated -= OnPoseDataUpdated;
    }

    void Start()
    {
        CalibrateBindPose();
    }

    void Update()
    {
        if (calibrateBindPoseNow)
        {
            calibrateBindPoseNow = false;
            CalibrateBindPose();
        }

        if (!isRendering) return;

        if (!hasFrame && poseDataSource != null)
        {
            _last = new Frame
            {
                LHip = poseDataSource.leftHip,
                LKnee = poseDataSource.leftKnee,
                LAnkle = poseDataSource.leftAnkle,
                LToe = poseDataSource.leftToe,
                RHip = poseDataSource.rightHip,
                RKnee = poseDataSource.rightKnee,
                RAnkle = poseDataSource.rightAnkle,
                RToe = poseDataSource.rightToe
            };
            hasFrame = true;
        }

        if (hasFrame && autoSourceScale && !scaleCalibrated)
        {
            TryCalibrateSourceScale(_last);
        }

        if (hasFrame)
        {
            ApplyFrameWithHipBasis(_last);
            hasFrame = false;
        }
    }

    private void OnPoseDataUpdated(LegPoseFrame f)
    {
        _last = new Frame
        {
            LHip = f.leftHip,
            LKnee = f.leftKnee,
            LAnkle = f.leftAnkle,
            LToe = f.leftToe,
            RHip = f.rightHip,
            RKnee = f.rightKnee,
            RAnkle = f.rightAnkle,
            RToe = f.rightToe
        };
        hasFrame = true;

        if (autoSourceScale && !scaleCalibrated)
            TryCalibrateSourceScale(_last);
    }

    // ================= 源尺度自动匹配 =================
    private void TryCalibrateSourceScale(Frame fr)
    {
        if (!hips || leftUpperLeg == null || leftLowerLeg == null || rightUpperLeg == null || rightLowerLeg == null
            || leftFoot == null || rightFoot == null) return;

        float srcThighL = (fr.LKnee - fr.LHip).magnitude;
        float srcThighR = (fr.RKnee - fr.RHip).magnitude;
        float srcCalfL = (fr.LAnkle - fr.LKnee).magnitude;
        float srcCalfR = (fr.RAnkle - fr.RKnee).magnitude;

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

        Debug.Log($"[TargetPoseRenderer] 自动估计 sourceScale = {sourceScale:F3} (thigh {sThigh:F3}, calf {sCalf:F3})");
    }

    // ================= 绑定姿态校准（与运行时同一套基） =================
    public void CalibrateBindPose()
    {
        C_bind.Clear();

        // 用 Hips 的本地基（在世界中的方向）
        Vector3 modelRight = hips ? hips.right : Vector3.right; // +X
        Vector3 modelUp = hips ? hips.up : Vector3.up;    // +Y
        // Vector3 modelFwd   = hips ? hips.forward : Vector3.forward; // +Z（不直接用到）

        // 左腿
        CalibBind(leftUpperLeg, leftLowerLeg ? leftLowerLeg.position : leftUpperLeg.position + leftUpperLeg.forward, modelRight, modelUp);
        CalibBind(leftLowerLeg, leftFoot ? leftFoot.position : leftLowerLeg.position + leftLowerLeg.forward, modelRight, modelUp);
        CalibBindFoot(leftFoot, leftToeHint ? leftToeHint.position : leftFoot.position + leftFoot.forward, modelRight, modelUp);

        // 右腿
        CalibBind(rightUpperLeg, rightLowerLeg ? rightLowerLeg.position : rightUpperLeg.position + rightUpperLeg.forward, modelRight, modelUp);
        CalibBind(rightLowerLeg, rightFoot ? rightFoot.position : rightLowerLeg.position + rightLowerLeg.forward, modelRight, modelUp);
        CalibBindFoot(rightFoot, rightToeHint ? rightToeHint.position : rightFoot.position + rightFoot.forward, modelRight, modelUp);

        Debug.Log("[TargetPoseRenderer] 绑定姿态校准完成（C_bind 已更新，Hip 基坐标）。");
    }

    private void CalibBind(Transform bone, Vector3 childPosWorld, Vector3 modelRight, Vector3 modelUp)
    {
        if (!bone) return;
        Vector3 fwd = childPosWorld - bone.position;
        if (fwd.sqrMagnitude < 1e-10f) return;
        fwd.Normalize();

        // lat = project(modelRight, ⟂ fwd)
        Vector3 lat = modelRight - Vector3.Dot(modelRight, fwd) * fwd;
        if (lat.sqrMagnitude < 1e-10f) lat = Vector3.Cross(modelUp, fwd); // 退化兜底
        lat.Normalize();
        // up = lat × fwd（右手系）
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

    // ================== 应用一帧（严格使用 Hip 基） ==================
    private void ApplyFrameWithHipBasis(Frame fr)
    {
        if (!hips)
        {
            Debug.LogWarning("[TargetPoseRenderer] hips 未指定。");
            return;
        }

        // 世界化 + 尺度
        Vector3 LHipW = hips.TransformPoint(sourceScale * fr.LHip);
        Vector3 LKneeW = hips.TransformPoint(sourceScale * fr.LKnee);
        Vector3 LAnkleW = hips.TransformPoint(sourceScale * fr.LAnkle);
        Vector3 LToeW = hips.TransformPoint(sourceScale * fr.LToe);

        Vector3 RHipW = hips.TransformPoint(sourceScale * fr.RHip);
        Vector3 RKneeW = hips.TransformPoint(sourceScale * fr.RKnee);
        Vector3 RAnkleW = hips.TransformPoint(sourceScale * fr.RAnkle);
        Vector3 RToeW = hips.TransformPoint(sourceScale * fr.RToe);

        // 固定 Hip 基向量（你模型 T-Pose +Z 朝前）
        Vector3 modelRight = hips.right; // +X
        Vector3 modelUp = hips.up;    // +Y
        // Vector3 modelFwd   = hips.forward; // +Z（不直接用）

        // 左腿
        ApplySegment(leftUpperLeg, LHipW, LKneeW, modelRight, modelUp);
        ApplySegment(leftLowerLeg, LKneeW, LAnkleW, modelRight, modelUp);
        ApplyFoot(leftFoot, LAnkleW, LToeW, modelRight, modelUp);

        // 右腿
        ApplySegment(rightUpperLeg, RHipW, RKneeW, modelRight, modelUp);
        ApplySegment(rightLowerLeg, RKneeW, RAnkleW, modelRight, modelUp);
        ApplyFoot(rightFoot, RAnkleW, RToeW, modelRight, modelUp);
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

        Vector3 up = Vector3.Cross(lat, fwd).normalized; // 右手系固定

        Quaternion R_pose = Quaternion.LookRotation(fwd, up);
        if (C_bind.TryGetValue(bone, out var c))
            bone.rotation = R_pose * c;
        else
            bone.rotation = R_pose;
    }

    private void ApplyFoot(Transform foot, Vector3 ankleW, Vector3 toeW, Vector3 modelRight, Vector3 modelUp)
    {
        if (!foot) return;
        Vector3 fwd = toeW - ankleW;
        if (fwd.sqrMagnitude < 1e-10f) return;
        fwd.Normalize();

        Vector3 lat = modelRight - Vector3.Dot(modelRight, fwd) * fwd;
        if (lat.sqrMagnitude < 1e-10f) lat = Vector3.Cross(modelUp, fwd);
        lat.Normalize();

        Vector3 up = Vector3.Cross(lat, fwd).normalized;

        // —— 关键：脚踝 X（俯仰）微校正，绕 lat 轴 —— //
        float offsetDeg = (foot == leftFoot) ? leftFootPitchOffsetDeg
                        : (foot == rightFoot) ? rightFootPitchOffsetDeg
                        : 0f;
        Quaternion pitchCorr = Quaternion.AngleAxis(offsetDeg, lat);

        Quaternion R_pose = Quaternion.LookRotation(fwd, up) * pitchCorr; // 先姿态，再绕lat微调
        if (C_bind.TryGetValue(foot, out var c))
            foot.rotation = R_pose * c;
        else
            foot.rotation = R_pose;
    }

    // ================== 渲染开关 ==================
    public void StartTraining()
    {
        if (C_bind.Count == 0) CalibrateBindPose();
        isRendering = true;
        Debug.Log("开始渲染（Hip 基坐标：+X 右，+Y 上，+Z 前）。");
    }

    public void StopTraining()
    {
        isRendering = false;
        Debug.Log("停止渲染。");
    }
}