/// <summary>
/// 旧版角色命名的阵营兼容枚举。
/// 新项目应优先使用 UnitAlignment。
/// 当前项目主链路已不再把它作为主字段类型使用，仅保留给兼容访问口。
/// </summary>
public enum CharacterAlignment
{
    Friendly,
    Enemy,
    Neutral,
}
