using UnityEngine;
using System;

public class PoseStreamBus : MonoBehaviour
{
    public enum BusRole { Generic, Source, Target }

    [Header("标识/调试")]
    public BusRole role = BusRole.Generic;
    public string busName = "PoseBus";

    [Header("左腿（以约定原点的局部坐标）")]
    public Vector3 leftHip, leftKnee, leftAnkle, leftToe;

    [Header("右腿（以约定原点的局部坐标）")]
    public Vector3 rightHip, rightKnee, rightAnkle, rightToe;

    [Header("状态")]
    public bool isStreaming = false;

    public event Action<LegPoseFrame> OnPoseDataUpdated;

    public void StartStreaming()
    {
        isStreaming = true;
        Debug.Log($"[PoseStreamBus:{busName}/{role}] 开始推流");
    }

    public void StopStreaming()
    {
        isStreaming = false;
        Debug.Log($"[PoseStreamBus:{busName}/{role}] 停止推流");
    }

    public void PublishFrame(LegPoseFrame f, bool overwriteFields = true)
    {
        if (overwriteFields)
        {
            leftHip = f.leftHip; leftKnee = f.leftKnee; leftAnkle = f.leftAnkle; leftToe = f.leftToe;
            rightHip = f.rightHip; rightKnee = f.rightKnee; rightAnkle = f.rightAnkle; rightToe = f.rightToe;
        }
        OnPoseDataUpdated?.Invoke(f);
    }
}
