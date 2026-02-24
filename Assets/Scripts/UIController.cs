using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [Header("Buttons")]
    public Button startButton;            // Start Collecting (All Data → Model)
    public Button stopButton;             // Stop Collecting
    public Button startTrainingButton;    // Start Training (AutoTrainingOrchestrator)
    public Button stopTrainingButton;     // Stop Training

    public Button ankleFlexionButton;     // -> ankle_flex_ext
    public Button ankleCircumductionButton;
    public Button raiseLegButton;         // -> leg_raise
    public Button dorsiflexionButton;     // -> ankle_alternating_flexion
    public Button quadricepsButton;       // -> leg_combo_kneeflex_raise
    public Button initialStateButton;     // -> initial_state

    [Header("Players / Visuals")]
    [Tooltip("播放 All Data → 推到 sourceBus（采集阶段用于驱动人物模型）")]
    public CollectGuideFromFiles_AllData allDataPlayer;
    [Tooltip("训练阶段入口（内部已分离 All Data 画线 & Max Data 驱骨）")]
    public AutoTrainingOrchestrator trainer;

    [Header("Collect Phase Model Driver")]
    [Tooltip("采集阶段驱动人物模型（bus 要与 allDataPlayer.bus 相同）")]
    public LegModelDriver collectModelDriver;

    [Header("Realtime Lines (可选)")]
    [Tooltip("订 sourceBus 的折线（采集阶段不显示；训练由 orchestrator 控制）")]
    public LegLineRenderer realtimeLines;

    private enum ActionChoice
    {
        None,
        InitialState,
        AnkleFlexExt,
        AnkleCircumduction,
        AnkleAlternatingFlexion,
        LegRaise,
        QuadKneeFlexRaise
    }
    private ActionChoice selected = ActionChoice.None;
    private bool isCollecting = false;

    void Start()
    {
        // —— 动作选择按钮 —— //
        if (initialStateButton) initialStateButton.onClick.AddListener(() => SelectAction(ActionChoice.InitialState));
        if (ankleFlexionButton) ankleFlexionButton.onClick.AddListener(() => SelectAction(ActionChoice.AnkleFlexExt));
        if (ankleCircumductionButton) ankleCircumductionButton.onClick.AddListener(() => SelectAction(ActionChoice.AnkleCircumduction));
        if (dorsiflexionButton) dorsiflexionButton.onClick.AddListener(() => SelectAction(ActionChoice.AnkleAlternatingFlexion));
        if (raiseLegButton) raiseLegButton.onClick.AddListener(() => SelectAction(ActionChoice.LegRaise));
        if (quadricepsButton) quadricepsButton.onClick.AddListener(() => SelectAction(ActionChoice.QuadKneeFlexRaise));

        // —— 采集（All Data → 模型） —— //
        if (startButton) startButton.onClick.AddListener(OnStartCollectingClicked);
        if (stopButton) stopButton.onClick.AddListener(OnStopCollectingClicked);

        // —— 训练（由 Orchestrator 统一启动/停止） —— //
        if (startTrainingButton) startTrainingButton.onClick.AddListener(OnStartTrainingClicked);
        if (stopTrainingButton) stopTrainingButton.onClick.AddListener(OnStopTrainingClicked);

        RefreshUI();
    }

    // ========== 采集：选择动作（只记录选择，不立即播放） ==========
    void SelectAction(ActionChoice choice)
    {
        if (isCollecting)
        {
            Debug.LogWarning("[UIController] 正在播放，请先 Stop Collecting 再换动作。");
            return;
        }
        selected = choice;
        Debug.Log($"[UIController] 已选择动作：{selected}");
        RefreshUI();
    }

    // ========== 采集：Start（All Data → 模型；不画线） ==========
    void OnStartCollectingClicked()
    {
        if (!allDataPlayer)
        {
            Debug.LogWarning("[UIController] allDataPlayer 未绑定。");
            return;
        }
        if (!collectModelDriver)
        {
            Debug.LogWarning("[UIController] collectModelDriver 未绑定（采集阶段需要驱动人物模型）。");
            return;
        }
        if (selected == ActionChoice.None)
        {
            Debug.LogWarning("[UIController] 请先选择一个动作，再点击 Start Collecting。");
            return;
        }
        if (isCollecting)
        {
            Debug.Log("[UIController] 已在播放中。");
            return;
        }

        // 采集阶段不需要实时腿线条 → 关闭
        if (realtimeLines) realtimeLines.gameObject.SetActive(false);

        // 播放 All Data（推流到 PoseStreamBus）
        string folder = MapFolder(selected);
        allDataPlayer.PlayFolder(folder);

        // 启动人物模型驱动（订同一个 bus）
        collectModelDriver.StartRendering();

        isCollecting = true;
        Debug.Log($"[UIController] Start Collecting —— 播放 {folder}（All Data → 模型；不画线）");
        RefreshUI();
    }

    // ========== 采集：Stop ==========
    void OnStopCollectingClicked()
    {
        if (allDataPlayer) allDataPlayer.StopPlayback();
        if (collectModelDriver) collectModelDriver.StopRendering();

        // 采集阶段结束仍保持线条关闭（线条只在 Training 时由 orchestrator 控制）
        if (realtimeLines) realtimeLines.gameObject.SetActive(false);

        isCollecting = false;
        Debug.Log("[UIController] Stop Collecting —— 已停止 All Data 播放与模型驱动");
        RefreshUI();
    }

    // ========== 训练：Start（Orchestrator 内部分开两路） ==========
    void OnStartTrainingClicked()
    {
        if (!trainer)
        {
            Debug.LogWarning("[UIController] trainer 未绑定：请把 AutoTrainingOrchestrator 拖到 UIController.trainer。");
            return;
        }

        // 避免冲突：若还在采集，先停掉（否则会抢 PoseStreamBus 或骨骼）
        if (isCollecting) OnStopCollectingClicked();

        trainer.StartTraining();
        Debug.Log("[UIController] Start Training —— 已启动（All Data→线条；Max Data→模型）");
    }

    // ========== 训练：Stop ==========
    void OnStopTrainingClicked()
    {
        if (!trainer)
        {
            Debug.LogWarning("[UIController] trainer 未绑定。");
            return;
        }
        trainer.StopTraining();
        Debug.Log("[UIController] Stop Training —— 已停止");
    }

    // ========== UI 刷新 ==========
    void RefreshUI()
    {
        bool selectable = !isCollecting;

        if (initialStateButton) initialStateButton.interactable = selectable;
        if (ankleFlexionButton) ankleFlexionButton.interactable = selectable;
        if (ankleCircumductionButton) ankleCircumductionButton.interactable = selectable;
        if (dorsiflexionButton) dorsiflexionButton.interactable = selectable;
        if (raiseLegButton) raiseLegButton.interactable = selectable;
        if (quadricepsButton) quadricepsButton.interactable = selectable;

        if (startButton) startButton.interactable = (selected != ActionChoice.None) && !isCollecting;
        if (stopButton) stopButton.interactable = isCollecting;

        // 训练按钮不受采集状态限制
    }

    // 映射到 StreamingAssets/All Data/<folder>
    private static string MapFolder(ActionChoice c)
    {
        switch (c)
        {
            case ActionChoice.InitialState: return "initial_state";
            case ActionChoice.AnkleFlexExt: return "ankle_flex_ext";
            case ActionChoice.AnkleCircumduction: return "ankle_circumduction";
            case ActionChoice.AnkleAlternatingFlexion: return "ankle_alternating_flexion";
            case ActionChoice.LegRaise: return "leg_raise";
            case ActionChoice.QuadKneeFlexRaise: return "leg_combo_kneeflex_raise";
            default: return "initial_state";
        }
    }
}
