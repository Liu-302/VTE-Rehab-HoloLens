using UnityEngine;
using System;

/// <summary>
/// 保存腿部各部位的坐标（测试数据）
/// </summary>
public class LegPoseData : MonoBehaviour
{
    [Header("左腿")]
    public Vector3 leftHip;
    public Vector3 leftKnee;
    public Vector3 leftAnkle;
    public Vector3 leftToe;

    [Header("右腿")]
    public Vector3 rightHip;
    public Vector3 rightKnee;
    public Vector3 rightAnkle;
    public Vector3 rightToe;

    public enum TestPoseType
    {
        None,
        InitialState,
        AnkleFlexion,
        AnkleExtension,
        AnkleCircumduction,
        AnkleDorsiflexion,
        RaiseRightLeg,
        RaiseLeftLeg,
        QuadricepsIsotonic_RightRaise,
        QuadricepsIsotonic_LeftRaise
    }
    [Header("选择要加载的测试姿势")]
    public TestPoseType selectedPose = TestPoseType.None;

    [Header("是否开始实时传输姿势数据")]
    public bool isStreaming = false;

    // 姿势数据传输事件
    public event Action<LegPoseFrame> OnPoseDataUpdated;

    private void Start()
    {
        ApplySelectedPose();
    }

    /*
    private void Update()
    {
        //SetRaiseRightLegProcess(Time.deltaTime);

        if (isStreaming && OnPoseDataUpdated != null)
        {
            // 每帧广播一次数据
            var frame = new LegPoseFrame
            {
            leftHip = leftHip,
            leftKnee = leftKnee,
            leftAnkle = leftAnkle,
            leftToe = leftToe,
            rightHip = rightHip,
            rightKnee = rightKnee,
            rightAnkle = rightAnkle,
            rightToe = rightToe
            };

            OnPoseDataUpdated.Invoke(frame);
        }
    }
    */

    //-----------------------------------------------------------------------
    private void Update()
    {
        // 动态模拟右腿抬腿动作
        if (selectedPose == TestPoseType.RaiseRightLeg)
        {
            SimulateRaiseRightLeg(Time.deltaTime);
        }

        // 实时传输
        if (isStreaming && OnPoseDataUpdated != null)
        {
            var frame = new LegPoseFrame
            {
                leftHip = leftHip,
                leftKnee = leftKnee,
                leftAnkle = leftAnkle,
                leftToe = leftToe,
                rightHip = rightHip,
                rightKnee = rightKnee,
                rightAnkle = rightAnkle,
                rightToe = rightToe
            };

            OnPoseDataUpdated.Invoke(frame);
        }
    }
    //------------------------------------------------------------------------

    public void StartStreaming()
    {
        isStreaming = true;
        Debug.Log($"[LegPoseData] 已开始传输坐标数据，当前姿势为：{selectedPose}");
    }

    public void StopStreaming()
    {
        isStreaming = false;
        Debug.Log("[LegPoseData] 已停止传输坐标数据。");
    }

    private void ApplySelectedPose()
    {
        switch (selectedPose)
        {
            case TestPoseType.InitialState:
                SetInitialState();
                break;
            case TestPoseType.AnkleFlexion:
                SetAnkleFlexion();
                break;
            case TestPoseType.AnkleExtension:
                SetAnkleExtension();
                break;
            case TestPoseType.AnkleCircumduction:
                SetAnkleCircumduction();
                break;
            case TestPoseType.AnkleDorsiflexion:
                SetAnkleDorsiflexion();
                break;
            /*
        case TestPoseType.RaiseRightLeg:
            SetRaiseRightLeg();
            break;
            */
            case TestPoseType.RaiseRightLeg:
                raiseProgress = 0f;
                raising = true;
                break;

            case TestPoseType.RaiseLeftLeg:
                SetRaiseLeftLeg();
                break;
            case TestPoseType.QuadricepsIsotonic_RightRaise:
                SetSimulateQuadricepsIsotonic_RightRaise();
                break;
            case TestPoseType.QuadricepsIsotonic_LeftRaise:
                SetSimulateQuadricepsIsotonic_LeftRaise();
                break;
            case TestPoseType.None:
            default:
                break;
        }
    }

