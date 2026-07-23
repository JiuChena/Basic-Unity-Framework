using BehaviorCore;

/// <summary>
/// 角色行为目录契约。
/// 任意项目都可以用自己的资产模型实现这套查询能力，而不要求运行时绑死某个具体 ScriptableObject。
/// </summary>
public interface ICharacterBehaviorCatalog
{
    BehaviorClip GetBehavior(string key, int clipIndex = 0);
    BehaviorClip[] GetBehaviorGroup(string key);
    bool HasBehavior(string key);
}
