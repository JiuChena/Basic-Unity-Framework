using UnityEngine;

/// <summary>
/// 旧版角色命名的运行时静态定义契约。
/// 当前继承中性接口 IUnitDefinition，保留给现有项目和资源链路使用。
/// 当前项目主链路已不再直接消费该接口，新项目应直接实现 IUnitDefinition。
/// </summary>
public interface ICharacterUnitDefinition : IUnitDefinition
{
    int CharacterId { get; }
    CharacterAlignment Alignment { get; }
}
