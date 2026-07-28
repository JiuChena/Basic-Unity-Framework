using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 定义 UnitMover 接管刚体后使用的重力参数。
    /// </summary>
    [Serializable]
    public sealed class GravitySettings
    {
        [Tooltip("基础重力倍率，乘以项目 Physics.gravity")]
        [Min(0f)] [SerializeField] private float _multiplier = 1f;
        [Tooltip("下落阶段额外使用的重力倍率")]
        [Min(1f)] [SerializeField] private float _fallMultiplier = 1.5f;
        [Tooltip("最大下落速度绝对值，单位：米/秒")]
        [Min(0f)] [SerializeField] private float _maxFallSpeed = 25f;

        /// <summary>获取基础重力倍率。</summary>
        public float Multiplier => _multiplier;
        /// <summary>获取下落阶段重力倍率。</summary>
        public float FallMultiplier => _fallMultiplier;
        /// <summary>获取最大下落速度。</summary>
        public float MaxFallSpeed => _maxFallSpeed;
    }
}
