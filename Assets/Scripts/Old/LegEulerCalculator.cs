using System;
using UnityEngine;

/// <summary>
/// 计算腿部各段的欧拉角（包含有效的 roll），读取自 LegPoseData
/// 通过事件广播每帧计算的角度数据
/// </summary>
public class LegEulerCalculator : MonoBehaviour
{
    [Header("数据源")]
    public LegPoseData poseDataSource;

    [Header("控制项")]
    public bool isCollecting = false;

    [Header("结果")]
    [HideInInspector] public Vector3 leftThighEuler;
    [HideInInspector] public Vector3 leftCalfEuler;
    [HideInInspector] public Vector3 leftFootEuler;
    [HideInInspector] public Vector3 rightThighEuler;
    [HideInInspector] public Vector3 rightCalfEuler;
    [HideInInspector] public Vector3 rightFootEuler;

    /// <summary>
    /// 每帧计算完角度后会触发，传递当前欧拉角数据
    /// </summary>
    public event Action<LegEulerFrame> OnEulerUpdated;

    void Start()
    {
        if (poseDataSource == null)
        {
            Debug.LogWarning("LegPoseData 未设置，欧拉角计算将无法开始。");
        }
    }

    void OnEnable()
    {
        if (poseDataSource != null)
        {
            poseDataSource.OnPoseDataUpdated += OnPoseDataUpdated;
        }
    }

    void OnDisable()
    {
        if (poseDataSource != null)
        {
            poseDataSource.OnPoseDataUpdated -= OnPoseDataUpdated;
        }
    }

    public void StartCollecting()
    {
        isCollecting = true;
        Debug.Log("开始采集欧拉角数据");
    }

    public void StopCollecting()
    {
        isCollecting = false;
        Debug.Log("停止采集欧拉角数据");
    }

    /// <summary>
    /// 接收到一帧姿势数据后计算欧拉角（含 roll）
    /// </summary>
    private void OnPoseDataUpdated(LegPoseFrame frame)
    {
        if (!isCollecting) return;

        // —— 1) 构造骨盆参考三轴（左右方向 + 前向 → 上方向）——
        // 用左右髋关节近似骨盆左右方向；前向用世界up与左右方向叉乘得到的正交方向（先不做坐标系标定）
        Vector3 pelvisRight = (frame.rightHip - frame.leftHip);
        if (pelvisRight.sqrMagnitude < 1e-8f) pelvisRight = Vector3.right;
        pelvisRight.Normalize();

        Vector3 pelvisForward = Vector3.Cross(Vector3.up, pelvisRight);
        if (pelvisForward.sqrMagnitude < 1e-8f) pelvisForward = Vector3.forward;
        pelvisForward.Normalize();

        Vector3 pelvisUp = Vector3.Cross(pelvisRight, pelvisForward);
        if (pelvisUp.sqrMagnitude < 1e-8f) pelvisUp = Vector3.up;
        pelvisUp.Normalize();

        // —— 2) 左腿：骨盆up → 大腿up → 小腿up → 脚up 逐段传播 —— 
        Vector3 L_thighF = SafeDir(frame.leftHip, frame.leftKnee);
        Vector3 L_thighUp = ProjectOnPlane(pelvisUp, L_thighF);
        leftThighEuler = NormalizeEuler(CalcEulerWithUp(frame.leftHip, frame.leftKnee, L_thighUp));

        Vector3 L_calfF = SafeDir(frame.leftKnee, frame.leftAnkle);
        Vector3 L_calfUp = ProjectOnPlane(L_thighUp, L_calfF);
        leftCalfEuler = NormalizeEuler(CalcEulerWithUp(frame.leftKnee, frame.leftAnkle, L_calfUp));

        Vector3 L_footF = SafeDir(frame.leftAnkle, frame.leftToe);
        Vector3 L_footUp = ProjectOnPlane(L_calfUp, L_footF);
        leftFootEuler = NormalizeEuler(CalcEulerWithUp(frame.leftAnkle, frame.leftToe, L_footUp));

        // —— 3) 右腿（同理） ——
        Vector3 R_thighF = SafeDir(frame.rightHip, frame.rightKnee);
        Vector3 R_thighUp = ProjectOnPlane(pelvisUp, R_thighF);
        rightThighEuler = NormalizeEuler(CalcEulerWithUp(frame.rightHip, frame.rightKnee, R_thighUp));

        Vector3 R_calfF = SafeDir(frame.rightKnee, frame.rightAnkle);
        Vector3 R_calfUp = ProjectOnPlane(R_thighUp, R_calfF);
        rightCalfEuler = NormalizeEuler(CalcEulerWithUp(frame.rightKnee, frame.rightAnkle, R_calfUp));

        Vector3 R_footF = SafeDir(frame.rightAnkle, frame.rightToe);
        Vector3 R_footUp = ProjectOnPlane(R_calfUp, R_footF);
        rightFootEuler = NormalizeEuler(CalcEulerWithUp(frame.rightAnkle, frame.rightToe, R_footUp));

        var result = new LegEulerFrame
        {
            leftThigh = leftThighEuler,
            leftCalf = leftCalfEuler,
            leftFoot = leftFootEuler,
            rightThigh = rightThighEuler,
            rightCalf = rightCalfEuler,
            rightFoot = rightFootEuler,
            timestamp = DateTime.Now
        };

        OnEulerUpdated?.Invoke(result);

        Debug.Log($"通过坐标计算出的角度（含 roll）：\n" +
                  $"- 左大腿: {leftThighEuler}，左小腿: {leftCalfEuler}，左脚: {leftFootEuler}\n" +
                  $"- 右大腿: {rightThighEuler}，右小腿: {rightCalfEuler}，右脚: {rightFootEuler}");
    }

