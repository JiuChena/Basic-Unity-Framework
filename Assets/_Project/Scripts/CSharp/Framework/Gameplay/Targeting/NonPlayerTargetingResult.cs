using UnityEngine;

/// <summary>
/// 非玩家单位索敌结果。
/// 同时服务于敌人/中立单位的目标发现、攻击朝向和投射物发射。
/// </summary>
public struct NonPlayerTargetingResult
{
    public StatusData targetData;
    public Transform targetTransform;
    public Collider targetCollider;
    public Vector3 aimPoint;
    public Vector3 launchDirection;
    public float sqrDistance;
}
