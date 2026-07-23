using UnityEngine;

/// <summary>
/// 投射物发射前的目标方向解算接口。
/// 旧版命名保留层；新项目应优先依赖 IUnitTargetingProvider。
/// 当前项目主链路已不再直接消费该接口，保留它仅用于兼容旧代码或旧资源引用。
/// 当前框架里只有 CharacterTargetingModule 这个旧命名兼容壳仍显式公开该接口。
/// </summary>
public interface IProjectileTargetingProvider : IUnitTargetingProvider
{
}
