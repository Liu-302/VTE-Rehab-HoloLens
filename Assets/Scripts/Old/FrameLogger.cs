using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 负责监听 LegEulerCalculator 的每帧角度事件，将数据实时记录下来（内存存储模拟）
/// </summary>
public class FrameLogger : MonoBehaviour
{
    [SerializeField] private LegEulerCalculator angleProvider;

    // 存储所有帧数据
    private List<LegEulerFrame> recordedFrames = new List<LegEulerFrame>();

    private void OnEnable()
    {
        if (angleProvider == null)
        {
            Debug.LogError("[FrameLogger] angleProvider 未赋值，脚本禁用");
            enabled = false;
            return;
        }

        angleProvider.OnEulerUpdated += OnEulerUpdatedHandler;
    }

    private void OnDisable()
    {
        if (angleProvider != null)
        {
            angleProvider.OnEulerUpdated -= OnEulerUpdatedHandler;
        }
    }

    private void OnEulerUpdatedHandler(LegEulerFrame frame)
    {
        // 可在此做校验/过滤逻辑
        recordedFrames.Add(frame);
    }

    /// <summary>
    /// 获取所有记录的帧数据（只读副本）
    /// </summary>
    public IReadOnlyList<LegEulerFrame> GetAllRecordedFrames()
    {
        return recordedFrames.AsReadOnly();
    }

    /// <summary>
    /// 清空所有记录
    /// </summary>
    public void ClearAllRecords()
    {
        recordedFrames.Clear();
    }

    /// <summary>
    /// 将当前数据导出为文本格式（示例）
    /// </summary>
    public string ExportRecordsToText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var f in recordedFrames)
        {
            sb.AppendLine(string.Format(
                "{0:O}, LT:{1:F2}, RT:{2:F2}, LC:{3:F2}, RC:{4:F2}, LF_Pitch:{5:F2}, RF_Pitch:{6:F2}, LF_Yaw:{7:F2}, RF_Yaw:{8:F2}",
                f.timestamp,
                f.leftThigh.x, f.rightThigh.x,
                f.leftCalf.x, f.rightCalf.x,
                f.leftFoot.x, f.rightFoot.x,
                f.leftFoot.y, f.rightFoot.y));
        }
        return sb.ToString();
    }
}
