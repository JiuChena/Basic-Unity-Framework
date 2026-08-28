using Framework.Gameplay.Abilities.Configuration;
using Framework.Gameplay.Abilities.Input;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Movement
{
    /// <summary>提供基础水平移动、接地和重力的能力运行时。</summary>
    public sealed class MovementAbility : AbilityRuntime
    {
        // 基础移动静态配置表。
        private readonly MovementAbilitySO _configuration;
        // 单位独占的水平移动运行时配置。
        private LocomotionSettings _locomotion;
        // 单位独占的接地运行时配置。
        private GroundSettings _ground;
        // 单位独占的重力运行时配置。
        private GravityModule _gravity;
        // 当前单位刚体。
        private Rigidbody _rigidbody;
        // 当前单位刚体速度适配器。
        private RigidbodyUnitBody _body;
        // 当前单位基础胶囊形状模块。
        private ColliderShapeModule _shape;
        // 当前单位基础接地探测模块。
        private GroundProbeModule _groundProbe;
        // 缓存的移动参考 Transform。
        private Transform _movementReference;
        // 当前固定帧运动状态。
        private MovementState _currentState;
        // 当前固定帧移动命令。
        private MovementCommand _currentCommand;
        // 当前单位独占的移动共享状态。
        private MovementSubContext _movementSub;

        /// <summary>创建基础移动运行时并保存配置表引用。</summary>
        /// <param name="configuration">基础移动配置表；允许为 null 并使用默认配置。</param>
        public MovementAbility(MovementAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>获取或创建基础移动组件并创建纯 C# 依赖。</summary>
        /// <param name="context">当前单位能力上下文。</param>
        public override void AbilityInit(AbilityContext context)
        {
            base.AbilityInit(context);
            if (context == null || context.Owner == null) return;

            // 基础移动只获取自身需要的 Unity 组件。
            _rigidbody = context.Owner.GetComponent<Rigidbody>();
            if (_rigidbody == null && Application.isPlaying) _rigidbody = context.Owner.AddComponent<Rigidbody>();
            CapsuleCollider capsule = context.Owner.GetComponent<CapsuleCollider>();
            if (capsule == null && Application.isPlaying) capsule = context.Owner.AddComponent<CapsuleCollider>();
            if (_rigidbody == null || capsule == null) return;

            // 从配置表创建单位独占运行时配置，避免共享 SO 内的状态。
            if (_configuration != null)
                _configuration.CreateRuntimeCopies(out _locomotion, out _ground, out _gravity);
            else
            {
                _locomotion = new LocomotionSettings();
                _ground = new GroundSettings();
                _gravity = new GravityModule();
            }

            _shape = new ColliderShapeModule(capsule, context.Owner, new FloatingCapsuleModule());
            _groundProbe = new GroundProbeModule(
                _shape,
                context.Transform,
                new UnityPhysicsQuery(),
                _ground);
            _gravity.Initialize(Physics.gravity);
            _movementSub = new MovementSubContext();
            Context.Register(AbilityContextDataType.Movement, _movementSub);
        }

        /// <summary>创建刚体接管适配器并缓存移动参考相机。</summary>
        public override void AbilityStart()
        {
            if (_rigidbody == null) return;
            if (_body == null || !_body.IsValid) _body = new RigidbodyUnitBody(_rigidbody, true, true);
            _movementReference = Camera.main != null ? Camera.main.transform : null;
        }

        /// <summary>重置基础移动的瞬态状态。</summary>
        public override void AbilityOnEnable()
        {
            _gravity?.ResetRuntimeState();
            _gravity?.Initialize(Physics.gravity);
            _currentState = default;
            _currentCommand = MovementCommand.CreateDefault();
            if (_rigidbody != null && (_body == null || !_body.IsValid))
                _body = new RigidbodyUnitBody(_rigidbody, true, true);
        }

        /// <summary>读取输入并计算基础移动速度。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public override void AbilityFixedUpdate(float fixedDeltaTime)
        {
            if (_body == null || !_body.IsValid || _groundProbe == null || _locomotion == null) return;

            // 基础移动先更新接地状态，其他能力读取本次结果。
            Vector3 currentVelocity = _body.Velocity;
            GroundProbeModule groundProbe = _groundProbe;
            if (Context != null
                && Context.TryGet(AbilityContextDataType.FloatingCapsule, out FloatingCapsuleSubContext floatingData)
                && floatingData.GroundProbe != null)
                groundProbe = floatingData.GroundProbe;
            GroundContact contact = groundProbe.ProbeGround();
            bool grounded = contact.IsGrounded;
            _currentState = groundProbe.CreateMovementState(
                contact,
                grounded ? MovementMode.Ground : MovementMode.Air,
                currentVelocity,
                false,
                false);

            // 当前帧只消费移动和冲刺输入，不处理其他能力输入。
            _currentCommand = MovementCommand.CreateDefault();
            if (Context != null && Context.TryGet(AbilityContextDataType.Input, out InputSubContext input))
            {
                _currentCommand.WorldMoveDirection = input.GetWorldMoveDirection(_movementReference);
                _currentCommand.SpeedScale = input.IsHeld(InputButton.Sprint) ? 1.6f : 1f;
            }
            _movementSub?.Write(_currentState, _currentCommand);

            Vector3 targetDirection = _currentCommand.WorldMoveDirection;
            if (grounded) targetDirection = Vector3.ProjectOnPlane(targetDirection, contact.Hit.normal).normalized;
            float maxSpeed = grounded ? _locomotion.GroundMaxSpeed : _locomotion.AirMaxSpeed;
            float acceleration = grounded ? _locomotion.GroundAcceleration : _locomotion.AirAcceleration;
            if (!grounded) acceleration *= _locomotion.AirControl;
            Vector3 horizontal = Vector3.ProjectOnPlane(currentVelocity, Vector3.up);
            Vector3 targetVelocity = targetDirection * (maxSpeed * Mathf.Max(0f, _currentCommand.SpeedScale));
            float moveRate = targetDirection.sqrMagnitude > 0.0001f ? acceleration : _locomotion.GroundDeceleration;
            horizontal = Vector3.MoveTowards(horizontal, targetVelocity, moveRate * fixedDeltaTime);

            // 基础移动只提交自己的移动、重力结果，附加能力随后继续修改刚体速度。
            Vector3 velocity = new Vector3(horizontal.x, currentVelocity.y, horizontal.z);
            _body.Commit(_gravity.Apply(velocity, grounded, fixedDeltaTime));
        }

        /// <summary>停止基础移动并恢复刚体接管前的设置。</summary>
        public override void AbilityOnDisable()
        {
            _body?.RestoreInitialSettings();
            _body = null;
            _gravity?.ResetRuntimeState();
        }

        /// <summary>释放基础移动运行时引用。</summary>
        public override void AbilityDispose()
        {
            AbilityOnDisable();
            _groundProbe = null;
            _shape = null;
            _rigidbody = null;
            _movementReference = null;
            Context?.Unregister(AbilityContextDataType.Movement, _movementSub);
            _movementSub = null;
            base.AbilityDispose();
        }
    }
}
