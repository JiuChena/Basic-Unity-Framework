using System;
using UnityEngine;

namespace BehaviorCore
{
    /// <summary>
    /// 行为事件类型。
    /// </summary>
    public enum BehaviorEventType
    {
        SpawnVFX = 0,
        PlayAudio = 1,
        SpawnProjectile = 2,
        ApplyBuff = 3,
        ApplySelfBuff = 4,
        ExecuteGameplayEffect = 5,
        CameraShake = 6,
        SetObjectActive = 7,
    }
}
