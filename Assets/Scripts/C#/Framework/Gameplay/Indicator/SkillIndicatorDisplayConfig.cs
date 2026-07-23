using System;
using UnityEngine;

[Serializable]
public sealed class SkillIndicatorDisplayConfig
{
    [Tooltip("是否启用该技能指示器。")] public bool enabled;
    [Tooltip("启用后，按住技能键期间只显示指示器，松开按键时才把这次输入提交给技能状态机。")] public bool triggerOnRelease = true;
    [Tooltip("技能指示器类型。")] public IndicatorType type = IndicatorType.Sector;
    [Tooltip("准星射线允许命中的层级。命中后会以命中点作为指示器瞄准点。")] public LayerMask aimLayers = Physics.DefaultRaycastLayers;
    [Tooltip("准星射线的最大检测距离，单位为米。")] [Min(0f)] public float aimRaycastDistance = 100f;
    [Tooltip("准星射线未命中时使用的回退距离，单位为米。小于等于 0 时自动按当前指示器尺寸推导。")] [Min(0f)] public float fallbackDistance;
    [Tooltip("指示器离地偏移，单位为米。")] [Min(0f)] public float surfaceOffset = 0.02f;
    [Tooltip("投掷型外圈相对主圈的额外离地偏移，单位为米。")] [Min(0f)] public float secondarySurfaceOffset = 0.005f;
    [Tooltip("圆弧采样段数，越高越圆滑，但顶点数也会更多。")] [Range(6, 128)] public int arcSegments = 36;
    [Tooltip("是否高亮当前技能范围内的有效目标。")] public bool highlightTargets = true;
    [ColorUsage(false, true), Tooltip("当前技能范围内目标的高亮显示颜色。")] public Color targetHighlightColor = new Color(1f, 0.92f, 0.5f, 0.55f);
    [Tooltip("扇形半径，单位为米。")] [Min(0f)] public float sectorRadius = 5f;
    [Tooltip("扇形张角，单位为度。")] [Range(1f, 360f)] public float sectorAngle = 90f;
    [Tooltip("指向性技能长度，单位为米。")] [Min(0f)] public float directionLength = 6f;
    [Tooltip("指向性技能宽度，单位为米。")] [Min(0f)] public float directionWidth = 2f;
    [Tooltip("投掷型技能可投掷的最大距离，单位为米。")] [Min(0f)] public float throwableMaxDistance = 8f;
    [Tooltip("投掷型技能落点范围半径，单位为米。")] [Min(0f)] public float throwableAreaRadius = 2f;

    public bool IsEnabled => enabled;

    public void Sanitize()
    {
        arcSegments = Mathf.Clamp(arcSegments, 6, 128);
        aimRaycastDistance = Mathf.Max(0f, aimRaycastDistance);
        fallbackDistance = Mathf.Max(0f, fallbackDistance);
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
        secondarySurfaceOffset = Mathf.Max(0f, secondarySurfaceOffset);
        sectorRadius = Mathf.Max(0f, sectorRadius);
        sectorAngle = Mathf.Clamp(sectorAngle, 1f, 360f);
        directionLength = Mathf.Max(0f, directionLength);
        directionWidth = Mathf.Max(0f, directionWidth);
        throwableMaxDistance = Mathf.Max(0f, throwableMaxDistance);
        throwableAreaRadius = Mathf.Max(0f, throwableAreaRadius);
    }

    public float ResolveFallbackDistance()
    {
        if (fallbackDistance > 0f)
            return fallbackDistance;

        return type switch
        {
            IndicatorType.Directionality => Mathf.Max(1f, directionLength),
            IndicatorType.Throwable => Mathf.Max(1f, throwableMaxDistance),
            _ => Mathf.Max(1f, sectorRadius),
        };
    }
}
