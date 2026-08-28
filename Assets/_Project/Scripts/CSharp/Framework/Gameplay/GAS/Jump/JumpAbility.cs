using Framework.Gameplay.Abilities.Configuration;
using Framework.Gameplay.Abilities.Input;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Movement
{
    /// <summary>提供跳跃、土狼时间和可变跳跃高度能力运行时。</summary>
    public sealed class JumpAbility : AbilityRuntime
    {
        // 跳跃静态配置表。
        private readonly JumpAbilitySO _configuration;
        // 当前单位共享的移动状态数据。
        private MovementSubContext _movementSub;
        // 当前单位刚体。
        private Rigidbody _rigidbody;
        // 当前单位独占跳跃模块。
        private JumpModule _jump;
        // 当前单位共享的跳跃状态数据。
        private JumpSubContext _jumpSub;

        /// <summary>创建跳跃运行时并保存配置表引用。</summary>
        /// <param name="configuration">跳跃配置表；允许为 null 并使用默认配置。</param>
        public JumpAbility(JumpAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>获取移动状态数据和刚体依赖。</summary>
        /// <param name="context">当前单位能力上下文。</param>
        public override void AbilityInit(AbilityContext context)
        {
            base.AbilityInit(context);
            if (context == null || context.Owner == null) return;
            _rigidbody = context.Owner.GetComponent<Rigidbody>();
            // 从配置表创建单位独占跳跃状态。
            _jump = _configuration != null
                ? _configuration.CreateRuntimeCopy()
                : new JumpModule();
            _jumpSub = new JumpSubContext();
            Context.Register(AbilityContextDataType.Jump, _jumpSub);
        }

        /// <summary>在所有能力完成初始化后解析移动状态依赖。</summary>
        public override void AbilityStart()
        {
            ResolveMovementData();
        }

        /// <summary>清空跳跃瞬态状态。</summary>
        public override void AbilityOnEnable()
        {
            _jump?.ResetRuntimeState();
            _jumpSub?.Reset();
        }

        /// <summary>消费输入并修改刚体的垂直跳跃速度。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public override void AbilityFixedUpdate(float fixedDeltaTime)
        {
            if (_movementSub == null) ResolveMovementData();
            if (_movementSub == null || _rigidbody == null || _jump == null || Context == null) return;
            MovementCommand command = MovementCommand.CreateDefault();
            if (Context.TryGet(AbilityContextDataType.Input, out InputSubContext input))
            {
                command.RequestJump = input.ConsumePressed(InputButton.Jump);
                command.IsJumpHeld = input.IsHeld(InputButton.Jump);
            }
            _jump.Update(_movementSub.CurrentState, command, fixedDeltaTime, out bool startJump, out bool cutJump);
            _jumpSub?.Write(_jump.IsJumping);
            if (!startJump && !cutJump) return;

            Vector3 velocity = _rigidbody.velocity;
            if (startJump) velocity.y = _jump.InitialSpeed;
            else if (velocity.y > 0f) velocity.y *= _jump.CutMultiplier;
            _rigidbody.velocity = velocity;
        }

        /// <summary>从能力上下文获取基础移动状态，允许能力列表顺序变化。</summary>
        private void ResolveMovementData()
        {
            if (_movementSub != null || Context == null) return;
            Context.TryGet(AbilityContextDataType.Movement, out _movementSub);
        }

        /// <summary>清空跳跃状态。</summary>
        public override void AbilityOnDisable()
        {
            _jump?.ResetRuntimeState();
            _jumpSub?.Reset();
        }

        /// <summary>释放跳跃运行时引用。</summary>
        public override void AbilityDispose()
        {
            AbilityOnDisable();
            Context?.Unregister(AbilityContextDataType.Jump, _jumpSub);
            _movementSub = null;
            _rigidbody = null;
            _jump = null;
            _jumpSub = null;
            base.AbilityDispose();
        }
    }
}
