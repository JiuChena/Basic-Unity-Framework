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
        // 浮动胶囊和脚底辅助碰撞体参数。
        [Header("浮动胶囊")]
        [Tooltip("顶部对齐的胶囊缩短、底部留空和脚底 BoxCollider 参数")]
        [SerializeField] private FloatingCapsuleModule _floatingCapsule = new FloatingCapsuleModule();
        // 跳跃参数。
        [Header("跳跃")]
        [Tooltip("跳跃初速度、土狼时间、输入缓冲和截断参数")]
        [SerializeField] private JumpModule _jump = new JumpModule();
        // 边缘保护参数。
        [Header("边缘保护")]
        [Tooltip("预测脚底支撑、短缝确认和边缘速度约束参数")]
        [SerializeField] private EdgeProtectionModule _edgeProtection = new EdgeProtectionModule();
        // 刚体接管配置。
        [Header("物理接管")]
        [Tooltip("是否由移动能力关闭 Unity 自动重力、冻结旋转并统一提交刚体速度")]
        [SerializeField] private bool _takeoverRigidbody = true;
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
                _floatingCapsule != null ? _floatingCapsule.CreateRuntimeCopy() : new FloatingCapsuleModule(),
                _jump != null ? _jump.CreateRuntimeCopy() : new JumpModule(),
                _edgeProtection != null ? _edgeProtection.CreateRuntimeCopy() : new EdgeProtectionModule(),
                _takeoverRigidbody,
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
        // 当前单位独占的浮动胶囊模块。
        private readonly FloatingCapsuleModule _floatingCapsule;
        // 当前单位独占的跳跃模块。
        private readonly JumpModule _jump;
        // 当前单位独占的边缘保护模块。
        private readonly EdgeProtectionModule _edgeProtection;
        // 是否由当前移动能力接管刚体物理设置。
        private readonly bool _takeoverRigidbody;
        // 缓存后的移动参考 Transform，避免固定帧访问 Camera.main。
        private Transform _movementReference;
        // 输入监听能力写入的单位独占输入黑板。
        private InputBlackboard _input;
        // 移动能力自行解析或创建的刚体组件。
        private Rigidbody _rigidbody;
        // 仅由当前移动能力创建和提交的刚体适配器。
        private IUnitBody _body;
        // 当前单位独占的浮动形状同步模块。
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
        /// <param name="floatingCapsule">单位独占浮动胶囊模块。</param>
        /// <param name="jump">单位独占跳跃模块。</param>
        /// <param name="edgeProtection">单位独占边缘保护模块。</param>
        /// <param name="takeoverRigidbody">是否由当前运行时接管刚体物理设置。</param>
        /// <param name="movementReferenceCamera">移动参考相机。</param>
        public MovementAbilityRuntime(
            LocomotionSettings locomotion,
            GroundSettings ground,
            GravityModule gravity,
            FloatingCapsuleModule floatingCapsule,
            JumpModule jump,
            EdgeProtectionModule edgeProtection,
            bool takeoverRigidbody,
            Camera movementReferenceCamera)
        {
            _locomotion = locomotion;
            _ground = ground;
            _gravity = gravity;
            _floatingCapsule = floatingCapsule;
            _jump = jump;
            _edgeProtection = edgeProtection;
            _takeoverRigidbody = takeoverRigidbody;
            _movementReference = movementReferenceCamera != null ? movementReferenceCamera.transform : null;
        }

        /// <summary>解析或补齐移动组件，并创建当前能力独占的纯 C# 模块。</summary>
        /// <param name="context">能力所属单位上下文。</param>
        public override void Initialize(AbilityContext context)
        {
            base.Initialize(context);
            if (context == null || context.Owner == null) return;

            // 移动能力独占解析自身需要的 Unity 组件，缺失时补齐最小可用组件。
            _rigidbody = context.Owner.GetComponent<Rigidbody>();
            if (_rigidbody == null) _rigidbody = context.Owner.AddComponent<Rigidbody>();
            CapsuleCollider movementCollider = context.Owner.GetComponent<CapsuleCollider>();
            if (movementCollider == null) movementCollider = context.Owner.AddComponent<CapsuleCollider>();

            // 组装浮动形状、接地探测和悬浮修正的完整移动链路。
            IPhysicsQuery physicsQuery = new UnityPhysicsQuery();
            _shape = new ColliderShapeModule(movementCollider, context.Owner, _floatingCapsule);
            _groundProbe = new GroundProbeModule(_shape, context.Transform, physicsQuery, _ground);
            _hover = new HoverModule(_ground, _groundProbe);
            _steepSlope = new SteepSlopeSlideModule(_ground);
            _edgeProtection?.Initialize(_shape, _groundProbe);
            _gravity.Initialize(Physics.gravity);
        }

        /// <summary>缓存输入黑板和参考相机。</summary>
        public override void Start()
        {
            if (Context == null) return;

            // 输入监听能力必须排在移动能力之前，移动能力只读取明确的输入黑板。
            _input = Context.Blackboard as InputBlackboard;
            if (_movementReference == null && Camera.main != null)
                _movementReference = Camera.main.transform;
        }

        /// <summary>启用形状同步并开始接管当前单位刚体。</summary>
        public override void OnEnable()
        {
            // 每次启用都重新创建刚体适配器，确保禁用期间已恢复的刚体设置会再次正确接管。
            if (_rigidbody != null && (_body == null || !_body.IsValid))
                _body = new RigidbodyUnitBody(_rigidbody, true, _takeoverRigidbody);

            // 形状模块是浮动胶囊和脚底辅助碰撞体的唯一所有者。
            _shape?.Synchronize();
            _gravity?.ResetRuntimeState();
            _jump?.ResetRuntimeState();
            _steepSlope?.ResetRuntimeState();
            _edgeProtection?.ResetRuntimeState();
        }

        /// <summary>执行输入转换、接地检测、斜坡限制、重力和悬浮修正。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public override void FixedUpdate(float fixedDeltaTime)
        {
            if (_body == null || !_body.IsValid || _groundProbe == null || _locomotion == null) return;

            // 先同步实际碰撞形状，再使用同一形状探测地面和支撑距离。
            _shape?.Synchronize();
            Vector3 currentVelocity = _body.Velocity;
            GroundContact contact = _groundProbe.ProbeGround();
            bool isGrounded = contact.IsGrounded;
            MovementMode mode = isGrounded ? MovementMode.Ground : MovementMode.Air;
            UnitMovementState state = _groundProbe.CreateMovementState(
                contact,
                mode,
                currentVelocity,
                _jump != null && _jump.IsJumping,
                _edgeProtection != null && _edgeProtection.IsEnabled);

            // 将输入黑板转换为本物理步可被所有移动子模块消费的无业务命令。
            UnitMovementCommand command = UnitMovementCommand.CreateDefault();
            if (_input != null)
            {
                command.WorldMoveDirection = _input.GetWorldMoveDirection(_movementReference);
                command.SpeedScale = _input.Sprint.IsHeld ? 1.6f : 1f;
                command.RequestJump = _input.Jump.ConsumePressed(ref _jumpPressedVersion, out _);
                command.IsJumpHeld = _input.Jump.IsHeld;
            }

            // 跳跃先更新本步状态，后续悬浮模块据此避免干预主动起跳。
            bool startJump = false;
            bool cutJump = false;
            if (_jump != null) _jump.Update(state, command, fixedDeltaTime, out startJump, out cutJump);
            _steepSlope.ConstrainUphillInput(ref command, contact, fixedDeltaTime);
            Vector3 targetDirection = command.WorldMoveDirection;
            if (isGrounded) targetDirection = Vector3.ProjectOnPlane(targetDirection, contact.Hit.normal).normalized;

            float maxSpeed = isGrounded ? _locomotion.GroundMaxSpeed : _locomotion.AirMaxSpeed;
            float acceleration = isGrounded ? _locomotion.GroundAcceleration : _locomotion.AirAcceleration;
            if (!isGrounded) acceleration *= _locomotion.AirControl;
            Vector3 horizontal = Vector3.ProjectOnPlane(currentVelocity, Vector3.up);
            Vector3 targetVelocity = targetDirection * (maxSpeed * Mathf.Max(0f, command.SpeedScale));
            float moveRate = targetDirection.sqrMagnitude > 0.0001f ? acceleration : _locomotion.GroundDeceleration;
            horizontal = Vector3.MoveTowards(horizontal, targetVelocity, moveRate * fixedDeltaTime);

            // 合并水平移动、跳跃、重力、悬浮和陡坡，最后只由本能力提交一次刚体速度。
            Vector3 velocity = new Vector3(horizontal.x, currentVelocity.y, horizontal.z);
            if (startJump) velocity.y = _jump.InitialSpeed;
            else if (cutJump && velocity.y > 0f) velocity.y *= _jump.CutMultiplier;
            velocity = _gravity.Apply(velocity, isGrounded, fixedDeltaTime);
            velocity = _hover.Apply(velocity, contact, _jump != null && _jump.IsJumping, fixedDeltaTime);
            velocity = _steepSlope.Apply(velocity, fixedDeltaTime);

            // 边缘保护只约束本能力已经合并完成的水平候选速度。
            if (_edgeProtection != null)
            {
                Vector3 candidateHorizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
                Vector3 currentHorizontal = Vector3.ProjectOnPlane(currentVelocity, Vector3.up);
                _edgeProtection.ConstrainVelocity(
                    state,
                    candidateHorizontal,
                    currentHorizontal,
                    fixedDeltaTime,
                    out Vector3 constrainedHorizontal,
                    out _);
                velocity = new Vector3(constrainedHorizontal.x, velocity.y, constrainedHorizontal.z);
            }

            _body.Commit(velocity);
        }

        /// <summary>停止移动能力并恢复其修改的共享 Unity 组件状态。</summary>
        public override void OnDisable()
        {
            // 浮动形状和脚底辅助碰撞体只在当前移动能力启用期间存在。
            _shape?.RestoreAuthoringShape();

            // 刚体物理设置属于移动能力的接管范围，禁用时必须恢复作者状态。
            _body?.RestoreInitialSettings();
            _body = null;
            _steepSlope?.ResetRuntimeState();
            _jump?.ResetRuntimeState();
            _edgeProtection?.ResetRuntimeState();
        }

        /// <summary>释放移动能力持有的运行时模块和 Unity 组件引用。</summary>
        public override void Dispose()
        {
            // 销毁路径可能绕过禁用回调，因此再次执行可重入的状态恢复。
            OnDisable();
            _gravity?.ResetRuntimeState();
            _groundProbe = null;
            _hover = null;
            _steepSlope = null;
            _shape = null;
            _rigidbody = null;
            _input = null;
            base.Dispose();
        }

        // 跳跃按下事件的移动能力消费者游标。
        private uint _jumpPressedVersion;
    }
}
