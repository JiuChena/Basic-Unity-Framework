using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 定义接地检测、坡面限制和悬浮弹簧参数。
    /// </summary>
    [Serializable]
    public sealed class GroundSettings
    {
        // 常规悬浮高度的框架级上限，避免将角色支撑到不符合接地语义的高度。
        private const float MaximumHoverHeight = 0.5f;

        [Tooltip("参与地面检测和支撑判断的物理层")]
        [SerializeField] private LayerMask _groundLayer = ~0;
        [Tooltip("允许作为可行走地面的最大坡度，单位：度")]
        [Range(0f, 89f)] [SerializeField] private float _slopeLimit = 45f;
        [Tooltip("有效支撑形状底部额外保持的悬浮距离，单位：米，最大为 0.5 米")]
        [Range(0f, MaximumHoverHeight)] [SerializeField] private float _hoverHeight = 0.05f;
        [Tooltip("悬浮距离之外额外用于接地检测的长度，单位：米")]
        [Min(0f)] [SerializeField] private float _probeDistance = 0.3f;
        [Tooltip("悬浮弹簧回正强度")]
        [Min(0f)] [SerializeField] private float _springStrength = 90f;
        [Tooltip("悬浮弹簧沿地面法线的阻尼")]
        [Min(0f)] [SerializeField] private float _springDamping = 14f;

        /// <summary>获取地面层掩码。</summary>
        public LayerMask GroundLayer => _groundLayer;
        /// <summary>获取最大可行走坡度。</summary>
        public float SlopeLimit => _slopeLimit;
        /// <summary>获取期望悬浮高度。</summary>
        public float HoverHeight => Mathf.Clamp(_hoverHeight, 0f, MaximumHoverHeight);
        /// <summary>获取额外地面探测长度。</summary>
        public float ProbeDistance => _probeDistance;
        /// <summary>获取悬浮弹簧强度。</summary>
        public float SpringStrength => _springStrength;
        /// <summary>获取悬浮弹簧阻尼。</summary>
        public float SpringDamping => _springDamping;
    }
}
