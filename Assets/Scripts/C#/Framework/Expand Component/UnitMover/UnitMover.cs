using Framework.ExpandComponent.DataProvider;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 作为 Unity 生命周期与序列化引用入口，组装纯 C# 运动模块并转发固定步执行。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class UnitMover : MonoBehaviour
    {
        // 由 UnitMover 接管并在固定步末端统一写入的刚体。
        [Tooltip("由 UnitMover 接管速度和重力的 Rigidbody 组件")]
        [SerializeField] private Rigidbody _rigidbody;
        // 参与接地和边缘保护查询的实际碰撞体。
        [Tooltip("参与移动物理检测的 CapsuleCollider 或 BoxCollider 组件")]
        [SerializeField] private Collider _movementCollider;
        // 向 UnitMover 提供通用移动输入黑板的同对象 DataProvider 组件。
        [Tooltip("提供 IUnitMovementInput 黑板的同对象 DataProvider；手动指定时优先使用，为空时自动查找")]
        [SerializeField] private MonoBehaviour _dataProvider;
        // 按功能大类聚合的可序列化纯 C# 运动配置。
        [Tooltip("包含移动策略和接地参数的模块化配置")]
        [SerializeField] private UnitMovementProfile _profile = new UnitMovementProfile();
        // 保存跳跃参数和本次运行瞬态状态的可序列化模块。
        [Tooltip("包含普通跳跃参数和运行时状态的模块")]
        [SerializeField] private JumpModule _jumpModule = new JumpModule();
        // 保存重力参数和本次运行重力基准的可序列化模块。
        [Tooltip("包含重力参数和运行时状态的模块")]
        [SerializeField] private GravityModule _gravityModule = new GravityModule();
        // 保存边缘保护参数、安全位置和诊断状态的可序列化模块。
        [Tooltip("包含边缘防跌落参数和运行时状态的模块")]
        [SerializeField] private EdgeProtectionModule _edgeProtectionModule = new EdgeProtectionModule();
        // 保存浮动胶囊参数和组件专属基础形状快照的可序列化模块。
        [Tooltip("包含浮动胶囊、脚底 BoxCollider 和基础形状快照的模块")]
        [SerializeField] private FloatingCapsuleModule _floatingCapsuleModule = new FloatingCapsuleModule();
        // 由开发者在 Inspector 选择的初始纯 C# 移动策略，不包含业务输入读取职责。
        [Tooltip("运行时首次启用的纯 C# 移动策略；可在代码中通过泛型接口切换并复用缓存实例")]
        [SerializeReference] private UnitMovementStrategy _movementStrategy
            = new DefaultRigidbodyMovementStrategy();
        // 是否在 Scene 视图绘制浮动间隙、接地和边缘保护诊断数据。
        [Tooltip("是否在 Scene 窗口绘制浮动间隙、悬浮高度和边缘保护预览")]
        [SerializeField] private bool _showScenePreview = true;
        // 是否绘制边缘防跌落模块实际执行的支撑和危险检测射线。
        [Tooltip("是否在运行时的 Scene 窗口绘制边缘防跌落检测射线；绿色为安全支撑，红色为危险缺口")]
        [SerializeField] private bool _showEdgeDetectionGizmos = true;

        // 运行时唯一的纯 C# 运动管线，编辑模式不创建。
        private UnitMovementRuntime _runtime;
        // 编辑模式和运行时共用的形状同步模块。
        private ColliderShapeModule _shapeModule;
        // 记录过缺失组件错误，避免每帧重复输出日志。
        private bool _reportedMissingDependencies;
        // 当前被 UnitMover 缓存并消费移动输入的同对象数据 Provider。
        private IDataProvider _movementDataProvider;
        // 当前数据 Provider 黑板暴露的通用移动输入读取契约。
        private IUnitMovementInput _movementInput;
        // UnitMover 作为当前黑板消费者独立维护的跳跃按下事件游标。
        private uint _jumpPressedVersion;

        /// <summary>获取最近完成固定步的只读运动状态；未运行时返回默认状态。</summary>
        public UnitMovementState State => _runtime != null ? _runtime.State : default;

        /// <summary>获取当前是否已经创建运行时运动管线。</summary>
        public bool IsRuntimeReady => _runtime != null;

        /// <summary>获取当前激活命令来源的标识；无来源时为 null。</summary>
        public string ActiveCommandSourceId => _runtime != null ? _runtime.ActiveCommandSourceId : null;

        /// <summary>获取当前是否正在直接消费已绑定 DataProvider 的移动输入黑板。</summary>
        public bool IsDataProviderInputActive => _movementDataProvider is Behaviour behaviour
            && behaviour.isActiveAndEnabled
            && _movementInput != null;

        /// <summary>获取当前正在生效的移动策略名称；未运行时返回 Inspector 选择的策略名称。</summary>
        public string ActiveMovementStrategyName => _runtime != null
            ? _runtime.ActiveMovementStrategyName
            : _movementStrategy != null ? _movementStrategy.DisplayName : null;

        /// <summary>获取最近一次命令来源合并后的移动命令；未运行时返回默认命令。</summary>
        public UnitMovementCommand LastCommand => _runtime != null
            ? _runtime.LastCommand
            : UnitMovementCommand.CreateDefault();

        /// <summary>获取最近一次移动策略计算出的候选速度；未运行时返回零。</summary>
        public Vector3 LastCandidateVelocity => _runtime != null ? _runtime.LastCandidateVelocity : Vector3.zero;

        /// <summary>获取最近一次提交给 Rigidbody 的最终速度；未运行时返回零。</summary>
        public Vector3 LastCommittedVelocity => _runtime != null ? _runtime.LastCommittedVelocity : Vector3.zero;

        /// <summary>获取当前 Rigidbody 的位置与旋转约束；未配置时返回 None。</summary>
        public RigidbodyConstraints RigidbodyConstraints => _rigidbody != null
            ? _rigidbody.constraints
            : RigidbodyConstraints.None;

        #region Unity Lifecycle

        /// <summary>
        /// 在组件初始化时解析引用、确保配置存在并同步编辑器可见的有效胶囊形状。
        /// </summary>
        private void Awake()
        {
            ResolveReferences();
            EnsureAuthoringData();
            SynchronizeColliderShape();
        }

        /// <summary>
        /// 仅在运行模式创建纯 C# 运动管线，编辑模式只保留形状同步和 Gizmos。
        /// </summary>
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            CreateRuntime();
        }

        /// <summary>
        /// 在禁用时释放命令来源并恢复 UnitMover 接管前的刚体设置。
        /// </summary>
        private void OnDisable()
        {
            DisposeRuntime();
        }

        /// <summary>
        /// 在销毁时确保运行时已释放，避免外部命令来源残留订阅。
        /// </summary>
        private void OnDestroy()
        {
            DisposeRuntime();
        }

        /// <summary>
        /// 在 Inspector 修改时保持浮动胶囊顶端对齐；不执行任何刚体或运动逻辑。
        /// </summary>
        private void OnValidate()
        {
            ResolveReferences();
            EnsureAuthoringData();
            SynchronizeColliderShape();
        }

        /// <summary>
        /// 在每个物理步将 Unity 时间转交给纯 C# 运动管线。
        /// </summary>
        private void FixedUpdate()
        {
            if (!Application.isPlaying) return;

            if (_runtime == null)
                CreateRuntime();
            if (_runtime == null) return;

            SubmitDataProviderCommand();
            _runtime.Simulate(Time.fixedDeltaTime, Time.time);
        }

        /// <summary>
        /// 为新添加组件解析默认刚体与碰撞体引用，并创建默认模块化配置。
        /// </summary>
        private void Reset()
        {
            ResolveReferences();
            EnsureAuthoringData();
            SynchronizeColliderShape();
        }

        /// <summary>
        /// 在选中对象时绘制浮动间隙、接地和边缘诊断预览。
        /// 此回调只读取状态，不能创建、更新或删除任何 Collider 组件。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!_showScenePreview) return;

            // Gizmo 渲染只消费已存在的模块和运行时快照，不参与组件同步或物理模拟。
            UnitMoverGizmoRenderer.DrawAll(
                _movementCollider,
                _shapeModule,
                _floatingCapsuleModule,
                _profile != null ? _profile.Ground : null,
                _runtime != null ? _runtime.EdgeDebugState : null,
                _showEdgeDetectionGizmos);
        }

        #endregion

        #region Command Sources

        /// <summary>
        /// 注册纯 C# 命令来源，例如玩家输入、AI 导航或网络回放来源。
        /// </summary>
        /// <param name="id">调用方定义的唯一命令来源标识。</param>
        /// <param name="source">不具备挂载职责的命令来源实例。</param>
        /// <param name="activate">是否在注册后立即作为当前命令来源。</param>
        public void RegisterCommandSource(string id, IUnitMovementCommandSource source, bool activate = false)
        {
            RequireRuntime();
            _runtime.RegisterCommandSource(id, source, activate);
        }

        /// <summary>
        /// 替换已有命令来源并按完整生命周期清理旧实例。
        /// </summary>
        /// <param name="id">需要替换的命令来源标识。</param>
        /// <param name="source">新的纯 C# 命令来源实例。</param>
        public void ReplaceCommandSource(string id, IUnitMovementCommandSource source)
        {
            RequireRuntime();
            _runtime.ReplaceCommandSource(id, source);
        }

        /// <summary>
        /// 激活已注册命令来源并保留其原有运行时状态。
        /// </summary>
        /// <param name="id">需要激活的命令来源标识。</param>
        /// <returns>是否成功找到并激活命令来源。</returns>
        public bool ActivateCommandSource(string id)
        {
            RequireRuntime();
            return _runtime.ActivateCommandSource(id);
        }

        /// <summary>
        /// 注销命令来源并在其为当前来源时自动停用。
        /// </summary>
        /// <param name="id">需要注销的命令来源标识。</param>
        /// <returns>是否成功注销命令来源。</returns>
        public bool UnregisterCommandSource(string id)
        {
            RequireRuntime();
            return _runtime.UnregisterCommandSource(id);
        }

        /// <summary>
        /// 提交只保留到下一固定步的通用移动命令，适用于简单桥接或测试。
        /// </summary>
        /// <param name="command">需要由运动管线消费的通用移动命令。</param>
        public void SubmitCommand(in UnitMovementCommand command)
        {
            RequireRuntime();
            _runtime.SubmitCommand(command);
        }

        #endregion

        #region Movement Strategies

        /// <summary>
        /// 按具体策略类型切换当前移动策略；首次使用时创建实例，后续再次选择时复用原实例与其状态。
        /// </summary>
        /// <typeparam name="TStrategy">需要启用的具体纯 C# 移动策略类型。</typeparam>
        /// <returns>当前生效且已缓存的策略实例。</returns>
        public TStrategy UseMovementStrategy<TStrategy>()
            where TStrategy : UnitMovementStrategy, new()
        {
            RequireRuntime();
            return _runtime.UseMovementStrategy<TStrategy>();
        }

        /// <summary>
        /// 清空指定缓存策略持有的全部运行时状态，但保留该实例供后续复用。
        /// </summary>
        /// <typeparam name="TStrategy">需要清空状态的具体纯 C# 移动策略类型。</typeparam>
        /// <returns>是否已找到并清空该策略实例。</returns>
        public bool ClearMovementStrategyState<TStrategy>()
            where TStrategy : UnitMovementStrategy
        {
            RequireRuntime();
            return _runtime.ClearMovementStrategyState<TStrategy>();
        }

        #endregion

        #region Runtime Assembly

        /// <summary>
        /// 解析未显式配置的同对象刚体、支持的移动碰撞体和兼容的数据 Provider 引用。
        /// </summary>
        private void ResolveReferences()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_movementCollider == null) _movementCollider = GetComponent<CapsuleCollider>();
            if (_movementCollider == null) _movementCollider = GetComponent<BoxCollider>();
            if (_dataProvider == null) _dataProvider = FindCompatibleDataProvider();
        }

        /// <summary>
        /// 确保旧 Prefab 或新增组件在反序列化后拥有所需的模块化配置对象。
        /// </summary>
        private void EnsureAuthoringData()
        {
            if (_profile == null) _profile = new UnitMovementProfile();
            _profile.EnsureModules();
            if (_movementStrategy == null) _movementStrategy = new DefaultRigidbodyMovementStrategy();
            if (_jumpModule == null) _jumpModule = new JumpModule();
            if (_gravityModule == null) _gravityModule = new GravityModule();
            if (_edgeProtectionModule == null) _edgeProtectionModule = new EdgeProtectionModule();
            if (_floatingCapsuleModule == null) _floatingCapsuleModule = new FloatingCapsuleModule();
            _floatingCapsuleModule.EnsureAuthoringState();
        }

        /// <summary>
        /// 创建形状、物理查询、接地和运动运行时对象，并接管 Rigidbody 的必要物理设置。
        /// </summary>
        private void CreateRuntime()
        {
            if (_runtime != null) return;

            ResolveReferences();
            EnsureAuthoringData();
            if (!HasRequiredDependencies()) return;

            _shapeModule = new ColliderShapeModule(
                _movementCollider,
                gameObject,
                _floatingCapsuleModule);
            _shapeModule.Synchronize();

            IUnitBody body = new RigidbodyUnitBody(_rigidbody);
            IPhysicsQuery physicsQuery = new UnityPhysicsQuery();
            GroundProbeModule groundProbe = new GroundProbeModule(
                _shapeModule,
                transform,
                physicsQuery,
                _profile.Ground);
            _runtime = new UnitMovementRuntime(
                body,
                _shapeModule,
                groundProbe,
                _profile,
                _jumpModule,
                _gravityModule,
                _edgeProtectionModule,
                _movementStrategy);
            ResolveMovementDataProvider();
            _reportedMissingDependencies = false;
        }

        /// <summary>
        /// 释放运行时模块；形状模块保留到编辑器预览同步时重新创建。
        /// </summary>
        private void DisposeRuntime()
        {
            if (_runtime == null) return;

            _runtime.Dispose();
            _runtime = null;
            _movementDataProvider = null;
            _movementInput = null;
            _jumpPressedVersion = 0;
        }

        /// <summary>
        /// 在编辑或运行时将浮动胶囊设置同步到实际 Collider，保证顶部对齐且底部留空。
        /// </summary>
        public void SynchronizeColliderShape()
        {
            if (_movementCollider == null || _floatingCapsuleModule == null) return;

            if (_shapeModule == null
                || _shapeModule.MovementCollider != _movementCollider
                || _shapeModule.FloatingCapsuleModule != _floatingCapsuleModule)
                _shapeModule = new ColliderShapeModule(
                    _movementCollider,
                    gameObject,
                    _floatingCapsuleModule);

            _shapeModule.Synchronize();
        }

        /// <summary>
        /// 验证运行时组装所需的 Rigidbody 与支持的 Collider 是否都已配置。
        /// </summary>
        /// <returns>依赖是否足以创建运动运行时。</returns>
        private bool HasRequiredDependencies()
        {
            bool supportedCollider = _movementCollider is CapsuleCollider || _movementCollider is BoxCollider;
            if (_rigidbody != null && supportedCollider) return true;
            if (_reportedMissingDependencies) return false;

            Debug.LogError("UnitMover 需要 Rigidbody 与 CapsuleCollider 或 BoxCollider 才能创建运动运行时。", this);
            _reportedMissingDependencies = true;
            return false;
        }

        /// <summary>
        /// 优先使用 Inspector 指定的 DataProvider；引用为空或失效时，再扫描同一 GameObject 的兼容 Provider。
        /// 解析仅发生在 UnitMover 初始化和运行时创建时，后续物理步直接读取已缓存的黑板数据。
        /// </summary>
        private void ResolveMovementDataProvider()
        {
            _movementDataProvider = null;
            _movementInput = null;
            _jumpPressedVersion = 0;
            if (TryResolveMovementDataProvider(_dataProvider)) return;

            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                MonoBehaviour component = components[index];
                if (component == _dataProvider) continue;
                if (TryResolveMovementDataProvider(component)) return;
            }
        }

        /// <summary>
        /// 验证一个 Unity 组件是否为可用的移动数据 Provider，并在通过验证后缓存其黑板输入契约。
        /// </summary>
        /// <param name="component">需要验证并尝试缓存的同对象组件。</param>
        /// <returns>组件提供了可用移动输入并已完成缓存时返回 true。</returns>
        private bool TryResolveMovementDataProvider(MonoBehaviour component)
        {
            if (component == null || !component.isActiveAndEnabled) return false;
            if (component.gameObject != gameObject) return false;
            if (component is not IDataProvider provider) return false;
            if (provider.Blackboard is not IUnitMovementInput movementInput) return false;

            _movementDataProvider = provider;
            _movementInput = movementInput;
            _movementInput.InitializeJumpPressedCursor(ref _jumpPressedVersion);
            return true;
        }

        /// <summary>
        /// 将当前缓存的 DataProvider 黑板数据直接提交给本物理步运行时。
        /// 该路径不依赖命令来源注册表，确保实体基础输入始终由 UnitMover 主动消费。
        /// </summary>
        private void SubmitDataProviderCommand()
        {
            // Unity 组件启用顺序不保证 DataProvider 必然早于 UnitMover。
            // 当运行时在 Provider 尚未激活时创建，首个可用物理步必须主动完成重新绑定。
            if (!IsDataProviderInputActive)
                ResolveMovementDataProvider();
            if (!IsDataProviderInputActive) return;

            UnitMovementCommand command = UnitMovementCommand.CreateDefault();
            command.WorldMoveDirection = _movementInput.WorldMoveDirection;
            command.SpeedScale = Mathf.Max(0f, _movementInput.SpeedScale);
            command.IsJumpHeld = _movementInput.IsJumpHeld;
            if (_movementInput.ConsumeJumpPressed(ref _jumpPressedVersion, out bool pressed) && pressed)
                command.RequestJump = true;

            _runtime.SubmitCommand(command);
        }

        /// <summary>
        /// 查找同一 GameObject 上第一个提供通用移动输入黑板的 DataProvider 组件。
        /// </summary>
        /// <returns>找到时返回其 Unity 组件引用，否则返回 null。</returns>
        private MonoBehaviour FindCompatibleDataProvider()
        {
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                MonoBehaviour component = components[index];
                if (component is not IDataProvider provider) continue;
                if (provider.Blackboard is IUnitMovementInput) return component;
            }

            return null;
        }

        /// <summary>
        /// 确保业务桥接只在 UnitMover 已启用并创建运行时后注册命令来源或模式。
        /// </summary>
        private void RequireRuntime()
        {
            if (_runtime != null) return;
            throw new System.InvalidOperationException("UnitMover 运行时尚未就绪，请在 UnitMover.OnEnable 之后注册命令来源或运动模式。");
        }

        #endregion

    }
}
