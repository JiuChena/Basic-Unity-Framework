using UnityEngine;

/// <summary>
/// 鍗曚綅杩愯鏃剁湡姝ｉ渶瑕佺殑闈欐€佸畾涔夊绾︺€?/// 鏂伴」鐩簲浼樺厛渚濊禆杩欎竴灞備腑鎬у懡鍚嶆帴鍙ｏ紝鑰屼笉鏄洿鎺ュ啓姝?Character 璇箟銆?/// </summary>
public interface IUnitDefinition
{
    int UnitId { get; }
    UnitAlignment UnitAlignment { get; }
    float MoveSpeed { get; }
    float MaxEnergy { get; }
    float BurstCost { get; }
    float TalentCooldown { get; }
    float BurstCooldown { get; }
    float BaseCritRate { get; }
    float BaseCritDamage { get; }
    float BaseDamageBonus { get; }
    float BasePenetration { get; }
    bool SupportsAttack { get; }
    bool SupportsTalent { get; }
    bool SupportsBurst { get; }
    bool SupportsReload { get; }
    bool SupportsCrouch { get; }
    bool SupportsJump { get; }
    LayerMask HitboxTargetLayers { get; }
    IUnitNumericResolver NumericResolver { get; }

    bool TryResolveNumericValue(string numericKey, IUnitAbilityLevelProvider levelProvider, out float value);
    float ResolveBaseHealth(int level);
    float ResolveBaseAttack(int level);
    float ResolveBaseDefense(int level);
}
