using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class ExtremumRecorder : MonoBehaviour
{
    public enum ExtremumType
    {
        None,
        SimulateAnkleFlexionExtension_LeftFootPitchMax,
        SimulateAnkleFlexionExtension_RightFootPitchMax,
        SimulateAnkleFlexionExtension_FootPitchMax,
        SimulateAnkleFlexionExtension_LeftFootPitchMin,
        SimulateAnkleFlexionExtension_RightFootPitchMin,
        SimulateAnkleFlexionExtension_FootPitchMin,
        SimulateAnkleCircumduction_LeftFootPitchYawMax,
        SimulateAnkleCircumduction_RightFootPitchYawMax,
        SimulateAnkleCircumduction_FootPitchYawMax,
        SimulateRaiseLegAlternately_LeftThighPitchMax,
        SimulateRaiseLegAlternately_RightThighPitchMax,
        SimulateAnkleDorsiflexionAlternately_LeftFootPitchMax,
        SimulateAnkleDorsiflexionAlternately_RightFootPitchMax,
        SimulateQuadricepsIsotonicExercise_LeftThighPitchMax,
        SimulateQuadricepsIsotonicExercise_RightThighPitchMax,
        InitialState
    }

    [Serializable]
    public class ExtremumFrame
    {
        public ExtremumType type;
        public Vector3 leftThighEuler;
        public Vector3 rightThighEuler;
        public Vector3 leftCalfEuler;
        public Vector3 rightCalfEuler;
        public Vector3 leftFootEuler;
        public Vector3 rightFootEuler;
        public DateTime timestamp;
    }


    public enum MotionType
    {
        InitialState,                  // 初始状态
        AnkleFlexion,                  // 脚踝屈
        AnkleExtension,                // 脚踝伸
        AnkleCircumduction,            // 脚踝旋转
        AnkleDorsiflexionAlternately,  // 交替勾脚
        RaiseLegAlternately_Left,      // 抬左腿
        RaiseLegAlternately_Right,     // 抬右腿
        QuadricepsIsotonic_LeftRaise,  // 股四头肌等张（左抬）
        QuadricepsIsotonic_RightRaise  // 股四头肌等张（右抬）
    }


    [Header("阈值设置")]
    [SerializeField] private float pitchThreshold = 2f;
    [SerializeField] private float yawThreshold = 2f;
    [SerializeField] private int initialStateSampleCount = 30;

    [Header("角度来源")]
    [SerializeField] private LegEulerCalculator angleProvider;

    [Header("当前动作")]
    [SerializeField] private MotionType currentMotion;

    // 存储极值帧
    private Dictionary<ExtremumType, ExtremumFrame> extremumFrames = new Dictionary<ExtremumType, ExtremumFrame>();
    private List<ExtremumFrame> initialStateSamples = new List<ExtremumFrame>();

    // 是否正在采集极值
    private bool isRecording = false;

    public event Action<ExtremumFrame> OnExtremumUpdated;

    private void OnEnable()
    {
        if (angleProvider != null)
        {
            angleProvider.OnEulerUpdated += OnNewEulerFrame;
        }
        else
        {
            Debug.LogError("[ExtremumRecorder] 未绑定 LegEulerCalculator，脚本无法工作！");
            enabled = false;
        }
    }

    private void OnDisable()
    {
        if (angleProvider != null)
        {
            angleProvider.OnEulerUpdated -= OnNewEulerFrame;
        }
    }

    /// <summary>
    /// 开始采集极值数据，清空旧数据
    /// </summary>
    public void StartRecording()
    {
        isRecording = true;
        extremumFrames.Clear();
        initialStateSamples.Clear();
        Debug.Log("[ExtremumRecorder] 开始采集极值数据");
    }

    /// <summary>
    /// 停止采集极值数据
    /// </summary>
    public void StopRecording()
    {
        isRecording = false;
        Debug.Log("[ExtremumRecorder] 停止采集极值数据");

        // 输出所有极值帧
        foreach (var kvp in extremumFrames)
        {
            ExtremumType type = kvp.Key;
            ExtremumFrame frame = kvp.Value;

            Debug.LogFormat("[Extremum] {0} | Time: {1}\n  左大腿: {2}\n  右大腿: {3}\n  左小腿: {4}\n  右小腿: {5}\n  左脚: {6}\n  右脚: {7}",
                type,
                frame.timestamp.ToString("HH:mm:ss.fff"),
                frame.leftThighEuler,
                frame.rightThighEuler,
                frame.leftCalfEuler,
                frame.rightCalfEuler,
                frame.leftFootEuler,
                frame.rightFootEuler);
        }
    }

    /// <summary>
    /// 设置当前动作类型
    /// </summary>
    /// <param name="motion"></param>
    /// 目前没用，是手动设置的
    public void SetCurrentMotion(MotionType motion)
    {
        currentMotion = motion;
        Debug.Log($"[ExtremumRecorder] 当前动作类型设为：{motion}");
    }

    /// <summary>
    /// 接收到新的欧拉角数据时调用，更新极值
    /// </summary>
    /// <param name="frame"></param>
    private void OnNewEulerFrame(LegEulerFrame frame)
    {
        //Debug.Log("[ExtremumRecorder] 收到新的欧拉角数据");

        if (!isRecording || frame == null) return;

        if (currentMotion == MotionType.InitialState)
        {
            // 收集初始状态采样
            CollectInitialStateSample(frame);

            if (initialStateSamples.Count >= initialStateSampleCount)
            {
                // 样本采集够了，计算平均值并更新极值帧
                ExtremumFrame avg = CalculateAverageInitialState();
                extremumFrames[ExtremumType.InitialState] = avg;
                OnExtremumUpdated?.Invoke(avg);

                // 采集完成后，自动切换动作
                // currentMotion = MotionType.SimulateAnkleFlexionExtension; 
                // initialStateSamples.Clear();
            }
            return;
        }

        // 5个训练动作的极值更新逻辑
        switch (currentMotion)
        {
            case MotionType.AnkleFlexion:
                TryUpdateExtremum(ExtremumType.SimulateAnkleFlexionExtension_LeftFootPitchMax, frame.leftFoot.x, true, frame);
                TryUpdateExtremum(ExtremumType.SimulateAnkleFlexionExtension_RightFootPitchMax, frame.rightFoot.x, true, frame);
                break;

            case MotionType.AnkleExtension:
                TryUpdateExtremum(ExtremumType.SimulateAnkleFlexionExtension_LeftFootPitchMin, frame.leftFoot.x, false, frame);
                TryUpdateExtremum(ExtremumType.SimulateAnkleFlexionExtension_RightFootPitchMin, frame.rightFoot.x, false, frame);
                break;

            case MotionType.AnkleCircumduction:
                TryUpdateExtremum(ExtremumType.SimulateAnkleCircumduction_LeftFootPitchYawMax,
                    frame.leftFoot.x + frame.leftFoot.y, true, frame);
                TryUpdateExtremum(ExtremumType.SimulateAnkleCircumduction_RightFootPitchYawMax,
                    frame.rightFoot.x + frame.rightFoot.y, true, frame);
                break;

            case MotionType.AnkleDorsiflexionAlternately:
                TryUpdateExtremum(ExtremumType.SimulateAnkleDorsiflexionAlternately_LeftFootPitchMax, frame.leftFoot.x, true, frame);
                TryUpdateExtremum(ExtremumType.SimulateAnkleDorsiflexionAlternately_RightFootPitchMax, frame.rightFoot.x, true, frame);
                break;

            case MotionType.RaiseLegAlternately_Left:
                TryUpdateExtremum(ExtremumType.SimulateRaiseLegAlternately_LeftThighPitchMax, frame.leftThigh.x, true, frame);
                break;

            case MotionType.RaiseLegAlternately_Right:
                TryUpdateExtremum(ExtremumType.SimulateRaiseLegAlternately_RightThighPitchMax, frame.rightThigh.x, true, frame);
                break;

            case MotionType.QuadricepsIsotonic_LeftRaise:
                TryUpdateExtremum(ExtremumType.SimulateQuadricepsIsotonicExercise_LeftThighPitchMax, frame.leftThigh.x, true, frame);
                break;

            case MotionType.QuadricepsIsotonic_RightRaise:
                TryUpdateExtremum(ExtremumType.SimulateQuadricepsIsotonicExercise_RightThighPitchMax, frame.rightThigh.x, true, frame);
                break;
        }

    }

    private void CollectInitialStateSample(LegEulerFrame frame)
    {
        ExtremumFrame sample = CreateExtremumFrame(ExtremumType.InitialState, frame);
        initialStateSamples.Add(sample);
    }

    private ExtremumFrame CalculateAverageInitialState()
    {
        ExtremumFrame avg = new ExtremumFrame();
        foreach (var f in initialStateSamples)
        {
            avg.leftThighEuler += f.leftThighEuler;
            avg.rightThighEuler += f.rightThighEuler;
            avg.leftCalfEuler += f.leftCalfEuler;
            avg.rightCalfEuler += f.rightCalfEuler;
            avg.leftFootEuler += f.leftFootEuler;
            avg.rightFootEuler += f.rightFootEuler;
        }
        int n = initialStateSamples.Count;
        avg.leftThighEuler /= n;
        avg.rightThighEuler /= n;
        avg.leftCalfEuler /= n;
        avg.rightCalfEuler /= n;
        avg.leftFootEuler /= n;
        avg.rightFootEuler /= n;

        avg.timestamp = DateTime.Now;
        avg.type = ExtremumType.InitialState;
        return avg;
    }

    private void TryUpdateExtremum(ExtremumType type, float value, bool isMax, LegEulerFrame frame)

    {
        if (!extremumFrames.ContainsKey(type))
        {
            extremumFrames[type] = new ExtremumFrame
            {
                type = type,
                leftFootEuler = frame.leftFoot,
                rightFootEuler = frame.rightFoot,
                leftThighEuler = frame.leftThigh,
                rightThighEuler = frame.rightThigh,
                leftCalfEuler = frame.leftCalf,
                rightCalfEuler = frame.rightCalf,
                timestamp = DateTime.Now
            };
            TryCombineExtremums(type); // 在初次添加后也触发检查
            return;
        }

        float existingValue = GetValueByType(extremumFrames[type], type);

        // 计算阈值：根据类型决定用pitchThreshold还是yawThreshold
        float threshold = pitchThreshold;
        string typeName = type.ToString();
        if (typeName.Contains("Yaw"))
        {
            threshold = yawThreshold;
        }

        // 差值是否超过阈值
        if (Mathf.Abs(value - existingValue) < threshold)
        {
            // 差值不够大，不更新
            return;
        }

        bool shouldUpdate = isMax ? value > existingValue : value < existingValue;
        if (shouldUpdate)
        {
            extremumFrames[type] = new ExtremumFrame
            {
                type = type,
                leftFootEuler = frame.leftFoot,
                rightFootEuler = frame.rightFoot,
                leftThighEuler = frame.leftThigh,
                rightThighEuler = frame.rightThigh,
                leftCalfEuler = frame.leftCalf,
                rightCalfEuler = frame.rightCalf,
                timestamp = DateTime.Now
            };

            TryCombineExtremums(type); // 更新后尝试合并
        }
    }


    private void TryCombineExtremums(ExtremumType updatedType)
    {
        // pitch max 合并
        if ((updatedType == ExtremumType.SimulateAnkleFlexionExtension_LeftFootPitchMax ||
             updatedType == ExtremumType.SimulateAnkleFlexionExtension_RightFootPitchMax) &&
            extremumFrames.ContainsKey(ExtremumType.SimulateAnkleFlexionExtension_LeftFootPitchMax) &&
            extremumFrames.ContainsKey(ExtremumType.SimulateAnkleFlexionExtension_RightFootPitchMax))
        {
            var left = extremumFrames[ExtremumType.SimulateAnkleFlexionExtension_LeftFootPitchMax];
            var right = extremumFrames[ExtremumType.SimulateAnkleFlexionExtension_RightFootPitchMax];

            ExtremumFrame combined = new ExtremumFrame
            {
                type = ExtremumType.SimulateAnkleFlexionExtension_FootPitchMax,
                leftFootEuler = left.leftFootEuler,
                rightFootEuler = right.rightFootEuler,
                leftThighEuler = left.leftThighEuler,
                rightThighEuler = right.rightThighEuler,
                leftCalfEuler = left.leftCalfEuler,
                rightCalfEuler = right.rightCalfEuler,
                timestamp = DateTime.Now
            };

            extremumFrames[combined.type] = combined;
            OnExtremumUpdated?.Invoke(combined);
        }

        // pitch min 合并
        if ((updatedType == ExtremumType.SimulateAnkleFlexionExtension_LeftFootPitchMin ||
             updatedType == ExtremumType.SimulateAnkleFlexionExtension_RightFootPitchMin) &&
            extremumFrames.ContainsKey(ExtremumType.SimulateAnkleFlexionExtension_LeftFootPitchMin) &&
            extremumFrames.ContainsKey(ExtremumType.SimulateAnkleFlexionExtension_RightFootPitchMin))
        {
            var left = extremumFrames[ExtremumType.SimulateAnkleFlexionExtension_LeftFootPitchMin];
            var right = extremumFrames[ExtremumType.SimulateAnkleFlexionExtension_RightFootPitchMin];

            ExtremumFrame combined = new ExtremumFrame
            {
                type = ExtremumType.SimulateAnkleFlexionExtension_FootPitchMin,
                leftFootEuler = left.leftFootEuler,
                rightFootEuler = right.rightFootEuler,
                leftThighEuler = left.leftThighEuler,
                rightThighEuler = right.rightThighEuler,
                leftCalfEuler = left.leftCalfEuler,
                rightCalfEuler = right.rightCalfEuler,
                timestamp = DateTime.Now
            };

            extremumFrames[combined.type] = combined;
            OnExtremumUpdated?.Invoke(combined);
        }

        // pitch + yaw max 合并 (脚踝环绕)
        if ((updatedType == ExtremumType.SimulateAnkleCircumduction_LeftFootPitchYawMax ||
             updatedType == ExtremumType.SimulateAnkleCircumduction_RightFootPitchYawMax) &&
            extremumFrames.ContainsKey(ExtremumType.SimulateAnkleCircumduction_LeftFootPitchYawMax) &&
            extremumFrames.ContainsKey(ExtremumType.SimulateAnkleCircumduction_RightFootPitchYawMax))
        {
            var left = extremumFrames[ExtremumType.SimulateAnkleCircumduction_LeftFootPitchYawMax];
            var right = extremumFrames[ExtremumType.SimulateAnkleCircumduction_RightFootPitchYawMax];

            ExtremumFrame combined = new ExtremumFrame
            {
                type = ExtremumType.SimulateAnkleCircumduction_FootPitchYawMax,
                leftFootEuler = left.leftFootEuler,
                rightFootEuler = right.rightFootEuler,
                leftThighEuler = left.leftThighEuler,
                rightThighEuler = right.rightThighEuler,
                leftCalfEuler = left.leftCalfEuler,
                rightCalfEuler = right.rightCalfEuler,
                timestamp = DateTime.Now
            };

            extremumFrames[combined.type] = combined;
            OnExtremumUpdated?.Invoke(combined);
        }
    }


    private ExtremumFrame CreateExtremumFrame(ExtremumType type, LegEulerFrame frame)
    {
        return new ExtremumFrame
        {
            type = type,
            leftThighEuler = frame.leftThigh,
            rightThighEuler = frame.rightThigh,
            leftCalfEuler = frame.leftCalf,
            rightCalfEuler = frame.rightCalf,
            leftFootEuler = frame.leftFoot,
            rightFootEuler = frame.rightFoot,
            timestamp = frame.timestamp
        };
    }

    private float GetValueByType(ExtremumFrame frame, ExtremumType type)
    {
        switch (type)
        {
            case ExtremumType.SimulateAnkleFlexionExtension_LeftFootPitchMax:
            case ExtremumType.SimulateAnkleFlexionExtension_LeftFootPitchMin:
            case ExtremumType.SimulateAnkleDorsiflexionAlternately_LeftFootPitchMax:
                return frame.leftFootEuler.x;
            case ExtremumType.SimulateAnkleFlexionExtension_RightFootPitchMax:
            case ExtremumType.SimulateAnkleFlexionExtension_RightFootPitchMin:
            case ExtremumType.SimulateAnkleDorsiflexionAlternately_RightFootPitchMax:
                return frame.rightFootEuler.x;
            case ExtremumType.SimulateAnkleCircumduction_LeftFootPitchYawMax:
                return frame.leftFootEuler.x + frame.leftFootEuler.y;
            case ExtremumType.SimulateAnkleCircumduction_RightFootPitchYawMax:
                return frame.rightFootEuler.x + frame.rightFootEuler.y;
            case ExtremumType.SimulateRaiseLegAlternately_LeftThighPitchMax:
            case ExtremumType.SimulateQuadricepsIsotonicExercise_LeftThighPitchMax:
                return frame.leftThighEuler.x;
            case ExtremumType.SimulateRaiseLegAlternately_RightThighPitchMax:
            case ExtremumType.SimulateQuadricepsIsotonicExercise_RightThighPitchMax:
                return frame.rightThighEuler.x;
            default:
                return 0f;
        }
    }

    private bool IsOutlier(ExtremumType type, float newValue)
    {
        if (extremumFrames.TryGetValue(type, out ExtremumFrame oldFrame))
        {
            float oldValue = GetValueByType(oldFrame, type);
            return Mathf.Abs(newValue - oldValue) > 20f;
        }
        return false;
    }

    public ExtremumFrame GetExtremum(ExtremumType type)
    {
        extremumFrames.TryGetValue(type, out ExtremumFrame result);
        return result;
    }
}
