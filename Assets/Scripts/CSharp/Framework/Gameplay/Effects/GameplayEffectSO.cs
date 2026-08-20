using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用玩法效果定义。当前支持即时/重复治疗、回能和“套一层属性效果”三类能力。
/// 后续若具体单位需要更复杂逻辑，应直接继承 EffectDefinitionSO 编写专属效果脚本。
/// </summary>
[CreateAssetMenu(fileName = "GameplayEffect", menuName = "Framework/Gameplay/Effects/Gameplay Effect")]
public class GameplayEffectSO : EffectDefinitionSO
{
    [Header("Identity")]
    [Tooltip("效果唯一标识，便于调试定位和日志输出")]
    public string effectId;

    [Tooltip("该效果在运行时执行的类型")]
    public GameplayEffectType effectType = GameplayEffectType.Heal;

    [Space(8)]
    [Header("Targeting")]
    [Tooltip("该效果的目标筛选范围")]
    public GameplayEffectTargetScope targetScope = GameplayEffectTargetScope.Self;

    [Tooltip("范围类目标筛选使用的半径，单位为米")]
    [Min(0f)]
    public float radius = 5f;

    [Tooltip("群体效果是否包含施法者自己")]
    public bool includeSelf = true;

    [Space(8)]
    [Header("Execution")]
    [Tooltip("效果触发次数。大于 1 时会按间隔重复触发多次")]
    [Min(1)]
    public int tickCount = 1;

    [Tooltip("重复触发间隔，单位为秒。tickCount 为 1 时可忽略")]
    [Min(0f)]
    public float tickInterval;

    [Space(8)]
    [Header("Value")]
    [Tooltip("效果固定基础数值。最终值 = 固定基础值 + 等级数值 + 来源属性 * 等级倍率")]
    public float baseValue;

    [Tooltip("等级数值引用的技能数值条目 key。配置后会从项目数值定义表读取当前等级的数值，并与固定基础值相加")]
    public string baseValueNumericKey;

    [Tooltip("数值绑定的来源属性")]
    public GameplayEffectScalingStat scalingStat = GameplayEffectScalingStat.None;

    [Tooltip("来源属性倍率引用的技能数值条目 key。配置后会从项目数值定义表读取当前等级的倍率")]
    public string scalingMultiplierNumericKey;

    [Space(8)]
    [Header("Buff")]
    [Tooltip("当效果类型为 ApplyBuff 时，要额外施加到目标上的属性效果定义")]
    public BuffDataSO buffRef;

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

        IReadOnlyList<UnitEffectController> registeredUnits = system.RegisteredUnits;
        for (int i = 0; i < registeredUnits.Count; i++)
        {
            UnitEffectController targetController = registeredUnits[i];
            if (targetController == null || targetController.DataPanel == null)
                continue;

            if (!includeSelf && targetController == sourceController)
                continue;

            if (!MatchesTargetScope(sourceController, targetController, request.Origin))
                continue;

            AddTargetIfValid(results, targetController);
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
        if (context == null || context.TargetData == null)
            return;

        switch (effectType)
        {
            case GameplayEffectType.Heal:
                context.TargetData.RestoreHealth(ResolveFinalValue(context));
                break;

            case GameplayEffectType.RestoreEnergy:
                if (context.TargetDriver != null && context.TargetDriver.Resources != null)
                    context.TargetDriver.Resources.Gain(ResolveFinalValue(context));
                break;

            case GameplayEffectType.ApplyBuff:
                if (buffRef != null)
                    context.TryApplyNestedEffect(buffRef);
                break;
        }
    }

    private bool MatchesTargetScope(UnitEffectController sourceController, UnitEffectController targetController,
        Vector3 origin)
    {
        switch (targetScope)
        {
            case GameplayEffectTargetScope.Self:
                return targetController == sourceController;

            case GameplayEffectTargetScope.AllAllies:
                return IsAlly(sourceController, targetController);

            case GameplayEffectTargetScope.AllEnemies:
                return IsEnemy(sourceController, targetController);

            case GameplayEffectTargetScope.AlliesInRadius:
                return IsAlly(sourceController, targetController) &&
                       Vector3.Distance(origin, targetController.transform.position) <= radius;

            case GameplayEffectTargetScope.EnemiesInRadius:
                return IsEnemy(sourceController, targetController) &&
                       Vector3.Distance(origin, targetController.transform.position) <= radius;

            default:
                return false;
        }
    }

    private static bool IsAlly(UnitEffectController sourceController, UnitEffectController targetController)
    {
        return sourceController != null &&
               targetController != null &&
               sourceController.DataPanel != null &&
               targetController.DataPanel != null &&
               sourceController.DataPanel.UnitAlignment == targetController.DataPanel.UnitAlignment;
    }

    private static bool IsEnemy(UnitEffectController sourceController, UnitEffectController targetController)
    {
        if (sourceController == null || targetController == null ||
            sourceController.DataPanel == null || targetController.DataPanel == null)
        {
            return false;
        }

        UnitAlignment targetAlignment = targetController.DataPanel.UnitAlignment;
        return sourceController.DataPanel.UnitAlignment != targetAlignment &&
               targetAlignment != UnitAlignment.Neutral;
    }

    private float ResolveFinalValue(EffectRuntimeContext context)
    {
        float resolvedLeveledValue = context.ResolveNumericValue(baseValueNumericKey, 0f);
        float resolvedScalingMultiplier = context.ResolveNumericValue(scalingMultiplierNumericKey, 0f);
        float sourceStatValue = context.GetSourceStat(scalingStat);
        return Mathf.Max(0f, baseValue + resolvedLeveledValue + sourceStatValue * resolvedScalingMultiplier);
    }

    private void OnValidate()
    {
        if (tickCount < 1)
            tickCount = 1;

        if (radius < 0f)
            radius = 0f;

        if (tickInterval < 0f)
            tickInterval = 0f;

        if (effectType == GameplayEffectType.ApplyBuff && buffRef == null)
        {
            Debug.LogWarning($"[{name}] GameplayEffect 类型为 ApplyBuff，但 buffRef 为空。", this);
        }
    }
}
