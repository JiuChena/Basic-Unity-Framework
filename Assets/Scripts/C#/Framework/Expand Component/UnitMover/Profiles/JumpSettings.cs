using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 定义普通单次跳跃的通用手感参数。
    /// </summary>
    [Serializable]
    public sealed class JumpSettings
    {
        [Tooltip("是否启用普通跳跃能力")]
        [SerializeField] private bool _enabled = true;
        [Tooltip("起跳时的初始向上速度，单位：米/秒")]
        [Min(0f)] [SerializeField] private float _initialSpeed = 8f;
        [Tooltip("离开稳定地面后仍允许起跳的时间，单位：秒")]
        [Min(0f)] [SerializeField] private float _coyoteTime = 0.1f;
        [Tooltip("落地前缓存跳跃请求的时间，单位：秒")]
        [Min(0f)] [SerializeField] private float _bufferTime = 0.12f;
        [Tooltip("提前松开跳跃键时保留的上升速度比例，范围：0-1")]
        [Range(0f, 1f)] [SerializeField] private float _cutMultiplier = 0.5f;

        /// <summary>获取普通跳跃是否启用。</summary>
        public bool Enabled => _enabled;
        /// <summary>获取初始起跳速度。</summary>
        public float InitialSpeed => _initialSpeed;
        /// <summary>获取土狼时间。</summary>
        public float CoyoteTime => _coyoteTime;
        /// <summary>获取跳跃请求缓存时间。</summary>
        public float BufferTime => _bufferTime;
        /// <summary>获取跳跃截断速度比例。</summary>
        public float CutMultiplier => _cutMultiplier;
    }
}
