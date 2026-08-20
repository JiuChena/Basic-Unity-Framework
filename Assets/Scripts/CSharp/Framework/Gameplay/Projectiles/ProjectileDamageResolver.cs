using UnityEngine;

/// <summary>
/// 通用伤害结算工具。
/// 当前同时被投射物与行为命中流程复用。
/// </summary>
public static class ProjectileDamageResolver
{
    public static float CalculateDamage(StatusData attacker, StatusData defender, float multiplier, string numericKey)
    {
        if (attacker == null || defender == null)
            return 0f;

        float resolvedMultiplier = ResolveNumericValue(attacker, numericKey, multiplier);
        float rawDamage = attacker.AttackPower * resolvedMultiplier;
        float damageWithBonus = rawDamage * (1f + Mathf.Max(0f, attacker.DamageBonus));
        float effectiveDefense = defender.Defense * (1f - Mathf.Clamp01(attacker.Penetration));
        return Mathf.Max(1f, damageWithBonus - effectiveDefense);
    }

    public static float ResolveNumericValue(StatusData ownerData, string numericKey, float fallbackValue)
    {
        if (ownerData == null || string.IsNullOrWhiteSpace(numericKey))
            return fallbackValue;

        UnitDriverBase ownerDriver = ownerData.Driver;
        if (ownerDriver == null)
            return fallbackValue;

        return ownerDriver.TryResolveNumericValue(numericKey, out float resolvedValue) ? resolvedValue : fallbackValue;
    }
}
