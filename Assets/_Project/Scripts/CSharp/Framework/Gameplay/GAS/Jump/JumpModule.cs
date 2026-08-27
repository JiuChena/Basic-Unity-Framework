using System;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Movement
{
    /// <summary>
    /// 维护跳跃请求缓存、土狼时间和跳跃截断状态的纯 C# 模块。
    /// </summary>
    [Serializable]
    public sealed class JumpModule
    {
        // 是否启用普通跳跃能力。
        [Tooltip("是否启用普通跳跃能力")]
        [SerializeField] private bool _enabled = true;
        // 起跳时写入的初始向上速度。
        [Tooltip("起跳时的初始向上速度，单位：米/秒")]
        [Min(0f)] [SerializeField] private float _initialSpeed = 8f;
        // 离开稳定地面后仍允许起跳的时间。
        [Tooltip("离开稳定地面后仍允许起跳的时间，单位：秒")]
        [Min(0f)] [SerializeField] private float _coyoteTime = 0.1f;
        // 接收到跳跃请求后等待接地消费的时间。
        [Tooltip("落地前缓存跳跃请求的时间，单位：秒")]
        [Min(0f)] [SerializeField] private float _bufferTime = 0.12f;
        // 起跳后暂时忽略接地探测的时长，避免扩大后的探测范围过早重新判定接地。
        [Tooltip("起跳后暂时忽略接地探测的时长，单位：秒")]
        [Min(0f)] [SerializeField] private float _groundIgnoreAfterStartDuration = 0.1f;
        // 提前松开跳跃键时保留的上升速度比例。
        [Tooltip("提前松开跳跃键时保留的上升速度比例，范围：0-1")]
        [Range(0f, 1f)] [SerializeField] private float _cutMultiplier = 0.5f;
        // 离开稳定地面后仍允许起跳的剩余时间。
        [NonSerialized] private float _coyoteRemaining;
        // 已接收到但尚未消费的跳跃请求剩余时间。
        [NonSerialized] private float _bufferRemaining;
        // 当前起跳后是否仍处于可执行跳跃截断的阶段。
        [NonSerialized] private bool _jumping;
        // 当前上升阶段是否已应用过一次跳跃截断。
        [NonSerialized] private bool _cutApplied;

        /// <summary>获取当前是否处于主动起跳后的空中阶段。</summary>
        public bool IsJumping => _jumping;

        /// <summary>获取本次起跳应写入的初始向上速度。</summary>
        public float InitialSpeed => _initialSpeed;

        /// <summary>获取提前松开跳跃键时保留的上升速度比例。</summary>
        public float CutMultiplier => _cutMultiplier;

        /// <summary>获取起跳后暂时忽略接地探测的时长。</summary>
        public float GroundIgnoreAfterStartDuration => _groundIgnoreAfterStartDuration;

        /// <summary>
        /// 创建不共享跳跃瞬态状态的运行时配置副本。
        /// </summary>
        /// <returns>供单个单位运行时使用的独立跳跃配置。</returns>
        public JumpModule CreateRuntimeCopy()
        {
            return new JumpModule
            {
                _enabled = _enabled,
                _initialSpeed = _initialSpeed,
                _coyoteTime = _coyoteTime,
                _bufferTime = _bufferTime,
                _groundIgnoreAfterStartDuration = _groundIgnoreAfterStartDuration,
                _cutMultiplier = _cutMultiplier
            };
        }

        /// <summary>
        /// 清空本组件保存的全部瞬态跳跃状态，保留 Inspector 配置供下一次运行时复用。
        /// </summary>
        public void ResetRuntimeState()
        {
            _coyoteRemaining = 0f;
            _bufferRemaining = 0f;
            _jumping = false;
            _cutApplied = false;
        }

        /// <summary>
        /// 更新跳跃计时器并解析本物理步是否应开始跳跃或截断上升速度。
        /// </summary>
        /// <param name="state">当前运动状态。</param>
        /// <param name="command">本物理步的通用移动命令。</param>
        /// <param name="fixedDeltaTime">本物理步时长，单位：秒。</param>
        /// <param name="startJump">返回是否应立即设置初始跳跃速度。</param>
        /// <param name="cutJump">返回是否应降低当前向上速度。</param>
        public void Update(
            in UnitMovementState state,
            in UnitMovementCommand command,
            float fixedDeltaTime,
            out bool startJump,
            out bool cutJump)
        {
            startJump = false;
            cutJump = false;
            if (!_enabled)
            {
                ResetRuntimeState();
                return;
            }

            // 主动跳跃上升期间即使仍在扩大后的探测范围内，也不能被误判为重新落地而提前结束跳跃。
            bool hasCompletedJumpLanding = state.IsStableGrounded
                                           && (!_jumping || state.CurrentVelocity.y <= 0f);
            if (hasCompletedJumpLanding)
            {
                _coyoteRemaining = _coyoteTime;
                _jumping = false;
                _cutApplied = false;
            }
            else
                _coyoteRemaining = Mathf.Max(0f, _coyoteRemaining - fixedDeltaTime);

            if (command.RequestJump)
                _bufferRemaining = _bufferTime;
            else
                _bufferRemaining = Mathf.Max(0f, _bufferRemaining - fixedDeltaTime);

            // 仅由本模块消费请求，防止同一跳跃被多个功能重复处理。
            if (_bufferRemaining > 0f && _coyoteRemaining > 0f)
            {
                _bufferRemaining = 0f;
                _coyoteRemaining = 0f;
                _jumping = true;
                _cutApplied = false;
                startJump = true;
                return;
            }

            // 跳跃键松开且仍向上时只截断一次，避免持续重复削减速度。
            if (_jumping && !_cutApplied && !command.IsJumpHeld && state.CurrentVelocity.y > 0f)
            {
                _cutApplied = true;
                cutJump = true;
            }
        }
    }
}

