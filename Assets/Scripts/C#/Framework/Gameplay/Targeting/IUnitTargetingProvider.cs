using UnityEngine;

/// <summary>
/// 投射物发射前的目标方向解算接口。
/// 新项目应优先依赖该中性命名入口；旧项目可继续通过 IProjectileTargetingProvider 兼容接入。
/// </summary>
public interface IUnitTargetingProvider
{
    bool TryResolveProjectileTargeting(StatusData ownerData, Vector3 spawnPosition, Vector3 fallbackDirection,
        out ProjectileTargetingResult result, int targetingScopeId = 0);
}