    //---------------------------------------------------------------
    // 动态动作用变量
    private float raiseProgress = 0f; // 抬腿进度（0~1）
    private bool raising = true;      // 是否在抬腿（否则是放腿）
    private float raiseSpeed = 0.5f;  // 控制快慢，0.5 就是一秒钟抬起或放下

    // 右腿抬腿过程中的关键帧
    private Vector3 rightKneeDown = new Vector3(0.082f, -0.065f, 0.44f);
    private Vector3 rightAnkleDown = new Vector3(0.082f, -0.083f, 0.875f);
    private Vector3 rightToeDown = new Vector3(0.07f, 0.008f, 0.945f);

    private Vector3 rightKneeUp = new Vector3(0.082f, 0.158f, 0.37f);
    private Vector3 rightAnkleUp = new Vector3(0.082f, 0.35f, 0.77f);
    private Vector3 rightToeUp = new Vector3(0.07f, 0.46f, 0.7f);

    private void SimulateRaiseRightLeg(float deltaTime)
    {
        // 左腿保持平放
        leftHip = new Vector3(-0.082f, -0.068f, 0f);
        leftKnee = new Vector3(-0.082f, -0.065f, 0.44f);
        leftAnkle = new Vector3(-0.082f, -0.083f, 0.875f);
        leftToe = new Vector3(-0.07f, 0.008f, 0.945f);
        rightHip = new Vector3(0.082f, -0.068f, 0f);

        // 控制进度
        raiseProgress += (raising ? 1f : -1f) * raiseSpeed * deltaTime;
        raiseProgress = Mathf.Clamp01(raiseProgress);

        // 插值计算右腿位置
        rightKnee = Vector3.Lerp(rightKneeDown, rightKneeUp, raiseProgress);
        rightAnkle = Vector3.Lerp(rightAnkleDown, rightAnkleUp, raiseProgress);
        rightToe = Vector3.Lerp(rightToeDown, rightToeUp, raiseProgress);

        // 往返切换（到头就反转）
        if (raiseProgress >= 1f)
            raising = false;
        else if (raiseProgress <= 0f)
            raising = true;
    }

    //---------------------------------------------------------------


    //============================= 各种动作封装为函数 =============================
    
    //Initial State: 初始状态
    public void SetInitialState()
    {
        //左腿平放
        leftHip = new Vector3(-0.082f, -0.068f, 0f);
        leftKnee = new Vector3(-0.082f, -0.065f, 0.44f);
        leftAnkle = new Vector3(-0.082f, -0.083f, 0.875f);
        leftToe = new Vector3(-0.07f, 0.008f, 0.945f);
        //右腿平放
        rightHip = new Vector3(0.082f, -0.068f, 0f);
        rightKnee = new Vector3(0.082f, -0.065f, 0.44f);
        rightAnkle = new Vector3(0.082f, -0.083f, 0.875f);
        rightToe = new Vector3(0.07f, 0.008f, 0.945f);
    }

    public void SetAnkleFlexion()
    {
        // 指定左腿屈脚
        leftHip = new Vector3(-0.082f, -0.068f, 0f);
        leftKnee = new Vector3(-0.082f, -0.065f, 0.43f);
        leftAnkle = new Vector3(-0.082f, -0.1f, 0.873f);
        leftToe = new Vector3(-0.07f, 0.02f, 0.865f);
        // 指定右腿屈脚
        rightHip = new Vector3(0.082f, -0.068f, 0f);
        rightKnee = new Vector3(0.082f, -0.065f, 0.43f);
        rightAnkle = new Vector3(0.082f, -0.1f, 0.873f);
        rightToe = new Vector3(0.07f, 0.02f, 0.865f);
    }

    public void SetAnkleExtension()
    {
        // 指定左腿伸脚
        leftHip = new Vector3(-0.082f, -0.068f, 0f);
        leftKnee = new Vector3(-0.082f, -0.065f, 0.43f);
        leftAnkle = new Vector3(-0.082f, -0.1f, 0.873f);
        leftToe = new Vector3(-0.07f, -0.04f, 0.98f);
        // 指定右腿伸脚
        rightHip = new Vector3(0.082f, -0.068f, 0f);
        rightKnee = new Vector3(0.082f, -0.065f, 0.43f);
        rightAnkle = new Vector3(0.082f, -0.1f, 0.873f);
        rightToe = new Vector3(0.07f, -0.04f, 0.98f);
    }

