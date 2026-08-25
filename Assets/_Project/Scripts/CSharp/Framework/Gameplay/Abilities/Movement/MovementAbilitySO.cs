using Framework.ExpandComponent.UnitMover;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 配置并执行地面、空中、接地和重力移动能力。
    /// </summary>
    [CreateAssetMenu(fileName = "MovementAbility", menuName = "Framework/Gameplay/Abilities/Movement")]
    public sealed class MovementAbilitySO : AbilityDefinitionSO
    {
        // 水平移动参数。
        [Header("移动")]
        [Tooltip("地面和空中的最大速度、加速度以及空中控制参数")]
        [SerializeField] private LocomotionSettings _locomotion = new LocomotionSettings();
        // 地面和坡面参数。
        [Tooltip("地面层、坡度限制、悬浮支撑和接地探测参数")]
        [SerializeField] private GroundSettings _ground = new GroundSettings();
        // 重力参数。
        [Header("重力")]
        [Tooltip("项目重力倍率、下落倍率和最大下落速度")]
        [SerializeField] private GravityModule _gravity = new GravityModule();
        // 移动参考相机。
        [Header("参考系")]
        [Tooltip("将输入转换到世界空间的参考相机；为空时使用主相机")]
        [SerializeField] private Camera _movementReferenceCamera;

        /// <summary>根据配置创建移动能力运行时。</summary>
        /// <param name="context">能力所属单位上下文。</param>
        /// <returns>单位独占移动运行时。</returns>
        public override AbilityRuntime CreateRuntime(AbilityContext context)
        {
            return new MovementAbilityRuntime(
                _locomotion != null ? _locomotion.CreateRuntimeCopy() : new LocomotionSettings(),
                _ground != null ? _ground.CreateRuntimeCopy() : new GroundSettings(),
                _gravity != null ? _gravity.CreateRuntimeCopy() : new GravityModule(),
                _movementReferenceCamera);
        }
    }

    /// <summary>
    /// 在固定帧中读取输入、探测接地并计算最终基础移动速度。
    /// </summary>
    public sealed class MovementAbilityRuntime : AbilityRuntime
    {
        // 当前单位独占的水平移动配置。
        private readonly LocomotionSettings _locomotion;
        // 当前单位独占的地面配置。
        private readonly GroundSettings _ground;
        // 当前单位独占的重力模块。
        private readonly GravityModule _gravity;
        // 缓存后的移动参考 Transform，避免固定帧访问 Camera.main。
        private Transform _movementReference;
        // 浮动胶囊能力提供的共享形状模块。
        private ColliderShapeModule _shape;
        // 当前单位独占的地面检测模块。
        private GroundProbeModule _groundProbe;
        // 当前单位独占的悬浮支撑模块。
        private HoverModule _hover;
        // 当前单位独占的陡坡约束模块。
        private SteepSlopeSlideModule _steepSlope;

        /// <summary>创建移动能力运行时。</summary>
        /// <param name="locomotion">单位独占水平移动配置。</param>
        /// <param name="ground">单位独占地面配置。</param>
        /// <param name="gravity">单位独占重力模块。</param>
        /// <param name="movementReferenceCamera">移动参考相机。</param>
        public MovementAbilityRuntime(
            LocomotionSettings locomotion,
            GroundSettings ground,
            GravityModule gravity,
            Camera movementReferenceCamera)
        {
            _locomotion = locomotion;
            _ground = ground;
            _gravity = gravity;
            _movementReference = movementReferenceCamera != null ? movementReferenceCamera.transform : null;
        }

        /// <summary>解析浮动胶囊形状并创建接地、悬浮和陡坡模块。</summary>
        public override void Start()
        {
            if (Context == null || Context.MovementCollider == null) return;

            _shape = Context.GetService<ColliderShapeModule>();
            if (_shape == null)
            {
                _shape = new ColliderShapeModule(
                    Context.MovementCollider,
                    Context.Owner,
                    new FloatingCapsuleModule());
                _shape.Synchronize();
                Context.RegisterService(_shape);
            }

            _groundProbe = new GroundProbeModule(_shape, Context.Transform, Context.PhysicsQuery, _ground);
            _hover = new HoverModule(_ground, _groundProbe);
            _steepSlope = new SteepSlopeSlideModule(_ground);
            _gravity.Initialize(Physics.gravity);
            if (_movementReference == null && Camera.main != null)
                _movementReference = Camera.main.transform;
            Context.RegisterService(_groundProbe);
        }

        /// <summary>执行输入转换、接地检测、斜坡限制、重力和悬浮修正。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public override void FixedUpdate(float fixedDeltaTime)
        {
            if (Context == null || _groundProbe == null || _locomotion == null) return;

            InputBlackboard input = Context.GetService<InputBlackboard>();
            GroundContact contact = _groundProbe.ProbeGround();
            bool isGrounded = contact.IsGrounded;
            MovementMode mode = isGrounded ? MovementMode.Ground : MovementMode.Air;
            UnitMovementState state = _groundProbe.CreateMovementState(
                contact,
                mode,
                Context.Velocity,
                false,
                false);

            UnitMovementCommand command = UnitMovementCommand.CreateDefault();
            if (input != null)
            {
                command.WorldMoveDirection = input.GetWorldMoveDirection(_movementReference);
                command.SpeedScale = input.Sprint.IsHeld ? 1.6f : 1f;
                command.RequestJump = input.Jump.ConsumePressed(ref _jumpPressedVersion, out _);
                command.IsJumpHeld = input.Jump.IsHeld;
            }

            _steepSlope.ConstrainUphillInput(ref command, contact, fixedDeltaTime);
            Vector3 targetDirection = command.WorldMoveDirection;
            if (isGrounded) targetDirection = Vector3.ProjectOnPlane(targetDirection, contact.Hit.normal).normalized;

            float maxSpeed = isGrounded ? _locomotion.GroundMaxSpeed : _locomotion.AirMaxSpeed;
            float acceleration = isGrounded ? _locomotion.GroundAcceleration : _locomotion.AirAcceleration;
            if (!isGrounded) acceleration *= _locomotion.AirControl;
            Vector3 horizontal = Vector3.ProjectOnPlane(Context.Velocity, Vector3.up);
            Vector3 targetVelocity = targetDirection * (maxSpeed * Mathf.Max(0f, command.SpeedScale));
            float moveRate = targetDirection.sqrMagnitude > 0.0001f ? acceleration : _locomotion.GroundDeceleration;
            horizontal = Vector3.MoveTowards(horizontal, targetVelocity, moveRate * fixedDeltaTime);

            Vector3 velocity = new Vector3(horizontal.x, Context.Velocity.y, horizontal.z);
            velocity = _gravity.Apply(velocity, isGrounded, fixedDeltaTime);
            velocity = _hover.Apply(velocity, contact, false, fixedDeltaTime);
            velocity = _steepSlope.Apply(velocity, fixedDeltaTime);
            Context.Velocity = velocity;
            Context.SetMovementFrame(state, command);
        }

        /// <summary>清理移动能力运行时状态。</summary>
        public override void Dispose()
        {
            _gravity.ResetRuntimeState();
            _steepSlope?.ResetRuntimeState();
            if (Context != null && ReferenceEquals(Context.GetService<GroundProbeModule>(), _groundProbe))
                Context.RegisterService<GroundProbeModule>(null);
            _groundProbe = null;
            _hover = null;
            _steepSlope = null;
            _shape = null;
            base.Dispose();
        }

        // 跳跃按下事件的移动能力消费者游标。
        private uint _jumpPressedVersion;
    }
}
