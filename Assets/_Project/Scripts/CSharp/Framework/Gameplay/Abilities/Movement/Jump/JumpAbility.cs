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
        private MovementContextData _movementData;
        // 当前单位刚体。
        private Rigidbody _rigidbody;
        // 当前单位独占跳跃模块。
        private JumpModule _jump;
        // 当前单位共享的跳跃状态数据。
        private JumpContextData _jumpData;

        /// <summary>创建跳跃运行时并保存配置表引用。</summary>
        /// <param name="configuration">跳跃配置表；允许为 null 并使用默认配置。</param>
        public JumpAbility(JumpAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>获取移动状态数据和刚体依赖。</summary>
        /// <param name="context">当前单位能力上下文。</param>
        public override void Initialize(AbilityContext context)
        {
            base.Initialize(context);
            if (context == null || context.Owner == null) return;
            Context.TryGet(AbilityContextDataType.Movement, out _movementData);
            _rigidbody = context.Owner.GetComponent<Rigidbody>();
            // 从配置表创建单位独占跳跃状态。
            _jump = _configuration != null
                ? _configuration.CreateRuntimeCopy()
                : new JumpModule();
            _jumpData = new JumpContextData();
            Context.Register(AbilityContextDataType.Jump, _jumpData);
        }

        /// <summary>清空跳跃瞬态状态。</summary>
        public override void OnAbilityEnable()
        {
            _jump?.ResetRuntimeState();
            _jumpData?.Reset();
        }

        /// <summary>消费输入并修改刚体的垂直跳跃速度。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public override void FixedUpdateAbility(float fixedDeltaTime)
        {
            if (_movementData == null || _rigidbody == null || _jump == null || Context == null) return;
            UnitMovementCommand command = UnitMovementCommand.CreateDefault();
            if (Context.TryGet(AbilityContextDataType.Input, out InputBlackboard input))
            {
                command.RequestJump = input.ConsumeJumpPressed();
                command.IsJumpHeld = input.JumpHeld;
            }
            _jump.Update(_movementData.CurrentState, command, fixedDeltaTime, out bool startJump, out bool cutJump);
            _jumpData?.Write(_jump.IsJumping);
            if (!startJump && !cutJump) return;

            Vector3 velocity = _rigidbody.velocity;
            if (startJump) velocity.y = _jump.InitialSpeed;
            else if (velocity.y > 0f) velocity.y *= _jump.CutMultiplier;
            _rigidbody.velocity = velocity;
        }

        /// <summary>清空跳跃状态。</summary>
        public override void OnAbilityDisable()
        {
            _jump?.ResetRuntimeState();
            _jumpData?.Reset();
        }

        /// <summary>释放跳跃运行时引用。</summary>
        public override void DisposeAbility()
        {
            OnAbilityDisable();
            Context?.Unregister(AbilityContextDataType.Jump, _jumpData);
            _movementData = null;
            _rigidbody = null;
            _jump = null;
            _jumpData = null;
            base.DisposeAbility();
        }
    }
}
