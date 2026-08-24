/// <summary>
/// 旧版角色命名的索敌组件兼容壳。
/// 新项目应优先挂载 UnitTargetingModule。
/// 当前项目主链路已不再直接引用该类型，仅为旧场景/旧预制体保留。
/// </summary>
public class CharacterTargetingModule : PlayerTargetingModule, IProjectileTargetingProvider
{
}