    public void SetAnkleCircumduction()
    {
        //左腿平放
        leftHip = new Vector3(-0.082f, -0.068f, 0f);
        leftKnee = new Vector3(-0.082f, -0.065f, 0.44f);
        leftAnkle = new Vector3(-0.082f, -0.098f, 0.875f);
        leftToe = new Vector3(-0.14f, -0.064f, 0.978f);
        //右腿平放
        rightHip = new Vector3(0.082f, -0.068f, 0f);
        rightKnee = new Vector3(0.082f, -0.065f, 0.44f);
        rightAnkle = new Vector3(0.082f, -0.098f, 0.875f);
        rightToe = new Vector3(0.097f, -0.024f, 0.968f);
    }

    public void SetAnkleDorsiflexion()
    {
        // 指定左腿屈脚
        leftHip = new Vector3(-0.082f, -0.068f, 0f);
        leftKnee = new Vector3(-0.082f, -0.065f, 0.43f);
        leftAnkle = new Vector3(-0.082f, -0.1f, 0.873f);
        leftToe = new Vector3(-0.07f, 0.02f, 0.865f);
        //右腿平放
        rightHip = new Vector3(0.082f, -0.068f, 0f);
        rightKnee = new Vector3(0.082f, -0.065f, 0.44f);
        rightAnkle = new Vector3(0.082f, -0.083f, 0.875f);
        rightToe = new Vector3(0.07f, 0.008f, 0.945f);
    }

    public void SetRaiseRightLeg()
    {
        //左腿平放
        leftHip = new Vector3(-0.082f, -0.068f, 0f);
        leftKnee = new Vector3(-0.082f, -0.065f, 0.44f);
        leftAnkle = new Vector3(-0.082f, -0.083f, 0.875f);
        leftToe = new Vector3(-0.07f, 0.008f, 0.945f);
        //右腿抬起
        rightHip = new Vector3(0.082f, -0.068f, 0f);
        rightKnee = new Vector3(0.082f, 0.158f, 0.37f);
        rightAnkle = new Vector3(0.082f, 0.35f, 0.77f);
        rightToe = new Vector3(0.07f, 0.46f, 0.7f);
    }

    public void SetRaiseLeftLeg()
    {
        //左腿抬起
        leftHip = new Vector3(-0.082f, -0.068f, 0f);
        leftKnee = new Vector3(-0.082f, 0.158f, 0.37f);
        leftAnkle = new Vector3(-0.082f, 0.35f, 0.77f);
        leftToe = new Vector3(-0.07f, 0.46f, 0.7f);
        //右腿平放
        rightHip = new Vector3(0.082f, -0.068f, 0f);
        rightKnee = new Vector3(0.082f, -0.065f, 0.44f);
        rightAnkle = new Vector3(0.082f, -0.083f, 0.875f);
        rightToe = new Vector3(0.07f, 0.008f, 0.945f);
    }

    public void SetSimulateQuadricepsIsotonic_RightRaise()
    {
        //左腿屈膝
        leftHip = new Vector3(-0.082f, -0.068f, 0f);
        leftKnee = new Vector3(-0.082f, 0.158f, 0.37f);
        leftAnkle = new Vector3(-0.082f, -0.095f, 0.73f);
        leftToe = new Vector3(-0.07f, -0.095f, 0.85f);
        //右腿抬腿
        rightHip = new Vector3(0.082f, -0.068f, 0f);
        rightKnee = new Vector3(0.082f, 0.158f, 0.37f);
        rightAnkle = new Vector3(0.082f, 0.35f, 0.77f);
        rightToe = new Vector3(0.07f, 0.46f, 0.7f);
    }

    public void SetSimulateQuadricepsIsotonic_LeftRaise()
    {
        // 左腿抬腿
        leftHip = new Vector3(-0.082f, -0.068f, 0f);
        leftKnee = new Vector3(-0.082f, 0.25f, 0.3f);
        leftAnkle = new Vector3(-0.082f, 0.54f, 0.64f);
        leftToe = new Vector3(-0.07f, 0.62f, 0.55f);
        // 右腿屈膝
        rightHip = new Vector3(0.082f, -0.068f, 0f);
        rightKnee = new Vector3(0.082f, 0.21f, 0.32f);
        rightAnkle = new Vector3(0.082f, -0.09f, 0.63f);
        rightToe = new Vector3(0.07f, -0.09f, 0.75f);
    }
    //==============================================================================
}