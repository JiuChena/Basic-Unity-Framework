using System;
using UnityEngine;

/// <summary>
/// 单位当前效果的只读展示快照。用于 Inspector 调试、UI 列表和状态图标刷新。
/// </summary>
[Serializable]
public sealed class UnitEffectViewData
{
    [Tooltip("当前效果的调试标识")]
    public string effectKey = string.Empty;

    [Tooltip("当前效果资源名，便于在项目中定位具体定义")]
    public string assetName = string.Empty;

    [Tooltip("施加该效果的来源对象名称")]
    public string sourceName = string.Empty;

    [Tooltip("剩余持续时间，单位为秒。瞬时效果通常为 0")]
    public float remainingDuration;

    [Tooltip("已完成的 Tick 次数")]
    public int completedTickCount;

    [Tooltip("总 Tick 次数")]
    public int totalTickCount;

    [Tooltip("当前实例运行时 ID，仅用于调试区分同类效果")]
    public int runtimeId;

    internal void UpdateFrom(RuntimeEffectInstance instance)
    {
        if (instance == null)
        {
            effectKey = string.Empty;
            assetName = string.Empty;
            sourceName = string.Empty;
            remainingDuration = 0f;
            completedTickCount = 0;
            totalTickCount = 0;
            runtimeId = 0;
            return;
        }

        effectKey = instance.DebugEffectKey;
        assetName = instance.Definition != null ? instance.Definition.name : string.Empty;
        sourceName = instance.SourceController != null ? instance.SourceController.name : string.Empty;
        remainingDuration = instance.RemainingDuration;
        completedTickCount = instance.CompletedTickCount;
        totalTickCount = instance.TotalTickCount;
        runtimeId = instance.RuntimeId;
    }
}
