/// <summary>
/// 框架侧单位行为定义最小聚合契约。
/// 新项目若要把行为编辑器接入任意单位配置资产，优先实现这一接口。
/// </summary>
public interface IUnitBehaviorDefinition : IUnitDefinition, ICharacterBehaviorCatalog
{
}
