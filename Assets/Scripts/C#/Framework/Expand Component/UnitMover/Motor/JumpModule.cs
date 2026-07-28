using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 维护跳跃请求缓存、土狼时间和跳跃截断状态的纯 C# 模块。
    /// </summary>
    public sealed class JumpModule
    {
        // 普通跳跃的启用开关和手感配置。
        private readonly JumpSettings _settings;
        // 离开稳定地面后仍允许起跳的剩余时间。
        private float _coyoteRemaining;
        // 已接收到但尚未消费的跳跃请求剩余时间。
        private float _bufferRemaining;
        // 当前起跳后是否仍处于可执行跳跃截断的阶段。
        private bool _jumping;
        // 当前上升阶段是否已应用过一次跳跃截断。
        private bool _cutApplied;

        /// <summary>
        /// 使用指定跳跃配置创建运行时跳跃模块。
        /// </summary>
        /// <param name="settings">普通跳跃配置。</param>
        public JumpModule(JumpSettings settings)
        {
            _settings = settings;
        }

        /// <summary>获取当前是否处于主动起跳后的空中阶段。</summary>
        public bool IsJumping => _jumping;

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
            if (_settings == null || !_settings.Enabled)
            {
                _coyoteRemaining = 0f;
                _bufferRemaining = 0f;
                _jumping = false;
                _cutApplied = false;
                return;
            }

            // 稳定接地会重置土狼时间并结束上一段跳跃状态。
            if (state.IsStableGrounded)
            {
                _coyoteRemaining = _settings.CoyoteTime;
                _jumping = false;
                _cutApplied = false;
            }
            else
                _coyoteRemaining = Mathf.Max(0f, _coyoteRemaining - fixedDeltaTime);

            if (command.RequestJump)
                _bufferRemaining = _settings.BufferTime;
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
