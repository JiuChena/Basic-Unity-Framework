using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// Provides ability levels for characters, enemies, and other runtime units.
/// </summary>
[MovedFrom(false, null, null, "ICharacterAbilityLevelProvider")]
public interface IUnitAbilityLevelProvider
{
    int GetAbilityLevel(UnitAbilityLevelGroup levelGroup);
}
