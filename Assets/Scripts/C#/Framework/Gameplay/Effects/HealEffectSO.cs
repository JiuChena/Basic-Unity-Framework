using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 专用治疗效果定义。用于单体治疗、群体治疗以及后台持续治疗。
/// </summary>
[CreateAssetMenu(fileName = "HealEffect", menuName = "Framework/Gameplay/Effects/Heal Effect")]
public class HealEffectSO : EffectDefinitionSO
{
    [Header("Identity")]
    [Tooltip("治疗效果唯一标识，便于调试、日志和后续展示")]
    public string effectId;

    [Space(8)]
    [Header("Targeting")]
    [Tooltip("治疗目标选择模式。单体治疗建议用 ExplicitTarget，群体治疗建议用 AllAllies 或 AlliesInRadius")]
    public HealEffectTargetMode targetMode = HealEffectTargetMode.Self;

    [Tooltip("范围群体治疗使用的半径，单位为米")]
    [Min(0f)]
    public float radius = 5f;

    [Tooltip("群体治疗时是否包含施法者自己")]
    public bool includeSelf = true;

    [Tooltip("当目标模式为 ExplicitTarget 但请求里没有显式目标时，是否回退到施法者自己")]
    public bool fallbackToSelfWhenNoExplicitTarget = true;

    [Space(8)]
    [Header("Execution")]
    [Tooltip("总治疗触发次数。1 表示瞬时治疗，大于 1 表示按间隔重复治疗")]
    [Min(1)]
    public int tickCount = 1;

    [Tooltip("重复治疗间隔，单位为秒。tickCount 为 1 时通常填 0")]
    [Min(0f)]
    public float tickInterval;

    [Space(8)]
    [Header("Value")]
    [Tooltip("治疗固定基础值。最终值 = 固定基础值 + 等级数值 + 来源属性 * 等级倍率")]
    public float baseHealValue;

    [Tooltip("治疗等级数值引用的技能数值条目 key。配置后会从项目数值定义表读取当前等级的数值，并与固定基础值相加")]
    public string baseHealNumericKey;

    [Tooltip("治疗量绑定的施法者来源属性")]
    public GameplayEffectScalingStat scalingStat = GameplayEffectScalingStat.None;

    [Tooltip("治疗倍率引用的技能数值条目 key。配置后会从项目数值定义表读取当前等级的倍率")]
    public string scalingMultiplierNumericKey;

    public override string EffectKey => string.IsNullOrWhiteSpace(effectId) ? name : effectId;

    public override void ResolveTargets(GlobalEffectSystem system, in EffectApplyRequest request,
        List<UnitEffectController> results)
    {
        if (system == null || results == null)
            return;

        UnitEffectController sourceController = request.SourceController != null
            ? request.SourceController
            : request.ExplicitTargetController;
        if (sourceController == null || sourceController.DataPanel == null)
            return;

        switch (targetMode)
        {
            case HealEffectTargetMode.ExplicitTarget:
                ResolveExplicitTarget(request, sourceController, results);
                break;

            case HealEffectTargetMode.Self:
                AddTargetIfValid(results, sourceController);
                break;

            case HealEffectTargetMode.AllAllies:
                ResolveAllAllies(system, sourceController, results);
                break;

            case HealEffectTargetMode.AlliesInRadius:
                ResolveAlliesInRadius(system, sourceController, request.Origin, results);
                break;
        }
    }

    public override EffectSchedule BuildSchedule(in EffectBuildContext context)
    {
        int resolvedTickCount = Mathf.Max(1, tickCount);
        float resolvedTickInterval = Mathf.Max(0f, tickInterval);
        float resolvedDuration = resolvedTickCount > 1 ? resolvedTickInterval * (resolvedTickCount - 1) : 0f;
        return new EffectSchedule(resolvedDuration, resolvedTickCount, resolvedTickInterval, true);
    }

    public override void OnEffectTick(EffectRuntimeContext context)
    {
        if (context?.TargetData == null)
            return;

        float healValue = ResolveHealValue(context);
        context.TargetData.RestoreHealth(healValue);
    }

    private void ResolveExplicitTarget(in EffectApplyRequest request, UnitEffectController sourceController,
        List<UnitEffectController> results)
    {
        UnitEffectController explicitTarget = request.ExplicitTargetController;
        if (explicitTarget != null)
        {
            AddTargetIfValid(results, explicitTarget);
            return;
        }

        if (fallbackToSelfWhenNoExplicitTarget)
            AddTargetIfValid(results, sourceController);
    }

    private void ResolveAllAllies(GlobalEffectSystem system, UnitEffectController sourceController,
        List<UnitEffectController> results)
    {
        IReadOnlyList<UnitEffectController> registeredUnits = system.RegisteredUnits;
        for (int i = 0; i < registeredUnits.Count; i++)
        {
            UnitEffectController targetController = registeredUnits[i];
            if (!CanHealAlly(sourceController, targetController))
                continue;

            if (!includeSelf && targetController == sourceController)
                continue;

            AddTargetIfValid(results, targetController);
        }
    }

    private void ResolveAlliesInRadius(GlobalEffectSystem system, UnitEffectController sourceController, Vector3 origin,
        List<UnitEffectController> results)
    {
        IReadOnlyList<UnitEffectController> registeredUnits = system.RegisteredUnits;
        float sqrRadius = radius * radius;
        for (int i = 0; i < registeredUnits.Count; i++)
        {
            UnitEffectController targetController = registeredUnits[i];
            if (!CanHealAlly(sourceController, targetController))
                continue;

            if (!includeSelf && targetController == sourceController)
                continue;

            Vector3 offset = targetController.transform.position - origin;
            if (offset.sqrMagnitude > sqrRadius)
                continue;

            AddTargetIfValid(results, targetController);
        }
    }

    private float ResolveHealValue(EffectRuntimeContext context)
    {
        float resolvedLeveledValue = context.ResolveNumericValue(baseHealNumericKey, 0f);
        float resolvedScalingMultiplier = context.ResolveNumericValue(scalingMultiplierNumericKey, 0f);
        float sourceStatValue = context.GetSourceStat(scalingStat);
        return Mathf.Max(0f, baseHealValue + resolvedLeveledValue + sourceStatValue * resolvedScalingMultiplier);
    }

    private static bool CanHealAlly(UnitEffectController sourceController, UnitEffectController targetController)
    {
        return sourceController != null &&
               targetController != null &&
               sourceController.DataPanel != null &&
               targetController.DataPanel != null &&
               sourceController.DataPanel.UnitAlignment == targetController.DataPanel.UnitAlignment;
    }

    private void OnValidate()
    {
        if (tickCount < 1)
            tickCount = 1;

        if (tickInterval < 0f)
            tickInterval = 0f;

        if (radius < 0f)
            radius = 0f;
    }
}
