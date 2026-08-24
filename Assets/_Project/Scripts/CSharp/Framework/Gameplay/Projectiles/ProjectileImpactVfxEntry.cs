using System;
using UnityEngine;

/// <summary>
/// 子弹命中特效映射项。
/// 以层级掩码为键，匹配到对应层时生成指定特效。
/// </summary>
[Serializable]
public sealed class ProjectileImpactVfxEntry
{
    [Tooltip("命中的目标层级掩码。只要命中层位于该掩码中，就会触发对应特效。")]
    public LayerMask targetLayers = ~0;

    [Tooltip("命中后生成的特效预制体。")]
    public GameObject impactVfxPrefab;

    [Tooltip("特效自动回收时间，单位秒。")]
    [Min(0.01f)]
    public float autoRecycleTime = 1f;
}
