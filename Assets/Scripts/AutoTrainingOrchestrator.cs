using UnityEngine;
using System.Collections;

public class AutoTrainingOrchestrator : MonoBehaviour
{
    // ====== 数据播放器 / 可视化 ======
    [Header("Players / Visuals")]
    [Tooltip("播放 All Data → 推到 sourceBus（供折线订阅）")]
    public CollectGuideFromFiles_AllData allDataPlayer;   // 线条的数据源（20帧循环）
    [Tooltip("用于驱动人物模型骨骼（加载 Max Data 单帧）")]
    public TargetMaxPosePlayer targetPlayer;              // 人物模型（Max Data单帧）
    [Tooltip("实时腿折线（订 sourceBus）：用于显示/隐藏折线")]
    public Behaviour realtimeLines;                       // e.g. LegLineRenderer

    [Header("（可选）比例同步")]
    [Tooltip("若指定，会把 targetPlayer.sourceScale 同步给折线渲染脚本（如 LegLineRenderer）")]
    public LegLineRenderer lineRendererToSyncScale;

    [Header("（可选）采集期驱动器")]
    [Tooltip("如果采集阶段用 LegModelDriver 驱动模型，这里拖它；训练开始时会自动 StopRendering 以免与 TargetMaxPosePlayer 抢骨骼")]
    public LegModelDriver collectPhaseModelDriver;

    // ====== 统一参数：每个标准动作由两个极值组成（前段/后段） ======
    [Header("Target 切换帧段（与 All Data 对齐）")]
    [Tooltip("前段帧数（第一极值保持的帧数）")]
    [Min(1)] public int firstSpanFrames = 10;
    [Tooltip("后段帧数（第二极值保持的帧数）")]
    [Min(1)] public int secondSpanFrames = 10;

    [Tooltip("All Data 播放帧率；为 0 则使用 allDataPlayer.fps；两边会按该 fps 同步")]
    public float overrideFps = 0f;

    [Header("每个动作循环次数（每次=20帧）")]
    [Min(1)] public int cyclesPerAction = 3;

    // ====== 运行期状态 ======
    private bool isTraining = false;
    private Coroutine runSequenceCo;
    private Coroutine targetSequenceCo;

    // ===================== 公有入口 =====================
    public void StartTraining()
    {
        if (isTraining) return;
        isTraining = true;

        // 0) 防止骨骼冲突
        if (collectPhaseModelDriver) collectPhaseModelDriver.StopRendering();

        // 1) 打开线条显示
        if (realtimeLines) realtimeLines.gameObject.SetActive(true);

        // 2) 启动顺序编排
        if (runSequenceCo != null) StopCoroutine(runSequenceCo);
        runSequenceCo = StartCoroutine(RunAllActionsSequence());

        Debug.Log("[Orchestrator] StartTraining：开始顺序执行 5 个动作（每个 3 次 × 20 帧），Target 与 Realtime 同步。");
    }

    public void StopTraining()
    {
        if (!isTraining) return;
        isTraining = false;

        // 停主序列
        if (runSequenceCo != null) { StopCoroutine(runSequenceCo); runSequenceCo = null; }
        // 停目标切换
        if (targetSequenceCo != null) { StopCoroutine(targetSequenceCo); targetSequenceCo = null; }

        // 停 All Data 推流 & 隐藏线条
        if (allDataPlayer) allDataPlayer.StopPlayback();
        if (realtimeLines) realtimeLines.gameObject.SetActive(false);

        // 关闭目标驱骨
        if (targetPlayer) targetPlayer.applyToModel = false;

        Debug.Log("[Orchestrator] StopTraining：已停止两路渲染与播放。");
    }

