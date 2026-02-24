using UnityEngine;

/// <summary>
/// 用 PoseStreamBus 的实时帧（来自 All Data 播放）渲染两条腿的折线与关键点球体。
/// - 坐标以 hipsTransform 为原点：world = hips.TransformPoint(sourceScale * local)
/// - 仅依赖 PoseStreamBus（CollectGuideFromFiles 会往 Bus 里发布每帧）
/// </summary>
public class LegLineRenderer : MonoBehaviour
{
    [Header("参考节点（局部→世界）")]
    public Transform hipsTransform;

    [Header("数据源（必须）")]
    public PoseStreamBus bus;

    [Header("外观与平滑")]
    [Tooltip("将源坐标整体缩放后再 TransformPoint，便于匹配模型尺寸")]
    public float sourceScale = 1f;

    [Tooltip("位置平滑（0=无，1=慢）")]
    [Range(0f, 1f)] public float positionSmoothing = 0f;

    [Tooltip("若未手动绑定 LineRenderer，自动在本物体下创建两条线")]
    public bool autoCreateLines = true;

    [Header("左腿线条")]
    public LineRenderer leftLegLine;
    [Header("右腿线条")]
    public LineRenderer rightLegLine;

    [Header("左腿关键点球体")]
    public Transform leftHipSphere;
    public Transform leftKneeSphere;
    public Transform leftAnkleSphere;
    public Transform leftToeSphere;

    [Header("右腿关键点球体")]
    public Transform rightHipSphere;
    public Transform rightKneeSphere;
    public Transform rightAnkleSphere;
    public Transform rightToeSphere;

    // —— 内部缓存 —— //
    private bool hasFrame = false;
    private LegPoseFrame lastFrame;

    // 平滑缓存（世界坐标顺序：LH,LK,LA,LT,RH,RK,RA,RT）
    private Vector3[] prevWorld = new Vector3[8];
    private bool hasPrev = false;

    void Reset()
    {
        if (autoCreateLines)
        {
            if (!leftLegLine)
            {
                var go = new GameObject("LeftLegLine");
                go.transform.SetParent(transform, false);
                leftLegLine = go.AddComponent<LineRenderer>();
                SetupLine(leftLegLine, Color.cyan);
            }
            if (!rightLegLine)
            {
                var go = new GameObject("RightLegLine");
                go.transform.SetParent(transform, false);
                rightLegLine = go.AddComponent<LineRenderer>();
                SetupLine(rightLegLine, Color.yellow);
            }
        }
    }

    void OnEnable()
    {
        if (bus != null) bus.OnPoseDataUpdated += OnBusFrame;
    }

    void OnDisable()
    {
        if (bus != null) bus.OnPoseDataUpdated -= OnBusFrame;
    }

    private void OnBusFrame(LegPoseFrame f)
    {
        lastFrame = f;
        hasFrame = true;
    }

    void Update()
    {
        if (!hipsTransform || bus == null || !hasFrame) return;

        // 局部(MidHip为原点) → 世界，带 sourceScale
        Vector3 LH = hipsTransform.TransformPoint(sourceScale * lastFrame.leftHip);
        Vector3 LK = hipsTransform.TransformPoint(sourceScale * lastFrame.leftKnee);
        Vector3 LA = hipsTransform.TransformPoint(sourceScale * lastFrame.leftAnkle);
        Vector3 LT = hipsTransform.TransformPoint(sourceScale * lastFrame.leftToe);

        Vector3 RH = hipsTransform.TransformPoint(sourceScale * lastFrame.rightHip);
        Vector3 RK = hipsTransform.TransformPoint(sourceScale * lastFrame.rightKnee);
        Vector3 RA = hipsTransform.TransformPoint(sourceScale * lastFrame.rightAnkle);
        Vector3 RT = hipsTransform.TransformPoint(sourceScale * lastFrame.rightToe);

        // 位置平滑（世界坐标）
        if (positionSmoothing > 0f && hasPrev)
        {
            float a = Mathf.Clamp01(positionSmoothing);
            LH = Vector3.Lerp(prevWorld[0], LH, 1f - a);
            LK = Vector3.Lerp(prevWorld[1], LK, 1f - a);
            LA = Vector3.Lerp(prevWorld[2], LA, 1f - a);
            LT = Vector3.Lerp(prevWorld[3], LT, 1f - a);

            RH = Vector3.Lerp(prevWorld[4], RH, 1f - a);
            RK = Vector3.Lerp(prevWorld[5], RK, 1f - a);
            RA = Vector3.Lerp(prevWorld[6], RA, 1f - a);
            RT = Vector3.Lerp(prevWorld[7], RT, 1f - a);
        }
        prevWorld[0] = LH; prevWorld[1] = LK; prevWorld[2] = LA; prevWorld[3] = LT;
        prevWorld[4] = RH; prevWorld[5] = RK; prevWorld[6] = RA; prevWorld[7] = RT;
        hasPrev = true;

        // 画线
        SetLine(leftLegLine, new Vector3[] { LH, LK, LA, LT });
        SetLine(rightLegLine, new Vector3[] { RH, RK, RA, RT });

        // 放球
        SetSpheres(new Transform[] { leftHipSphere, leftKneeSphere, leftAnkleSphere, leftToeSphere },
                   new Vector3[] { LH, LK, LA, LT });
        SetSpheres(new Transform[] { rightHipSphere, rightKneeSphere, rightAnkleSphere, rightToeSphere },
                   new Vector3[] { RH, RK, RA, RT });
    }

    private void SetLine(LineRenderer line, Vector3[] points)
    {
        if (!line) return;
        line.useWorldSpace = true;
        line.positionCount = points.Length;
        line.SetPositions(points);
    }

    private void SetSpheres(Transform[] spheres, Vector3[] positions)
    {
        if (spheres == null) return;
        for (int i = 0; i < spheres.Length && i < positions.Length; i++)
        {
            if (spheres[i]) spheres[i].position = positions[i];
        }
    }

    private void SetupLine(LineRenderer lr, Color c)
    {
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.01f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = c;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
    }
}
