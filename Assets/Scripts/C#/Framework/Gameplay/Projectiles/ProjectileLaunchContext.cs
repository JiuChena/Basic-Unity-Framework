using UnityEngine;

/// <summary>
/// 投射物发射初始化上下文。
/// </summary>
public struct ProjectileLaunchContext
{
    public StatusData ownerData;
    public float damageMultiplier;
    public string numericKey;
    public Vector3 position;
    public Quaternion rotation;
    public float defaultSpeed;
    public bool hasTarget;
    public Transform targetTransform;
    public Vector3 aimPoint;
    public Vector3 launchDirection;
}