    // —— 核心：给定 forward 与“解剖学 upRef”，计算完整欧拉角（含 roll） ——
    private Vector3 CalcEulerWithUp(Vector3 start, Vector3 end, Vector3 upRef)
    {
        Vector3 f = end - start;
        if (f.sqrMagnitude < 1e-8f) return Vector3.zero;
        f.Normalize();

        // 正交化 upRef，避免与 forward 共线
        Vector3 r = Vector3.Cross(upRef, f);
        if (r.sqrMagnitude < 1e-8f)
        {
            // 临时兜底轴，保证数值稳定（腿接近竖直等退化工况）
            Vector3 tmp = Mathf.Abs(f.y) < 0.99f ? Vector3.up : Vector3.right;
            r = Vector3.Cross(tmp, f);
        }
        r.Normalize();
        Vector3 up = Vector3.Cross(f, r); // 最终与 forward 正交的 up

        Quaternion q = Quaternion.LookRotation(f, up);
        Vector3 eul = q.eulerAngles;
        return new Vector3(
            NormalizeAngle(eul.x), // X: pitch
            NormalizeAngle(eul.y), // Y: yaw
            NormalizeAngle(eul.z)  // Z: roll（现在会随 upRef 变化而变化）
        );
    }

    // 将 v 投影到以 n 为法向的平面并归一化；若退化返回 Vector3.up 兜底
    private static Vector3 ProjectOnPlane(Vector3 v, Vector3 n)
    {
        Vector3 r = v - Vector3.Dot(v, n) * n;
        float m2 = r.sqrMagnitude;
        if (m2 < 1e-12f) return Vector3.up;
        return r / Mathf.Sqrt(m2);
    }

    // 安全求段向量方向
    private static Vector3 SafeDir(Vector3 a, Vector3 b)
    {
        Vector3 f = b - a;
        if (f.sqrMagnitude < 1e-8f) return Vector3.forward;
        return f.normalized;
    }

    private Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(
            NormalizeAngle(euler.x),
            NormalizeAngle(euler.y),
            NormalizeAngle(euler.z)
        );
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;
        return angle;
    }
}
