using UnityEngine;

/// <summary>
/// 投射物发射前的目标解算结果。
/// </summary>
public struct ProjectileTargetingResult
{
    public Transform targetTransform;
    public StatusData targetData;
    public Collider targetCollider;
    public Vector3 aimPoint;
    public Vector3 launchDirection;
    public bool usesLockedSnapshot;
}
