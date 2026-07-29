using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 组装纯 C# 运动能力模块、命令源和运动模式，并执行固定步运动管线。
    /// </summary>
    public sealed class UnitMovementRuntime : IDisposable
    {
        // 统一提交运动结果的刚体边界。
        private readonly IUnitBody _body;
        // 同步实际 Collider 形状并提供边界尺寸的模块。
        private readonly ColliderShapeModule _shapeModule;
        // 提供接地和统一地面过滤的模块。
        private readonly GroundProbeModule _groundProbe;
        // 维护跳跃缓冲、土狼时间和截断状态的模块。
        private readonly JumpModule _jumpModule;
        // 在接地时施加悬浮弹簧修正的模块。
        private readonly HoverModule _hoverModule;
        // 在空中施加重力并限制下落速度的模块。
        private readonly GravityModule _gravityModule;
        // 约束危险边缘速度并记录安全快照的模块。
        private readonly EdgeProtectionModule _edgeProtection;
        // 命令来源标识到纯 C# 实例的显式注册表。
        private readonly Dictionary<string, IUnitMovementCommandSource> _commandSources
            = new Dictionary<string, IUnitMovementCommandSource>();
        // 策略类型到可复用纯 C# 策略实例的运行时缓存。
        private readonly Dictionary<Type, UnitMovementStrategy> _movementStrategies
            = new Dictionary<Type, UnitMovementStrategy>();
        // 创建或复用策略实例时需要注入的通用水平移动配置。
        private readonly LocomotionSettings _locomotionSettings;
        // 正在激活的命令来源标识。
        private string _activeCommandSourceId;
        // 是否有业务层显式提交且等待本物理步消费的命令。
        private bool _hasSubmittedCommand;
        // 等待本物理步消费的一次性外部命令。
        private UnitMovementCommand _submittedCommand;
        // 当前负责解释通用移动命令的唯一策略实例。
        private UnitMovementStrategy _activeMovementStrategy;
        // 起跳后短暂忽略地面 Cast 的截止时间。
        private float _groundIgnoreUntil;
        // 最近一次已完成固定步的状态快照。
        private UnitMovementState _state;
        // 最近一次由当前命令来源合并完成的通用移动命令。
        private UnitMovementCommand _lastCommand;
        // 最近一次策略计算出的未受边缘约束候选速度。
        private Vector3 _lastCandidateVelocity;
        // 最近一次实际提交给刚体的最终速度。
        private Vector3 _lastCommittedVelocity;

        /// <summary>
        /// 以 Unity 适配器和各功能配置构造完整的纯 C# 运动运行时。
        /// </summary>
        /// <param name="body">统一提交刚体结果的边界。</param>
        /// <param name="shapeModule">当前有效 Collider 形状模块。</param>
        /// <param name="groundProbe">地面查询模块。</param>
        /// <param name="profile">聚合策略与接地参数的运动 Profile。</param>
        /// <param name="jumpModule">保存跳跃配置和瞬态状态的模块。</param>
        /// <param name="gravityModule">保存重力配置和运行时重力基准的模块。</param>
        /// <param name="edgeProtectionModule">保存边缘保护配置和安全位置状态的模块。</param>
        /// <param name="initialMovementStrategy">由 Inspector 选择并用于初始化缓存的默认移动策略。</param>
        public UnitMovementRuntime(
            IUnitBody body,
            ColliderShapeModule shapeModule,
            GroundProbeModule groundProbe,
            UnitMovementProfile profile,
            JumpModule jumpModule,
            GravityModule gravityModule,
            EdgeProtectionModule edgeProtectionModule,
            UnitMovementStrategy initialMovementStrategy)
        {
            _body = body;
            _shapeModule = shapeModule;
            _groundProbe = groundProbe;

            // 核心模块均只接收自己需要的配置，避免依赖整个 Profile。
            LocomotionSettings locomotion = profile != null ? profile.Locomotion : null;
            GroundSettings ground = profile != null ? profile.Ground : null;

            _jumpModule = jumpModule ?? new JumpModule();
            _jumpModule.ResetRuntimeState();
            _hoverModule = new HoverModule(ground, groundProbe);
            _gravityModule = gravityModule ?? new GravityModule();
            _gravityModule.Initialize(Physics.gravity);
            _edgeProtection = edgeProtectionModule ?? new EdgeProtectionModule();
            _edgeProtection.Initialize(shapeModule, groundProbe);
            _edgeProtection.ResetRuntimeState();
            _locomotionSettings = locomotion;
            SetInitialMovementStrategy(initialMovementStrategy);
        }

        /// <summary>获取最近完成物理步的只读运动状态。</summary>
        public UnitMovementState State => _state;

        /// <summary>获取当前激活命令来源的标识；未激活时为 null。</summary>
        public string ActiveCommandSourceId => _activeCommandSourceId;

        /// <summary>获取当前正在解释通用移动命令的策略名称。</summary>
        public string ActiveMovementStrategyName => _activeMovementStrategy != null
            ? _activeMovementStrategy.DisplayName
            : null;

        /// <summary>获取最近一次命令来源合并完成的通用移动命令。</summary>
        public UnitMovementCommand LastCommand => _lastCommand;

        /// <summary>获取最近一次策略计算的候选速度。</summary>
        public Vector3 LastCandidateVelocity => _lastCandidateVelocity;

        /// <summary>获取最近一次提交给刚体的最终速度。</summary>
        public Vector3 LastCommittedVelocity => _lastCommittedVelocity;

        /// <summary>获取边缘防跌落模块用于 Gizmos 的诊断数据。</summary>
        public EdgeProtectionDebugState EdgeDebugState => _edgeProtection.DebugState;

        /// <summary>
        /// 向注册表添加唯一命令来源；来源实例在切换时不重建，因而保留自身运行时状态。
        /// </summary>
        /// <param name="id">调用方定义的唯一命令来源标识。</param>
        /// <param name="source">需要注册的纯 C# 命令来源。</param>
        /// <param name="activate">是否在注册后立即切换为激活来源。</param>
        public void RegisterCommandSource(string id, IUnitMovementCommandSource source, bool activate = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("命令来源标识不能为空。", nameof(id));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (_commandSources.ContainsKey(id))
                throw new InvalidOperationException($"命令来源已注册：{id}");

            _commandSources.Add(id, source);
            source.OnRegistered(this);
            if (activate) ActivateCommandSource(id);
        }

        /// <summary>
        /// 替换指定标识的命令来源，并按完整生命周期卸载旧实例。
        /// </summary>
        /// <param name="id">需要替换的唯一命令来源标识。</param>
        /// <param name="source">新的纯 C# 命令来源。</param>
        public void ReplaceCommandSource(string id, IUnitMovementCommandSource source)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("命令来源标识不能为空。", nameof(id));
            if (source == null) throw new ArgumentNullException(nameof(source));

            bool wasActive = string.Equals(_activeCommandSourceId, id, StringComparison.Ordinal);
            if (_commandSources.TryGetValue(id, out IUnitMovementCommandSource previous))
            {
                if (wasActive) previous.OnDeactivated();
                previous.OnUnregistered();
                _commandSources.Remove(id);
            }

            _commandSources.Add(id, source);
            source.OnRegistered(this);
            if (wasActive) source.OnActivated(_state);
        }

        /// <summary>
        /// 切换到已经注册的命令来源，不创建新实例也不复制字段状态。
        /// </summary>
        /// <param name="id">需要激活的命令来源标识。</param>
        /// <returns>是否找到并成功激活该命令来源。</returns>
        public bool ActivateCommandSource(string id)
        {
            if (!_commandSources.TryGetValue(id, out IUnitMovementCommandSource nextSource)) return false;
            if (string.Equals(_activeCommandSourceId, id, StringComparison.Ordinal)) return true;

            if (!string.IsNullOrEmpty(_activeCommandSourceId)
                && _commandSources.TryGetValue(_activeCommandSourceId, out IUnitMovementCommandSource currentSource))
                currentSource.OnDeactivated();

            _activeCommandSourceId = id;
            nextSource.OnActivated(_state);
            return true;
        }

        /// <summary>
        /// 从注册表移除命令来源，并在移除激活来源时先执行停用回调。
        /// </summary>
        /// <param name="id">需要注销的命令来源标识。</param>
        /// <returns>是否找到并注销了该命令来源。</returns>
        public bool UnregisterCommandSource(string id)
        {
            if (!_commandSources.TryGetValue(id, out IUnitMovementCommandSource source)) return false;

            if (string.Equals(_activeCommandSourceId, id, StringComparison.Ordinal))
            {
                source.OnDeactivated();
                _activeCommandSourceId = null;
            }

            source.OnUnregistered();
            _commandSources.Remove(id);
            return true;
        }

        /// <summary>
        /// 提交一次只保留到下一个固定步的通用命令，适用于无独立命令来源的简单桥接场景。
        /// </summary>
        /// <param name="command">需要在下一固定步消费的移动命令。</param>
        public void SubmitCommand(in UnitMovementCommand command)
        {
            _submittedCommand = command;
            _hasSubmittedCommand = true;
        }

        /// <summary>
        /// 按策略类型选择当前移动策略；首次使用时创建实例，后续切回时复用原实例及其运行时状态。
        /// </summary>
        /// <typeparam name="TStrategy">需要选择的具体移动策略类型。</typeparam>
        /// <returns>当前生效且已缓存的策略实例。</returns>
        public TStrategy UseMovementStrategy<TStrategy>()
            where TStrategy : UnitMovementStrategy, new()
        {
            Type strategyType = typeof(TStrategy);
            if (!_movementStrategies.TryGetValue(strategyType, out UnitMovementStrategy strategy))
            {
                strategy = new TStrategy();
                CacheMovementStrategy(strategyType, strategy);
            }

            _activeMovementStrategy = strategy;
            return (TStrategy)strategy;
        }

        /// <summary>
        /// 清空指定缓存策略实例持有的全部运行时状态，不移除缓存也不切换当前策略。
        /// </summary>
        /// <typeparam name="TStrategy">需要清空状态的具体移动策略类型。</typeparam>
        /// <returns>是否已找到并清空该策略实例。</returns>
        public bool ClearMovementStrategyState<TStrategy>()
            where TStrategy : UnitMovementStrategy
        {
            if (!_movementStrategies.TryGetValue(typeof(TStrategy), out UnitMovementStrategy strategy)) return false;

            strategy.ClearState();
            return true;
        }

        /// <summary>
        /// 执行一次完整固定步：更新形状和接地、构建命令、计算约束并统一提交结果。
        /// </summary>
        /// <param name="fixedDeltaTime">当前固定步时长，单位：秒。</param>
        /// <param name="currentTime">Unity 当前时间，用于短暂忽略起跳后接地。</param>
        public void Simulate(float fixedDeltaTime, float currentTime)
        {
            if (_body == null || !_body.IsValid || _shapeModule == null || _groundProbe == null) return;

            // 在所有探测前先确保实际 Collider 形状与浮动胶囊配置一致。
            _shapeModule.Synchronize();
            GroundContact contact = currentTime < _groundIgnoreUntil
                ? new GroundContact(false, default)
                : _groundProbe.ProbeGround();
            if (_activeMovementStrategy == null) return;

            MovementMode mode = _activeMovementStrategy.ResolveMovementMode(contact.HasContact);
            _state = CreateState(contact, mode, _body.Velocity, _jumpModule.IsJumping);

            // 稳定支撑会更新可回退安全位置；离地时先检查是否需要提前结束本步。
            _edgeProtection.UpdateSafePosition(_state, _body.Position, _body.Rotation);
            if (_edgeProtection.TryGetFallRecovery(_state, out SafePositionSnapshot safePosition))
            {
                _body.RestoreSafePosition(safePosition.Position, safePosition.Rotation);
                _hasSubmittedCommand = false;
                _submittedCommand = UnitMovementCommand.CreateDefault();
                _lastCommand = UnitMovementCommand.CreateDefault();
                _lastCandidateVelocity = Vector3.zero;
                _lastCommittedVelocity = Vector3.zero;
                return;
            }

            UnitMovementCommand command = ConsumeCommand();
            _lastCommand = command;
            _jumpModule.Update(_state, command, fixedDeltaTime, out bool startJump, out bool cutJump);

            Vector3 candidateVelocity = _activeMovementStrategy.BuildPlanarVelocity(
                _state,
                command,
                fixedDeltaTime);
            _lastCandidateVelocity = candidateVelocity;
            Vector3 candidateHorizontal = Vector3.ProjectOnPlane(candidateVelocity, Vector3.up);
            Vector3 currentHorizontal = Vector3.ProjectOnPlane(_body.Velocity, Vector3.up);
            _edgeProtection.ConstrainVelocity(
                _state,
                candidateHorizontal,
                currentHorizontal,
                fixedDeltaTime,
                out Vector3 constrainedCandidate,
                out Vector3 constrainedCurrent);

            bool useGroundNormal = _state.IsGrounded && !startJump;
            Vector3 supportNormal = useGroundNormal ? _state.GroundNormal : Vector3.up;
            Vector3 constrainedTangent = Vector3.ProjectOnPlane(constrainedCandidate, supportNormal);
            Vector3 adjustedCurrentVelocity = new Vector3(
                constrainedCurrent.x,
                _body.Velocity.y,
                constrainedCurrent.z);
            Vector3 normalVelocity = Vector3.Project(adjustedCurrentVelocity, supportNormal);
            Vector3 finalVelocity = constrainedTangent + normalVelocity;

            if (startJump)
            {
                float upwardSpeed = Vector3.Dot(finalVelocity, Vector3.up);
                float jumpDelta = Mathf.Max(0f, GetJumpInitialSpeed() - upwardSpeed);
                finalVelocity += Vector3.up * jumpDelta;
                _groundIgnoreUntil = currentTime + 0.1f;
                _edgeProtection.NotifyJumpStarted();
            }
            else if (cutJump && finalVelocity.y > 0f)
                finalVelocity = new Vector3(
                    finalVelocity.x,
                    finalVelocity.y * GetJumpCutMultiplier(),
                    finalVelocity.z);

            if (useGroundNormal)
                finalVelocity = _hoverModule.Apply(finalVelocity, _state, fixedDeltaTime);
            else
                finalVelocity = _gravityModule.Apply(finalVelocity, false, fixedDeltaTime);

            _body.Commit(finalVelocity);
            _lastCommittedVelocity = finalVelocity;
            _state = new UnitMovementState(
                useGroundNormal,
                useGroundNormal,
                _state.GroundNormal,
                _state.GroundPoint,
                _state.GroundDistance,
                finalVelocity,
                startJump ? MovementMode.Air : mode,
                _jumpModule.IsJumping);
        }

        /// <summary>
        /// 停用所有命令来源、释放它们的注册关系并恢复底层刚体设置。
        /// </summary>
        public void Dispose()
        {
            if (!string.IsNullOrEmpty(_activeCommandSourceId)
                && _commandSources.TryGetValue(_activeCommandSourceId, out IUnitMovementCommandSource activeSource))
                activeSource.OnDeactivated();

            foreach (KeyValuePair<string, IUnitMovementCommandSource> pair in _commandSources)
                pair.Value.OnUnregistered();

            _commandSources.Clear();
            _activeCommandSourceId = null;
            ClearAllMovementStrategyStates();
            _movementStrategies.Clear();
            _activeMovementStrategy = null;
            _jumpModule.ResetRuntimeState();
            _gravityModule.ResetRuntimeState();
            _edgeProtection.ResetRuntimeState();
            _body?.RestoreInitialSettings();
        }

        /// <summary>
        /// 合并临时提交命令和当前激活来源产生的命令，并保证下一物理步不会继承旧命令。
        /// </summary>
        /// <returns>当前固定步可供运动模式消费的命令。</returns>
        private UnitMovementCommand ConsumeCommand()
        {
            UnitMovementCommand command = _hasSubmittedCommand
                ? _submittedCommand
                : UnitMovementCommand.CreateDefault();
            _hasSubmittedCommand = false;
            _submittedCommand = UnitMovementCommand.CreateDefault();

            if (!string.IsNullOrEmpty(_activeCommandSourceId)
                && _commandSources.TryGetValue(_activeCommandSourceId, out IUnitMovementCommandSource source))
                source.BuildCommand(_state, ref command);

            command.SpeedScale = Mathf.Max(0f, command.SpeedScale);
            return command;
        }

        /// <summary>
        /// 缓存 Inspector 选择的初始策略，并将其设为当前唯一生效策略。
        /// </summary>
        /// <param name="strategy">由 UnitMover 序列化字段提供的初始策略实例。</param>
        private void SetInitialMovementStrategy(UnitMovementStrategy strategy)
        {
            if (strategy == null) strategy = new DefaultRigidbodyMovementStrategy();

            Type strategyType = strategy.GetType();
            CacheMovementStrategy(strategyType, strategy);
            _activeMovementStrategy = strategy;
        }

        /// <summary>
        /// 初始化并保存一个尚未缓存的策略实例。
        /// </summary>
        /// <param name="strategyType">策略实例的具体运行时类型。</param>
        /// <param name="strategy">需要缓存的策略实例。</param>
        private void CacheMovementStrategy(Type strategyType, UnitMovementStrategy strategy)
        {
            strategy.Initialize(_locomotionSettings);
            _movementStrategies.Add(strategyType, strategy);
        }

        /// <summary>
        /// 清空当前运行时缓存的全部策略实例状态。
        /// </summary>
        private void ClearAllMovementStrategyStates()
        {
            foreach (KeyValuePair<Type, UnitMovementStrategy> pair in _movementStrategies)
                pair.Value.ClearState();
        }

        /// <summary>
        /// 根据地面命中和当前刚体速度创建供本固定步使用的状态快照。
        /// </summary>
        /// <param name="contact">经过过滤的接地结果。</param>
        /// <param name="mode">本物理步选定的运动模式。</param>
        /// <param name="velocity">刚体开始时的线速度。</param>
        /// <param name="isJumping">是否处于主动跳跃阶段。</param>
        /// <returns>已填充地面数据的只读状态快照。</returns>
        private static UnitMovementState CreateState(
            GroundContact contact,
            MovementMode mode,
            Vector3 velocity,
            bool isJumping)
        {
            return new UnitMovementState(
                contact.HasContact,
                contact.HasContact,
                contact.HasContact ? contact.Hit.normal : Vector3.up,
                contact.HasContact ? contact.Hit.point : Vector3.zero,
                contact.HasContact ? contact.Distance : float.PositiveInfinity,
                velocity,
                mode,
                isJumping);
        }

        /// <summary>
        /// 从当前 Profile 的跳跃配置读取起跳速度，配置缺失时返回零。
        /// </summary>
        /// <returns>本次普通跳跃应达到的初始向上速度。</returns>
        private float GetJumpInitialSpeed()
        {
            return _jumpModule != null ? _jumpModule.InitialSpeed : 0f;
        }

        /// <summary>
        /// 从当前 Profile 的跳跃配置读取截断速度比例，配置缺失时返回一。
        /// </summary>
        /// <returns>松开跳跃键后保留的向上速度比例。</returns>
        private float GetJumpCutMultiplier()
        {
            return _jumpModule != null ? _jumpModule.CutMultiplier : 1f;
        }
    }
}