    // ===================== 主顺序：5 动作 × N 次 =====================
    private IEnumerator RunAllActionsSequence()
    {
        // 统一 fps
        float fps = overrideFps > 0f ? overrideFps :
                    (allDataPlayer != null && allDataPlayer.fps > 0f ? allDataPlayer.fps : 4f);

        // 每次动作的时长（秒）
        float oneCycleSeconds = 20f / Mathf.Max(0.0001f, fps);
        float actionSeconds = cyclesPerAction * oneCycleSeconds;

        // 固定顺序的 5 个动作定义（All Data 文件夹 + Max Data 两个极值的键）
        var actions = new (string folder, string keyA, string keyB)[]
        {
            // 1) 脚踝屈伸：先屈后伸（Max→Min）
            ("ankle_flex_ext",
             "SimulateAnkleFlexionExtension_FootPitchMax",
             "SimulateAnkleFlexionExtension_FootPitchMin"),

            // 2) 脚踝环绕：先 Max 后 Min
            ("ankle_circumduction",
             "SimulateAnkleCircumduction_FootPitchYawMax",
             "SimulateAnkleCircumduction_FootPitchYawMin"),

            // 3) 交替抬腿：先左 Max 后右 Max
            ("leg_raise",
             "SimulateRaiseLegAlternately_LeftThighPitchMax",
             "SimulateRaiseLegAlternately_RightThighPitchMax"),

            // 4) 交替屈脚：先左 Max 后右 Max
            ("ankle_alternating_flexion",
             "SimulateAnkleDorsiflexionAlternately_LeftFootPitchMax",
             "SimulateAnkleDorsiflexionAlternately_RightFootPitchMax"),

            // 5) 屈膝抬腿：先左 Max 后右 Max
            ("leg_combo_kneeflex_raise",
             "SimulateQuadricepsIsotonicExercise_LeftThighPitchMax",
             "SimulateQuadricepsIsotonicExercise_RightThighPitchMax"),
        };

        for (int i = 0; i < actions.Length && isTraining; i++)
        {
            var (folder, keyA, keyB) = actions[i];

            // —— Realtime：播放 All Data 的对应动作（20 帧循环）
            if (allDataPlayer)
            {
                allDataPlayer.PlayFolder(folder);
            }
            else
            {
                Debug.LogWarning($"[Orchestrator] allDataPlayer 未绑定，无法播放 All Data：{folder}");
            }

            // —— Target：在两极值之间切换（A 段 firstSpanFrames、B 段 secondSpanFrames）
            if (targetPlayer)
            {
                targetPlayer.applyToModel = true;

                // 开启/重启目标切换协程
                if (targetSequenceCo != null) StopCoroutine(targetSequenceCo);
                targetSequenceCo = StartCoroutine(TargetSequenceRoutine(keyA, keyB, fps));
            }
            else
            {
                Debug.LogWarning("[Orchestrator] targetPlayer 未绑定，无法渲染 Max Data 到模型。");
            }

            // —— 同步比例（可选）
            TrySyncLineRendererScale();

            // —— 等待本动作的 N 次循环时间
            yield return new WaitForSeconds(actionSeconds);

            // —— 每个动作结束后停一下 Target 切换（避免动作切换时刚好换极值）
            if (targetSequenceCo != null) { StopCoroutine(targetSequenceCo); targetSequenceCo = null; }
        }

        // 全部完成后自动停止
        StopTraining();
    }

    // ===================== 目标序列（两极值按帧段切换，循环） =====================
    private IEnumerator TargetSequenceRoutine(string keyA, string keyB, float fps)
    {
        if (firstSpanFrames <= 0) firstSpanFrames = 10;
        if (secondSpanFrames <= 0) secondSpanFrames = 10;

        var waitA = new WaitForSeconds(firstSpanFrames / Mathf.Max(0.0001f, fps));
        var waitB = new WaitForSeconds(secondSpanFrames / Mathf.Max(0.0001f, fps));

        while (isTraining && targetPlayer != null)
        {
            if (!string.IsNullOrEmpty(keyA)) targetPlayer.ShowByKey(keyA);
            yield return waitA;

            if (!string.IsNullOrEmpty(keyB)) targetPlayer.ShowByKey(keyB);
            yield return waitB;
        }
    }

    // ===================== 小工具 =====================
    private void TrySyncLineRendererScale()
    {
        if (lineRendererToSyncScale && targetPlayer)
            lineRendererToSyncScale.sourceScale = targetPlayer.sourceScale;
    }
}
