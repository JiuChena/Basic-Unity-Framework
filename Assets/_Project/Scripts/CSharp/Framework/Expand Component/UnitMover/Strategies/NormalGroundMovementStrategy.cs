using System;
using Framework.ExpandComponent.DataProvider;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 组合标准地面模块并提供普通刚体角色移动的默认策略。
    /// </summary>
    [Serializable]
    [MovedFrom(true, "Framework.ExpandComponent.UnitMover", null, "DefaultRigidbodyMovementStrategy")]
    public sealed class NormalGroundMovementStrategy : UnitMovementStrategy
    {
        // 地面与空中水平速度配置。
        [UnitMovementModuleName("移动参数")]
        [Tooltip("标准地面与空中移动的速度、加速度和空中控制配置")]
        [SerializeField] private LocomotionSettings _locomotionSettings = new LocomotionSettings();
        // 接地、坡面、悬浮弹簧与陡坡下滑配置。
        [UnitMovementModuleName("地面与斜坡")]
        [Tooltip("标准接地、坡面限制、悬浮和陡坡下滑配置")]
        [SerializeField] private GroundSettings _groundSettings = new GroundSettings();
        // 普通跳跃配置与运行时跳跃状态。
        [UnitMovementModuleName("跳跃")]
        [Tooltip("标准跳跃配置与运行时缓存")]
        [SerializeField] private JumpModule _jumpModule = new JumpModule();
        // 项目重力倍率和最大下落速度配置。
        [UnitMovementModuleName("重力")]
        [Tooltip("重力倍率与最大下落速度配置")]
        [SerializeField] private GravityModule _gravityModule = new GravityModule();
        // 浮动胶囊、脚底辅助碰撞体和基础形状快照。
        [UnitMovementModuleName("浮动胶囊")]
        [Tooltip("顶部对齐浮动胶囊与脚底辅助碰撞体配置")]
        [SerializeField] private FloatingCapsuleModule _floatingCapsuleModule = new FloatingCapsuleModule();
        // 边缘支撑预测、短缝与检查点恢复配置。
        [UnitMovementModuleName("边缘保护")]
        [Tooltip("边缘防跌落、支撑预测和检查点配置")]
        [SerializeField] private EdgeProtectionModule _edgeProtectionModule = new EdgeProtectionModule();

        // 同步有效胶囊形状并维护脚底辅助碰撞体的运行时模块。
        [NonSerialized] private ColliderShapeModule _shapeModule;
        // 提供统一地面过滤、接地和坡面命中的运行时模块。
        [NonSerialized] private GroundProbeModule _groundProbeModule;
        // 根据接地距离施加沿地面法线的弹簧与阻尼修正的运行时模块。
        [NonSerialized] private HoverModule _hoverModule;
        // 锁定不可行走坡面并执行上坡约束和下坡速度修正的运行时模块。
        [NonSerialized] private SteepSlopeSlideModule _steepSlopeSlideModule;
        // 从 Provider 黑板读取标准移动输入的策略私有契约。
        [NonSerialized] private IUnitMovementInput _movementInput;
        // 策略作为跳跃事件消费者维护的独立按下版本游标。
        [NonSerialized] private uint _jumpPressedVersion;
        // 起跳后短暂忽略接地探测的截止时间。
        [NonSerialized] private float _groundIgnoreUntil;
        // 最近一次完成固定步的只读状态快照。
        [NonSerialized] private UnitMovementState _state;
        // 最近一次由数据黑板转换出的通用移动命令。
        [NonSerialized] private UnitMovementCommand _lastCommand;
        // 最近一次尚未经过最终支撑与重力修正的候选速度。
        [NonSerialized] private Vector3 _lastCandidateVelocity;
        // 最近一次写入刚体的最终速度。
        [NonSerialized] private Vector3 _lastCommittedVelocity;

        /// <summary>获取默认地面策略的显示名称。</summary>
        public override string DisplayName => "普通地面移动";

        /// <summary>获取最近完成固定步的状态快照。</summary>
        public override UnitMovementState State => _state;

        /// <summary>获取最近一次由数据黑板转换得到的移动命令。</summary>
        public override UnitMovementCommand LastCommand => _lastCommand;

        /// <summary>获取最近一次标准移动速度候选值。</summary>
        public override Vector3 LastCandidateVelocity => _lastCandidateVelocity;

        /// <summary>获取最近一次提交给刚体的最终速度。</summary>
        public override Vector3 LastCommittedVelocity => _lastCommittedVelocity;

        /// <summary>获取边缘保护模块的 Gizmo 诊断快照。</summary>
        public override EdgeProtectionDebugState EdgeDebugState => _edgeProtectionModule != null
            ? _edgeProtectionModule.DebugState
            : null;

        /// <summary>获取用于编辑器预览的当前胶囊形状模块。</summary>
        public override ColliderShapeModule ShapeModule => _shapeModule;

        /// <summary>获取当前策略持有的浮动胶囊配置。</summary>
        public override FloatingCapsuleModule FloatingCapsuleModule => _floatingCapsuleModule;

        /// <summary>获取当前策略持有的接地配置。</summary>
        public override GroundSettings GroundSettings => _groundSettings;

        /// <summary>
        /// 创建标准地面策略首次运行所需的模块实例。
        /// </summary>
        protected override void OnInitialized()
        {
            // 确保旧序列化数据或新建策略都拥有独立的模块配置实例。
            EnsureConfiguration();
            _shapeModule = CreateOrReuseShapeModule(MovementCollider, OwnerTransform);

            // 每个策略只创建自己需要的运行时模块，模块之间保持单向依赖。
            _groundProbeModule = new GroundProbeModule(_shapeModule, OwnerTransform, PhysicsQuery, _groundSettings);
            _hoverModule = new HoverModule(_groundSettings, _groundProbeModule);
            _steepSlopeSlideModule = new SteepSlopeSlideModule(_groundSettings);
            _edgeProtectionModule.Initialize(_shapeModule, _groundProbeModule);
        }

        /// <summary>
        /// 按默认地面策略的模块组合顺序向 UnitMover 容器注入无参生命周期包装方法。
        /// </summary>
        protected override void OnRegisterLifecycle()
        {
            if (Lifecycle == null) return;

            // 策略负责选择阶段与回调顺序，UnitMover 只在自身生命周期中触发容器。
            Lifecycle.RegisterInitialize(InitializeMovementRuntime);
            Lifecycle.RegisterDependenciesChanged(RefreshMovementDependencies);
            Lifecycle.RegisterActivated(ActivateMovementRuntime);
            Lifecycle.RegisterFixedUpdate(SimulateMovement);
            Lifecycle.RegisterDeactivated(DeactivateMovementRuntime);
            Lifecycle.RegisterDisposed(DisposeMovementRuntime);
            Lifecycle.RegisterAuthoringValidate(SynchronizeAuthoringShape);
            Lifecycle.RegisterAuthoringRestore(RestoreAuthoringShape);
            Lifecycle.RegisterAuthoringRecapture(RecaptureAuthoringShape);
            Lifecycle.RegisterDrawGizmosSelected(DrawSelectedGizmos);
        }

        /// <summary>
        /// 初始化默认策略自身的输入缓存、重力基准和瞬态运动状态。
        /// </summary>
        private void InitializeMovementRuntime()
        {
            // 输入契约与重力基准均依赖已经注入的运行时依赖，初始化阶段只执行一次。
            CacheMovementInput();
            ClearState();
        }

        /// <summary>
        /// 在 Provider 或移动参考变更后刷新默认策略的输入契约与相机参考系。
        /// </summary>
        private void RefreshMovementDependencies()
        {
            // Provider 可能在运行时被 UnitMover 重新解析；仅在契约实例变化时重建独立输入游标。
            IUnitMovementInput nextMovementInput = DataProvider != null
                ? DataProvider.Blackboard as IUnitMovementInput
                : null;
            if (!ReferenceEquals(_movementInput, nextMovementInput))
            {
                _movementInput = nextMovementInput;
                if (_movementInput != null) _movementInput.InitializeJumpPressedCursor(ref _jumpPressedVersion);
            }

            // 移动参考可在运行时切换，相同输入契约也必须收到新的参考 Transform。
            if (_movementInput is IUnitMovementReferenceFrame referenceFrame)
                referenceFrame.MovementReference = MovementReference;
        }

        /// <summary>
        /// 在策略成为当前活动实例后重新同步其浮动胶囊和脚底辅助碰撞体。
        /// </summary>
        private void ActivateMovementRuntime()
        {
            // 切回缓存策略时，旧策略已归还共享胶囊，此处立即重新接管本策略的有效形状。
            _shapeModule?.Synchronize();
        }

        /// <summary>
        /// 执行普通地面角色的一次完整固定步移动编排。
        /// </summary>
        private void SimulateMovement()
        {
            if (Body == null || !Body.IsValid || _shapeModule == null || _groundProbeModule == null) return;

            // 容器在 FixedUpdate 前写入时间上下文，策略与模块调用始终保持无参。
            float fixedDeltaTime = Lifecycle != null ? Lifecycle.FixedDeltaTime : Time.fixedDeltaTime;
            float currentTime = Lifecycle != null ? Lifecycle.CurrentTime : Time.time;

            // 在所有物理探测前同步当前浮动胶囊有效形状和脚底辅助碰撞体。
            _shapeModule.Synchronize();
            GroundContact contact = currentTime < _groundIgnoreUntil
                ? new GroundContact(false, default)
                : _groundProbeModule.ProbeGround();

            // 跳跃主动阶段保持空中模式，避免范围探测到的地面提前取消上升过程。
            MovementMode mode = _jumpModule.IsJumping || !contact.HasContact
                ? MovementMode.Air
                : MovementMode.Ground;
            _state = _groundProbeModule.CreateMovementState(
                contact,
                mode,
                Body.Velocity,
                _jumpModule.IsJumping,
                _edgeProtectionModule.IsEnabled);

            // 仅由当前策略解释 Provider 黑板，未提供输入时按中性命令安全退化。
            UnitMovementCommand command = BuildInputCommand();
            _steepSlopeSlideModule.ConstrainUphillInput(ref command, contact, fixedDeltaTime);
            _lastCommand = command;
            _jumpModule.Update(_state, command, fixedDeltaTime, out bool startJump, out bool cutJump);

            // 先计算策略自身的平面移动，再交由边缘保护约束危险的前进分量。
            Vector3 candidateVelocity = BuildPlanarVelocity(_state, command, fixedDeltaTime);
            _lastCandidateVelocity = candidateVelocity;
            Vector3 candidateHorizontal = Vector3.ProjectOnPlane(candidateVelocity, Vector3.up);
            Vector3 currentHorizontal = Vector3.ProjectOnPlane(Body.Velocity, Vector3.up);
            _edgeProtectionModule.ConstrainVelocity(
                _state,
                candidateHorizontal,
                currentHorizontal,
                fixedDeltaTime,
                out Vector3 constrainedCandidate,
                out Vector3 constrainedCurrent);

            // 将边缘保护产生的水平约束稳定映射回接地法线切平面，保留策略的速度长度规则。
            bool isJumping = _jumpModule.IsJumping;
            bool hasSupportContact = _state.HasGroundContact && !isJumping;
            bool isWalkableGrounded = _state.IsGrounded && !isJumping;
            Vector3 supportNormal = hasSupportContact ? _state.GroundNormal : Vector3.up;
            Vector3 finalVelocity = ComposeConstrainedVelocity(
                candidateVelocity,
                candidateHorizontal,
                constrainedCandidate,
                constrainedCurrent,
                supportNormal,
                isWalkableGrounded);

            // 跳跃、重力、陡坡和悬浮均以增量方式叠加到本策略准备提交的最终速度。
            finalVelocity = ApplyJumpVelocity(finalVelocity, startJump, cutJump, currentTime);
            finalVelocity = _gravityModule.Apply(finalVelocity, hasSupportContact, fixedDeltaTime);
            finalVelocity = _steepSlopeSlideModule.Apply(finalVelocity, fixedDeltaTime);
            finalVelocity = _hoverModule.Apply(finalVelocity, contact, isJumping, fixedDeltaTime);

            // 本策略完成所有模块编排后只提交一次刚体速度，并保存对外只读诊断。
            Body.Commit(finalVelocity);
            _lastCommittedVelocity = finalVelocity;
            _state = new UnitMovementState(
                hasSupportContact,
                isWalkableGrounded,
                isWalkableGrounded && _state.IsStableGrounded,
                _state.GroundNormal,
                _state.GroundPoint,
                _state.GroundDistance,
                finalVelocity,
                isJumping ? MovementMode.Air : mode,
                isJumping);
        }

        /// <summary>
        /// 在策略切走前归还默认策略修改过的共享碰撞组件，保留缓存运动状态。
        /// </summary>
        private void DeactivateMovementRuntime()
        {
            // 策略缓存可以保留内部状态，但必须归还自己修改过的共享碰撞组件。
            _shapeModule?.RestoreAuthoringShape();
        }

        /// <summary>
        /// 在编辑器 Authoring 验证时同步当前策略的浮动胶囊有效形状。
        /// </summary>
        private void SynchronizeAuthoringShape()
        {
            CapsuleCollider movementCollider = AuthoringMovementCollider ?? MovementCollider;
            Transform ownerTransform = AuthoringOwnerTransform ?? OwnerTransform;
            if (movementCollider == null || ownerTransform == null) return;

            // 编辑模式复用策略自己的形状模块，UnitMover 只触发容器而不识别浮动胶囊。
            EnsureConfiguration();
            _shapeModule = CreateOrReuseShapeModule(movementCollider, ownerTransform);
            _shapeModule.Synchronize();
        }

        /// <summary>
        /// 在编辑器替换初始策略前归还本策略接管的胶囊与脚底辅助碰撞体。
        /// </summary>
        private void RestoreAuthoringShape()
        {
            CapsuleCollider movementCollider = AuthoringMovementCollider ?? MovementCollider;
            Transform ownerTransform = AuthoringOwnerTransform ?? OwnerTransform;
            if (movementCollider == null || ownerTransform == null) return;

            // Authoring 策略不经过运行时停用阶段，也必须先恢复共享组件再被序列化替换。
            EnsureConfiguration();
            _shapeModule = CreateOrReuseShapeModule(movementCollider, ownerTransform);
            _shapeModule.RestoreAuthoringShape();
        }

        /// <summary>
        /// 将当前 Authoring 主胶囊记录为浮动胶囊新的基础形状并重新同步预览。
        /// </summary>
        private void RecaptureAuthoringShape()
        {
            CapsuleCollider movementCollider = AuthoringMovementCollider ?? MovementCollider;
            if (movementCollider == null) return;

            // 显式重抓取只影响本策略持有的浮动胶囊 Authoring 数据。
            EnsureConfiguration();
            _floatingCapsuleModule.RecaptureBaseShape(movementCollider);
            SynchronizeAuthoringShape();
        }

        /// <summary>
        /// 在选中对象的 Scene 窗口中绘制本策略提供的只读形状与边缘诊断。
        /// </summary>
        private void DrawSelectedGizmos()
        {
            if (Lifecycle == null || !Lifecycle.IsScenePreviewEnabled) return;

            // Gizmo 绘制只读取当前模块状态，不触发形状写入、物理查询或策略固定步。
            CapsuleCollider movementCollider = AuthoringMovementCollider ?? MovementCollider;
            UnitMoverGizmoRenderer.DrawAll(
                movementCollider,
                _shapeModule,
                _floatingCapsuleModule,
                _groundSettings,
                EdgeDebugState,
                Lifecycle.IsEdgeDetectionGizmosEnabled);
        }

        /// <summary>
        /// 清除普通地面策略和其模块的全部运行时状态。
        /// </summary>
        public override void ClearState()
        {
            // 显式重置策略内部计时、输入游标和诊断，配置字段不受影响。
            _jumpPressedVersion = 0;
            _groundIgnoreUntil = 0f;
            _state = default;
            _lastCommand = UnitMovementCommand.CreateDefault();
            _lastCandidateVelocity = Vector3.zero;
            _lastCommittedVelocity = Vector3.zero;
            _jumpModule?.ResetRuntimeState();
            _gravityModule?.ResetRuntimeState();
            _steepSlopeSlideModule?.ResetRuntimeState();
            _edgeProtectionModule?.ResetRuntimeState();
            if (_movementInput != null) _movementInput.InitializeJumpPressedCursor(ref _jumpPressedVersion);

            // 重力基准是运行时依赖而不是可序列化配置；清状态后立即重新注入，保证检查点恢复与外部重置后仍保持下落行为。
            _gravityModule?.Initialize(Physics.gravity);
        }

        /// <summary>
        /// 在 UnitMover 完整释放当前策略时清除模块状态和全部注入依赖。
        /// </summary>
        private void DisposeMovementRuntime()
        {
            // 停用回调与最终释放都允许触发此处，恢复操作本身必须可重复执行。
            _shapeModule?.RestoreAuthoringShape();

            // 清除模块状态后再解除所有外部引用，避免缓存策略跨启用周期持有无效对象。
            ClearState();
            _movementInput = null;
            _shapeModule = null;
            _groundProbeModule = null;
            _hoverModule = null;
            _steepSlopeSlideModule = null;
            ClearInjectedDependencies();
        }

        /// <summary>
        /// 将当前刚体位置和旋转作为边缘保护模块的显式恢复检查点。
        /// </summary>
        public override void SetCheckpoint()
        {
            if (Body == null || !Body.IsValid || _edgeProtectionModule == null) return;

            // 检查点属于边缘保护模块，但由策略的公开行为统一转发。
            _edgeProtectionModule.SetCheckpoint(Body.Position, Body.Rotation);
        }

        /// <summary>
        /// 恢复到最近一次显式检查点，并清空本策略的瞬态运动数据。
        /// </summary>
        /// <returns>存在检查点且已完成刚体恢复时返回 true。</returns>
        public override bool RestoreCheckpoint()
        {
            if (Body == null || !Body.IsValid || _edgeProtectionModule == null) return false;
            if (!_edgeProtectionModule.TryGetCheckpoint(out CheckpointSnapshot checkpoint)) return false;

            // 恢复刚体后不能保留上一物理步输入、跳跃或速度诊断。
            Body.RestoreCheckpoint(checkpoint.Position, checkpoint.Rotation);
            ClearState();
            return true;
        }

        /// <summary>
        /// 确保策略序列化模块完整，兼容旧场景中缺失的嵌套引用。
        /// </summary>
        private void EnsureConfiguration()
        {
            if (_locomotionSettings == null) _locomotionSettings = new LocomotionSettings();
            if (_groundSettings == null) _groundSettings = new GroundSettings();
            if (_jumpModule == null) _jumpModule = new JumpModule();
            if (_gravityModule == null) _gravityModule = new GravityModule();
            if (_floatingCapsuleModule == null) _floatingCapsuleModule = new FloatingCapsuleModule();
            if (_edgeProtectionModule == null) _edgeProtectionModule = new EdgeProtectionModule();
            _floatingCapsuleModule.EnsureAuthoringState();
        }

        /// <summary>
        /// 创建或复用与当前主胶囊和浮动胶囊配置匹配的形状模块。
        /// </summary>
        /// <param name="movementCollider">当前主胶囊碰撞体。</param>
        /// <param name="ownerTransform">实体根 Transform。</param>
        /// <returns>可用于同步胶囊形状的模块；依赖缺失时返回 null。</returns>
        private ColliderShapeModule CreateOrReuseShapeModule(CapsuleCollider movementCollider, Transform ownerTransform)
        {
            if (movementCollider == null || ownerTransform == null) return null;
            if (_shapeModule != null
                && _shapeModule.MovementCollider == movementCollider
                && _shapeModule.FloatingCapsuleModule == _floatingCapsuleModule)
                return _shapeModule;

            // 形状模块只需碰撞体、宿主对象和本策略自己的浮动配置，不依赖 UnitMover。
            return new ColliderShapeModule(movementCollider, ownerTransform.gameObject, _floatingCapsuleModule);
        }

        /// <summary>
        /// 从当前 DataProvider 黑板缓存标准移动策略需要的输入与参考系契约。
        /// </summary>
        private void CacheMovementInput()
        {
            // 策略自己解释黑板，UnitMover 不读取 Blackboard 也不参与输入事件消费。
            _movementInput = DataProvider != null ? DataProvider.Blackboard as IUnitMovementInput : null;
            if (_movementInput == null) return;

            _movementInput.InitializeJumpPressedCursor(ref _jumpPressedVersion);
            if (_movementInput is IUnitMovementReferenceFrame referenceFrame)
                referenceFrame.MovementReference = MovementReference;
        }

        /// <summary>
        /// 将当前数据黑板读取为标准策略的单步移动命令。
        /// </summary>
        /// <returns>没有可用输入契约时返回中性移动命令。</returns>
        private UnitMovementCommand BuildInputCommand()
        {
            UnitMovementCommand command = UnitMovementCommand.CreateDefault();
            if (_movementInput == null) return command;

            // 跳跃按下事件由此策略独占消费，避免多个模块重复处理同一次输入。
            command.WorldMoveDirection = _movementInput.WorldMoveDirection;
            command.SpeedScale = Mathf.Max(0f, _movementInput.SpeedScale);
            command.IsJumpHeld = _movementInput.IsJumpHeld;
            if (_movementInput.ConsumeJumpPressed(ref _jumpPressedVersion, out bool pressed) && pressed)
                command.RequestJump = true;
            return command;
        }

        /// <summary>
        /// 根据当前支撑状态计算标准地面或空中的候选平面速度。
        /// </summary>
        /// <param name="state">当前固定步的状态快照。</param>
        /// <param name="command">本策略已解释的数据输入命令。</param>
        /// <param name="fixedDeltaTime">当前固定步时长，单位：秒。</param>
        /// <returns>尚未应用边缘、重力和悬浮修正的候选速度。</returns>
        private Vector3 BuildPlanarVelocity(
            in UnitMovementState state,
            in UnitMovementCommand command,
            float fixedDeltaTime)
        {
            if (state.Mode == MovementMode.Ground)
                return BuildGroundVelocity(state, command, fixedDeltaTime);
            return BuildAirVelocity(state, command, fixedDeltaTime);
        }

        /// <summary>
        /// 计算沿接地法线切平面的标准地面速度。
        /// </summary>
        /// <param name="state">当前固定步状态。</param>
        /// <param name="command">当前移动命令。</param>
        /// <param name="fixedDeltaTime">当前固定步时长，单位：秒。</param>
        /// <returns>标准地面候选速度。</returns>
        private Vector3 BuildGroundVelocity(in UnitMovementState state, in UnitMovementCommand command, float fixedDeltaTime)
        {
            Vector3 normal = state.GroundNormal.sqrMagnitude > 0.000001f ? state.GroundNormal : Vector3.up;
            Vector3 direction = Vector3.ProjectOnPlane(command.WorldMoveDirection, normal);
            if (direction.sqrMagnitude > 0.000001f) direction.Normalize();

            // 同向输入使用加速度，松开或反向输入使用减速度，保证普通地面手感稳定。
            float speedScale = Mathf.Max(0f, command.SpeedScale);
            Vector3 targetVelocity = direction * _locomotionSettings.GroundMaxSpeed * speedScale;
            Vector3 currentVelocity = Vector3.ProjectOnPlane(state.CurrentVelocity, normal);
            bool isAccelerating = direction.sqrMagnitude > 0.000001f
                && Vector3.Dot(currentVelocity, targetVelocity) >= 0f;
            float acceleration = isAccelerating
                ? _locomotionSettings.GroundAcceleration
                : _locomotionSettings.GroundDeceleration;
            return Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * fixedDeltaTime);
        }

        /// <summary>
        /// 计算受空中控制比例限制的标准空中速度。
        /// </summary>
        /// <param name="state">当前固定步状态。</param>
        /// <param name="command">当前移动命令。</param>
        /// <param name="fixedDeltaTime">当前固定步时长，单位：秒。</param>
        /// <returns>标准空中候选速度。</returns>
        private Vector3 BuildAirVelocity(in UnitMovementState state, in UnitMovementCommand command, float fixedDeltaTime)
        {
            Vector3 direction = Vector3.ProjectOnPlane(command.WorldMoveDirection, Vector3.up);
            if (direction.sqrMagnitude > 0.000001f) direction.Normalize();

            // 空中移动始终只在水平面调整，垂直速度继续由跳跃和重力模块控制。
            float speedScale = Mathf.Max(0f, command.SpeedScale);
            Vector3 targetVelocity = direction * _locomotionSettings.AirMaxSpeed * speedScale;
            Vector3 currentVelocity = Vector3.ProjectOnPlane(state.CurrentVelocity, Vector3.up);
            float controlAcceleration = _locomotionSettings.AirAcceleration * _locomotionSettings.AirControl;
            return Vector3.MoveTowards(currentVelocity, targetVelocity, controlAcceleration * fixedDeltaTime);
        }

        /// <summary>
        /// 将边缘保护的水平约束映射为当前支撑法线上的完整速度。
        /// </summary>
        /// <param name="candidateVelocity">策略原始候选速度。</param>
        /// <param name="candidateHorizontal">原始候选的世界水平分量。</param>
        /// <param name="constrainedCandidate">边缘保护后的候选水平速度。</param>
        /// <param name="constrainedCurrent">边缘保护后的当前水平速度。</param>
        /// <param name="supportNormal">当前支撑面法线。</param>
        /// <param name="isWalkableGrounded">当前是否稳定站在可行走地面上。</param>
        /// <returns>已保留支撑法线速度的组合结果。</returns>
        private Vector3 ComposeConstrainedVelocity(
            Vector3 candidateVelocity,
            Vector3 candidateHorizontal,
            Vector3 constrainedCandidate,
            Vector3 constrainedCurrent,
            Vector3 supportNormal,
            bool isWalkableGrounded)
        {
            // 边缘保护在水平面工作，地面策略负责把其结果映射回真实坡面切线空间。
            Vector3 constrainedTangent = Vector3.ProjectOnPlane(constrainedCandidate, supportNormal);
            if (isWalkableGrounded && candidateHorizontal.sqrMagnitude > 0.000001f)
            {
                if ((constrainedCandidate - candidateHorizontal).sqrMagnitude <= 0.000001f)
                    constrainedTangent = candidateVelocity;
                else if (constrainedTangent.sqrMagnitude > 0.000001f)
                {
                    float constraintRatioSquared = Mathf.Clamp01(
                        constrainedCandidate.sqrMagnitude / candidateHorizontal.sqrMagnitude);
                    float targetTangentSqrMagnitude = candidateVelocity.sqrMagnitude * constraintRatioSquared;
                    constrainedTangent *= Mathf.Sqrt(targetTangentSqrMagnitude / constrainedTangent.sqrMagnitude);
                }
            }

            // 垂直或法线方向的既有速度不应被边缘保护的平面约束意外删除。
            Vector3 adjustedCurrentVelocity = new Vector3(
                constrainedCurrent.x,
                Body.Velocity.y,
                constrainedCurrent.z);
            Vector3 normalVelocity = Vector3.Project(adjustedCurrentVelocity, supportNormal);
            return constrainedTangent + normalVelocity;
        }

        /// <summary>
        /// 处理起跳初速度、提前松键截断和起跳后接地豁免时间。
        /// </summary>
        /// <param name="velocity">应用跳跃前的组合速度。</param>
        /// <param name="startJump">本步是否应开始跳跃。</param>
        /// <param name="cutJump">本步是否应截断上升速度。</param>
        /// <param name="currentTime">Unity 当前时间。</param>
        /// <returns>已应用跳跃规则的速度。</returns>
        private Vector3 ApplyJumpVelocity(Vector3 velocity, bool startJump, bool cutJump, float currentTime)
        {
            if (startJump)
            {
                // 仅补足当前上升速度差，避免连续接地造成跳跃速度叠加。
                float upwardSpeed = Vector3.Dot(velocity, Vector3.up);
                float jumpDelta = Mathf.Max(0f, _jumpModule.InitialSpeed - upwardSpeed);
                _groundIgnoreUntil = currentTime + _jumpModule.GroundIgnoreAfterStartDuration;
                return velocity + Vector3.up * jumpDelta;
            }

            // 提前松键时只由 JumpModule 标记一次截断，策略只负责应用速度结果。
            if (cutJump && velocity.y > 0f)
                return new Vector3(velocity.x, velocity.y * _jumpModule.CutMultiplier, velocity.z);
            return velocity;
        }
    }
}
