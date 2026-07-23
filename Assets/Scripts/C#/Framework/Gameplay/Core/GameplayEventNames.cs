/// <summary>
/// Gameplay 公开使用的字符串事件名。
/// 先由玩法层持有这些约定，避免业务语义完全依附于底层事件系统实现。
/// </summary>
public static class GameplayEventNames
{
    public const string UnitDeath = "UnitDeath";
}
