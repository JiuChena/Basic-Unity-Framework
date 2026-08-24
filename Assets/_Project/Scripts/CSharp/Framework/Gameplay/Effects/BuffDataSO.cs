using System;
using UnityEngine;

/// <summary>
/// 基础属性修饰效果定义。
/// 当前覆盖生命/攻击/防御的固定值与百分比修正，并支持额外免伤百分比。
/// </summary>
[CreateAssetMenu(fileName = "BuffData", menuName = "Framework/Gameplay/Effects/Buff Data")]
public class BuffDataSO : EffectDefinitionSO
{
    [Header("Identity")]
    [Tooltip("效果的唯一标识，便于调试、显示和定位")]
    public string buffId;

    [Space(8)]
    [Header("Duration")]
    [Tooltip("效果持续时间，单位为秒。小于等于 0 时只会执行一次开始/结束")]
    [Min(0f)]
    public float duration = 1f;

    [Space(8)]
    [Header("Flat Modifiers")]
    [Tooltip("效果生效期间附加的生命固定值")]
    public float healthFlatBonus;

    [Tooltip("效果生效期间附加的攻击固定值")]
    public float attackFlatBonus;

    [Tooltip("效果生效期间附加的防御固定值")]
    public float defenseFlatBonus;

    [Space(8)]
    [Header("Percent Modifiers")]
    [Tooltip("效果生效期间附加的生命百分比修正，0.2 代表最终生命公式中的百分比项直接 +0.2")]
    public float healthPercentBonus;

    [Tooltip("效果生效期间附加的攻击百分比修正，0.2 代表最终攻击公式中的百分比项直接 +0.2")]
    public float attackPercentBonus;

    [Tooltip("效果生效期间附加的防御百分比修正，0.2 代表最终防御公式中的百分比项直接 +0.2")]
    public float defensePercentBonus;

    [Space(8)]
    [Header("Secondary Stats")]
    [Tooltip("效果生效期间附加的暴击率修正，0.15 代表当前暴击率直接 +0.15")]
    public float critRateBonus;

    [Tooltip("效果生效期间附加的暴击伤害倍率修正，0.2 代表当前暴击伤害倍率直接 +0.2")]
    public float critDamageBonus;

    [Tooltip("效果生效期间附加的伤害提升倍率修正，0.2 代表当前伤害提升直接 +0.2")]
    public float damageBonus;

    [Tooltip("效果生效期间附加的穿透修正，0.1 代表当前穿透直接 +0.1")]
    public float penetrationBonus;

    [Space(8)]
    [Header("Mitigation")]
    [Tooltip("效果生效期间附加的免伤修正，0.2 代表当前免伤直接 +0.2")]
    [Range(0f, 1f)]
    public float damageReductionPercent;

    public override string EffectKey => string.IsNullOrWhiteSpace(buffId) ? name : buffId;

    public override EffectSchedule BuildSchedule(in EffectBuildContext context)
    {
        return new EffectSchedule(duration, 0, 0f, false);
    }

    public override void OnEffectStarted(EffectRuntimeContext context)
    {
        if (context?.TargetData == null)
            return;

        context.TargetData.AddPrimaryStatFlatModifier(healthFlatBonus, attackFlatBonus, defenseFlatBonus);
        context.TargetData.AddPrimaryStatPercentModifier(healthPercentBonus, attackPercentBonus, defensePercentBonus);
        context.TargetData.AddSecondaryStatModifier(critRateBonus, critDamageBonus, damageBonus, penetrationBonus);
        context.TargetData.AddDamageReductionModifier(damageReductionPercent);
    }

    public override void OnEffectEnded(EffectRuntimeContext context)
    {
        if (context?.TargetData == null)
            return;

        context.TargetData.RemovePrimaryStatFlatModifier(healthFlatBonus, attackFlatBonus, defenseFlatBonus);
        context.TargetData.RemovePrimaryStatPercentModifier(healthPercentBonus, attackPercentBonus, defensePercentBonus);
        context.TargetData.RemoveSecondaryStatModifier(critRateBonus, critDamageBonus, damageBonus, penetrationBonus);
        context.TargetData.RemoveDamageReductionModifier(damageReductionPercent);
    }
}
