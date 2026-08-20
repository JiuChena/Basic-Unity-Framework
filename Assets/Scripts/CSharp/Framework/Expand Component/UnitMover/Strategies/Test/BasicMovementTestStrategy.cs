using System;
using Framework.ExpandComponent.DataProvider;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 仅用于验证策略字段递归显示与基础平面移动链路的最小移动策略。
    /// </summary>
    [Serializable]
    public sealed class BasicMovementTestStrategy : UnitMovementStrategy
    {
        // 基础平面速度配置，也是 Inspector 递归显示的嵌套序列化字段。
        [UnitMovementModuleName("测试移动参数")]
        [Tooltip("基础测试策略使用的最大速度、加速与减速配置")]
        [SerializeField] private LocomotionSettings _locomotionSettings = new LocomotionSettings();
        // 基础重力倍率与最大下落速度配置。
        [UnitMovementModuleName("重力")]
        [Tooltip("基础测试策略的重力倍率与最大下落速度配置")]
        [SerializeField] private GravityModule _gravityModule = new GravityModule();

        // 当前策略从 Provider 黑板缓存的通用移动输入契约。
        [NonSerialized] private IUnitMovementInput _movementInput;
        // 最近一次从输入契约构建出的只读移动命令。
        [NonSerialized] private UnitMovementCommand _lastCommand;
        // 最近一次计算得到的平面候选速度。
        [NonSerialized] private Vector3 _lastCandidateVelocity;
        // 最近一次提交给刚体适配器的最终速度。
        [NonSerialized] private Vector3 _lastCommittedVelocity;

        /// <summary>获取基础移动测试策略在 Inspector 中显示的名称。</summary>
        public override string DisplayName => "基础移动测试策略";

        /// <summary>获取最近一次从数据黑板读取的移动命令。</summary>
        public override UnitMovementCommand LastCommand => _lastCommand;

        /// <summary>获取最近一次计算得到的平面候选速度。</summary>
        public override Vector3 LastCandidateVelocity => _lastCandidateVelocity;

        /// <summary>获取最近一次提交给刚体的最终速度。</summary>
        public override Vector3 LastCommittedVelocity => _lastCommittedVelocity;

        /// <summary>
        /// 确保测试策略首次创建时拥有完整的嵌套配置实例。
        /// </summary>
        protected override void OnInitialized()
        {
            // 兼容旧序列化数据或手动清空嵌套配置后的测试策略实例。
            if (_locomotionSettings == null) _locomotionSettings = new LocomotionSettings();

            // 记录项目重力基准，固定步中由重力模块统一结算垂直速度。
            _gravityModule.Initialize(Physics.gravity);
        }

        /// <summary>
        /// 按基础测试策略的需求向 UnitMover 容器注入无参生命周期包装方法。
        /// </summary>
        protected override void OnRegisterLifecycle()
        {
            if (Lifecycle == null) return;

            // 测试策略只注入自身实际需要的初始化、依赖刷新、固定步和最终释放阶段。
            Lifecycle.RegisterInitialize(InitializeMovementRuntime);
            Lifecycle.RegisterDependenciesChanged(RefreshMovementDependencies);
            Lifecycle.RegisterFixedUpdate(SimulateMovement);
            Lifecycle.RegisterDisposed(DisposeMovementRuntime);
        }

        /// <summary>
        /// 初始化测试策略的输入缓存和可选诊断状态。
        /// </summary>
        private void InitializeMovementRuntime()
        {
            // 初始化阶段只在策略首次加入缓存时执行一次。
            RefreshMovementInput();
            ClearState();
        }

        /// <summary>
        /// 执行包含基础平面移动与重力结算的 XZ 移动，垂直方向由重力模块统一处理。
        /// </summary>
        private void SimulateMovement()
        {
            if (Body == null || !Body.IsValid) return;

            // 固定步时长由 UnitMover 在容器上下文中更新，包装回调本身保持无参。
            float fixedDeltaTime = Lifecycle != null ? Lifecycle.FixedDeltaTime : Time.fixedDeltaTime;

            // 测试策略只读取通用输入契约；缺少契约时使用中性命令自然减速。
            _lastCommand = BuildMovementCommand();
            Vector3 direction = Vector3.ProjectOnPlane(_lastCommand.WorldMoveDirection, Vector3.up);
            if (direction.sqrMagnitude > 0.000001f) direction.Normalize();

            // 根据输入方向、目标速度和地面加减速配置计算当前物理步的平面速度。
            Vector3 currentPlanarVelocity = Vector3.ProjectOnPlane(Body.Velocity, Vector3.up);
            float speedScale = Mathf.Max(0f, _lastCommand.SpeedScale);
            Vector3 targetVelocity = direction * _locomotionSettings.GroundMaxSpeed * speedScale;
            float acceleration = direction.sqrMagnitude > 0.000001f
                ? _locomotionSettings.GroundAcceleration
                : _locomotionSettings.GroundDeceleration;
            _lastCandidateVelocity = Vector3.MoveTowards(
                currentPlanarVelocity,
                targetVelocity,
                acceleration * fixedDeltaTime);

            // 水平移动只替换 XZ 分量，垂直方向由重力模块统一结算，避免策略接管后失去下落。
            Vector3 planarVelocity = new Vector3(
                _lastCandidateVelocity.x,
                Body.Velocity.y,
                _lastCandidateVelocity.z);
            _lastCommittedVelocity = _gravityModule.Apply(planarVelocity, false, fixedDeltaTime);
            Body.Commit(_lastCommittedVelocity);
        }

        /// <summary>
        /// 清空测试策略保存的输入与速度诊断，不修改 Inspector 配置。
        /// </summary>
        public override void ClearState()
        {
            // 运行时状态仅用于策略缓存验证和 Inspector 诊断，重置后下一物理步重新写入。
            _lastCommand = UnitMovementCommand.CreateDefault();
            _lastCandidateVelocity = Vector3.zero;
            _lastCommittedVelocity = Vector3.zero;

            // 重力基准是运行时依赖而不是可序列化配置；清状态后立即重新注入，保证重置后仍保持下落行为。
            _gravityModule.ResetRuntimeState();
            _gravityModule.Initialize(Physics.gravity);
        }

        /// <summary>
        /// 释放测试策略缓存的输入引用、诊断状态与 UnitMover 注入依赖。
        /// </summary>
        private void DisposeMovementRuntime()
        {
            // 纯 C# 测试策略不持有 Unity 模块，只清除自身运行时引用。
            ClearState();
            _movementInput = null;
            _gravityModule.ResetRuntimeState();
            ClearInjectedDependencies();
        }

        /// <summary>
        /// 从当前 Provider 的黑板缓存移动输入，并写入可选的移动参考系。
        /// </summary>
        private void RefreshMovementDependencies()
        {
            // 依赖刷新阶段重用与首次初始化相同的输入契约转换规则。
            RefreshMovementInput();
        }

        /// <summary>
        /// 从当前 Provider 的黑板缓存移动输入，并写入可选的移动参考系。
        /// </summary>
        private void RefreshMovementInput()
        {
            // Blackboard 的具体类型由策略自行解释，UnitMover 只负责传入 IDataProvider 引用。
            _movementInput = DataProvider != null ? DataProvider.Blackboard as IUnitMovementInput : null;
            if (_movementInput is IUnitMovementReferenceFrame referenceFrame)
                referenceFrame.MovementReference = MovementReference;
        }

        /// <summary>
        /// 将当前输入契约转换为不含跳跃与业务语义的基础移动命令。
        /// </summary>
        /// <returns>缺少输入契约时返回速度倍率为一且方向为零的中性命令。</returns>
        private UnitMovementCommand BuildMovementCommand()
        {
            UnitMovementCommand command = UnitMovementCommand.CreateDefault();
            if (_movementInput == null) return command;

            // 测试策略仅读取平面方向和速度倍率，跳跃事件完全不消费。
            command.WorldMoveDirection = _movementInput.WorldMoveDirection;
            command.SpeedScale = Mathf.Max(0f, _movementInput.SpeedScale);
            return command;
        }
    }
}
