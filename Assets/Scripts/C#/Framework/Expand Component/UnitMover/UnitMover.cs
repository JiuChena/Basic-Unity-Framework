using System;
using System.Collections.Generic;
using Framework.ExpandComponent.DataProvider;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 承载 Unity 组件引用并管理可缓存纯 C# 移动策略的生命周期。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class UnitMover : MonoBehaviour
    {
        // 由当前移动策略通过刚体适配器统一提交速度的刚体组件。
        [Tooltip("由 UnitMover 解析并交给移动策略接管的 Rigidbody 组件")]
        [SerializeField] private Rigidbody _rigidbody;
        // 当前 UnitMover 唯一支持的主胶囊碰撞体。
        [Tooltip("UnitMover 唯一支持的主 CapsuleCollider；用于形状同步和物理探测")]
        [SerializeField] private CapsuleCollider _movementCollider;
        // 提供实体数据的同对象 Provider；手动指定时优先。
        [Tooltip("同对象 IDataProvider 引用；为空时自动解析唯一 Provider，不解释其黑板内容")]
        [SerializeField] private MonoBehaviour _dataProvider;
        // 向当前策略传递世界空间移动方向参考的可选摄像机。
        [Tooltip("可选移动参考摄像机；其 Transform 会显式传递给当前移动策略")]
        [SerializeField] private Camera _movementReferenceCamera;
        // UnitMover 接管期间是否冻结刚体旋转。
        [Tooltip("是否在 UnitMover 启用期间冻结 Rigidbody 旋转")]
        [SerializeField] private bool _freezeRigidbodyRotation = true;
        // Inspector 选择的初始移动策略，首次启用时加入策略缓存。
        [Tooltip("首次启用的纯 C# 移动策略；运行时可通过泛型 API 切换并复用缓存实例")]
        [SerializeReference] private UnitMovementStrategy _movementStrategy = new NormalGroundMovementStrategy();
        // 是否在 Scene 窗口显示当前策略提供的形状与接地预览。
        [Tooltip("是否在 Scene 窗口绘制当前策略的浮动胶囊、接地与边缘保护预览")]
        [SerializeField] private bool _showScenePreview = true;
        // 是否绘制当前策略边缘保护模块保存的实际检测诊断。
        [Tooltip("是否绘制边缘防跌落的运行时检测射线；绿色为安全支撑，红色为危险缺口")]
        [SerializeField] private bool _showEdgeDetectionGizmos = true;

        // 策略类型到当前启用周期可复用策略实例的缓存。
        private readonly Dictionary<Type, UnitMovementStrategy> _movementStrategies = new Dictionary<Type, UnitMovementStrategy>();
        // UnitMover 唯一持有的策略生命周期容器，切换后清空并由目标策略重新注入回调。
        private readonly UnitMoverLifecycleContainer _lifecycleContainer = new UnitMoverLifecycleContainer();
        // 当前实际执行固定步的策略实例。
        private UnitMovementStrategy _activeMovementStrategy;
        // 当前启用周期唯一的刚体写入适配器。
        private IUnitBody _body;
        // 当前启用周期各策略复用的无分配物理查询适配器。
        private IPhysicsQuery _physicsQuery;
        // 解析并传递给策略的实体数据 Provider，不读取其 Blackboard。
        private IDataProvider _resolvedDataProvider;
        // 避免在依赖缺失时每个固定步重复输出错误。
        private bool _reportedMissingDependencies;
        // 避免同对象多 Provider 歧义在反复启用时刷屏。
        private bool _reportedAmbiguousDataProvider;

        /// <summary>获取最近完成固定步的只读运动状态；没有激活策略时返回默认值。</summary>
        public UnitMovementState State => _activeMovementStrategy != null ? _activeMovementStrategy.State : default;

        /// <summary>获取当前是否已经成功完成策略和 Unity 适配器的运行时组装。</summary>
        public bool IsRuntimeReady => _body != null && _body.IsValid && _activeMovementStrategy != null;

        /// <summary>获取当前激活策略的显示名称；未运行时回退到 Inspector 初始策略。</summary>
        public string ActiveMovementStrategyName => _activeMovementStrategy != null
            ? _activeMovementStrategy.DisplayName
            : _movementStrategy != null ? _movementStrategy.DisplayName : null;

        /// <summary>获取最近一次当前策略读取或构建的通用移动命令。</summary>
        public UnitMovementCommand LastCommand => _activeMovementStrategy != null
            ? _activeMovementStrategy.LastCommand
            : UnitMovementCommand.CreateDefault();

        /// <summary>获取当前策略最近一次计算的候选速度。</summary>
        public Vector3 LastCandidateVelocity => _activeMovementStrategy != null
            ? _activeMovementStrategy.LastCandidateVelocity
            : Vector3.zero;

        /// <summary>获取当前策略最近一次提交给刚体的最终速度。</summary>
        public Vector3 LastCommittedVelocity => _activeMovementStrategy != null
            ? _activeMovementStrategy.LastCommittedVelocity
            : Vector3.zero;

        /// <summary>获取当前刚体的位置和旋转约束；没有刚体时返回 None。</summary>
        public RigidbodyConstraints RigidbodyConstraints => _rigidbody != null
            ? _rigidbody.constraints
            : RigidbodyConstraints.None;

        /// <summary>获取 UnitMover 当前指定并用于策略形状同步的主 CapsuleCollider。</summary>
        public CapsuleCollider MovementCollider => _movementCollider;

        /// <summary>
        /// 获取或设置移动参考摄像机；运行中设置后在下一次策略初始化或切换时生效。
        /// </summary>
        public Camera MovementReferenceCamera
        {
            get => _movementReferenceCamera;
            set
            {
                if (_movementReferenceCamera == value) return;

                // 相机引用是 UnitMover 解析的外部依赖，变更后仅转交给当前策略处理，不重建其运行时模块。
                _movementReferenceCamera = value;
                if (_activeMovementStrategy == null) return;

                // 先刷新策略持有的外部引用，再由容器触发策略注入的依赖更新包装方法。
                _activeMovementStrategy.RefreshRuntimeDependencies(
                    _resolvedDataProvider,
                    _movementReferenceCamera != null ? _movementReferenceCamera.transform : null);
                _lifecycleContainer.InvokeDependenciesChanged();
            }
        }

        #region Unity Lifecycle

        /// <summary>
        /// 在组件初始化时解析 Unity 依赖并同步初始策略的编辑器 Authoring 形状。
        /// </summary>
        private void Awake()
        {
            ResolveReferences();
            EnsureInitialStrategy();
            SynchronizeColliderShape();
        }

        /// <summary>
        /// 在播放模式创建 Unity 适配器、缓存初始策略并激活它。
        /// </summary>
        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            CreateRuntime();
        }

        /// <summary>
        /// 在停用时依次停用并释放所有缓存策略，再恢复刚体原始设置。
        /// </summary>
        private void OnDisable()
        {
            DisposeRuntime();
        }

        /// <summary>
        /// 在销毁时确保策略与刚体适配器已释放。
        /// </summary>
        private void OnDestroy()
        {
            DisposeRuntime();
        }

        /// <summary>
        /// 在 Inspector 修改后重新解析引用并仅同步编辑器可见的策略形状。
        /// </summary>
        private void OnValidate()
        {
            ResolveReferences();
            EnsureInitialStrategy();
            SynchronizeColliderShape();
        }

        /// <summary>
        /// 在播放模式将普通帧上下文写入容器，并调用当前策略注入的 Update 回调。
        /// </summary>
        private void Update()
        {
            if (!Application.isPlaying || !IsRuntimeReady) return;

            // UnitMover 只提供时间上下文并触发容器，不解释策略或模块功能。
            _lifecycleContainer.SetFrameContext(Time.deltaTime, Time.fixedDeltaTime, Time.time);
            _lifecycleContainer.InvokeUpdate();
        }

        /// <summary>
        /// 在播放模式将固定步上下文写入容器，并调用当前策略注入的 FixedUpdate 回调。
        /// </summary>
        private void FixedUpdate()
        {
            if (!Application.isPlaying) return;

            // 运行时在异常禁用或依赖后置初始化时允许重新组装一次。
            if (!IsRuntimeReady) CreateRuntime();
            if (!IsRuntimeReady) return;
            _lifecycleContainer.SetFrameContext(Time.deltaTime, Time.fixedDeltaTime, Time.time);
            _lifecycleContainer.InvokeFixedUpdate();
        }

        /// <summary>
        /// 在播放模式调用当前策略注入的 LateUpdate 回调。
        /// </summary>
        private void LateUpdate()
        {
            if (!Application.isPlaying || !IsRuntimeReady) return;

            // LateUpdate 与其他阶段共用同一帧上下文，不产生额外策略或模块查找。
            _lifecycleContainer.InvokeLateUpdate();
        }

        /// <summary>
        /// 为新添加组件填充默认 Unity 引用和初始策略，再同步 Authoring 形状。
        /// </summary>
        private void Reset()
        {
            ResolveReferences();
            EnsureInitialStrategy();
            SynchronizeColliderShape();
        }

        /// <summary>
        /// 在选中对象时读取当前策略的只读数据绘制 Scene 诊断预览。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!_showScenePreview) return;

            // 编辑模式临时绑定初始策略，播放模式使用当前活动策略已经注入的容器回调。
            UnitMovementStrategy previewStrategy = _activeMovementStrategy ?? _movementStrategy;
            if (previewStrategy == null) return;
            if (!Application.isPlaying) PrepareAuthoringLifecycle(previewStrategy);

            // Gizmo 开关是 Unity 外壳数据；具体绘制由策略注入的容器回调决定。
            _lifecycleContainer.SetGizmoContext(_showScenePreview, _showEdgeDetectionGizmos);
            _lifecycleContainer.InvokeDrawGizmosSelected();
        }

        #endregion

        #region Strategy Management

        /// <summary>
        /// 使用指定类型的移动策略；首次使用创建并缓存，后续切回时保留其运行时状态。
        /// </summary>
        /// <typeparam name="TStrategy">需要激活的具体纯 C# 移动策略类型。</typeparam>
        /// <returns>当前激活的策略实例；运行时依赖未准备好时返回 null。</returns>
        public TStrategy UseMovementStrategy<TStrategy>() where TStrategy : UnitMovementStrategy, new()
        {
            if (!Application.isPlaying) return null;
            if (!IsRuntimeReady) CreateRuntime();
            if (!IsRuntimeReady) return null;

            // 策略按具体类型缓存，同类型重复选择不重复激活也不会重置实例数据。
            Type strategyType = typeof(TStrategy);
            if (!_movementStrategies.TryGetValue(strategyType, out UnitMovementStrategy strategy))
            {
                // 首次初始化目标策略前必须先归还旧策略修改的共享组件，避免捕获到浮动后的胶囊形状。
                DeactivateActiveStrategy();
                _lifecycleContainer.Clear();
                strategy = new TStrategy();
                CacheStrategy(strategyType, strategy);
                ActivatePreparedStrategy(strategy, true);
                return (TStrategy)strategy;
            }

            ActivateStrategy(strategy, false);
            return (TStrategy)strategy;
        }

        /// <summary>
        /// 使用运行时指定的具体移动策略类型，供 Inspector 或策略选择 UI 在播放期间切换策略。
        /// </summary>
        /// <param name="strategyType">非抽象且具有公开无参构造函数的具体策略类型。</param>
        /// <returns>切换后活动的策略实例；类型或运行时依赖无效时返回 null。</returns>
        public UnitMovementStrategy UseMovementStrategy(Type strategyType)
        {
            if (!Application.isPlaying) return null;
            if (!IsValidMovementStrategyType(strategyType)) return null;
            if (!IsRuntimeReady) CreateRuntime();
            if (!IsRuntimeReady) return null;

            // Type 选择只在显式切换时创建实例，固定步仍只调用活动策略的 Simulate。
            if (!_movementStrategies.TryGetValue(strategyType, out UnitMovementStrategy strategy))
            {
                DeactivateActiveStrategy();
                _lifecycleContainer.Clear();
                strategy = Activator.CreateInstance(strategyType) as UnitMovementStrategy;
                if (strategy == null) return null;
                CacheStrategy(strategyType, strategy);
                ActivatePreparedStrategy(strategy, true);
                return strategy;
            }

            ActivateStrategy(strategy, false);
            return strategy;
        }

        /// <summary>
        /// 显式清空指定缓存策略的运行时数据，不切换策略也不移除缓存。
        /// </summary>
        /// <typeparam name="TStrategy">需要清空状态的具体策略类型。</typeparam>
        /// <returns>找到目标缓存策略并完成清空时返回 true。</returns>
        public bool ClearMovementStrategyState<TStrategy>() where TStrategy : UnitMovementStrategy
        {
            if (!_movementStrategies.TryGetValue(typeof(TStrategy), out UnitMovementStrategy strategy)) return false;

            // 状态清理必须由调用方显式触发，策略切换本身保留缓存状态。
            strategy.ClearState();
            return true;
        }

        /// <summary>
        /// 将当前位置和旋转交给当前策略记录为显式检查点。
        /// </summary>
        public void SetCheckpoint()
        {
            _activeMovementStrategy?.SetCheckpoint();
        }

        /// <summary>
        /// 请求当前策略恢复其最近一次显式记录的检查点。
        /// </summary>
        /// <returns>当前策略存在并成功恢复检查点时返回 true。</returns>
        public bool RestoreCheckpoint()
        {
            return _activeMovementStrategy != null && _activeMovementStrategy.RestoreCheckpoint();
        }

        /// <summary>
        /// 将调用方刚写入的主胶囊形状记录为初始策略的浮动胶囊基础形状，并立即同步预览。
        /// </summary>
        public void RecaptureFloatingCapsuleBaseShape()
        {
            if (_movementCollider == null) return;

            // Authoring 数据属于策略，UnitMover 仅准备容器并触发策略已注入的重捕获回调。
            UnitMovementStrategy strategy = _activeMovementStrategy ?? _movementStrategy;
            if (strategy == null) return;
            if (!Application.isPlaying) PrepareAuthoringLifecycle(strategy);
            else strategy.SetAuthoringDependencies(_movementCollider, transform);
            _lifecycleContainer.InvokeAuthoringRecapture();
        }

        #endregion

        #region Runtime Assembly

        /// <summary>
        /// 解析未显式指定的刚体、胶囊和唯一同对象数据 Provider。
        /// </summary>
        private void ResolveReferences()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_movementCollider == null) _movementCollider = GetComponent<CapsuleCollider>();
            _resolvedDataProvider = ResolveDataProvider();
        }

        /// <summary>
        /// 确保 Inspector 初始策略始终存在，以便编辑模式形状同步和运行时缓存均有入口。
        /// </summary>
        private void EnsureInitialStrategy()
        {
            if (_movementStrategy == null) _movementStrategy = new NormalGroundMovementStrategy();
        }

        /// <summary>
        /// 创建当前启用周期的 Unity 适配器、缓存初始策略并激活初始策略。
        /// </summary>
        private void CreateRuntime()
        {
            if (IsRuntimeReady) return;

            // 运行时组装阶段一次性解析 Unity 依赖，固定步中不重复 GetComponent 或扫描 Provider。
            ResolveReferences();
            EnsureInitialStrategy();
            if (!HasRequiredDependencies()) return;

            _body = new RigidbodyUnitBody(_rigidbody, _freezeRigidbodyRotation);
            _physicsQuery = new UnityPhysicsQuery();
            CacheStrategy(_movementStrategy.GetType(), _movementStrategy);
            _lifecycleContainer.Clear();
            ActivatePreparedStrategy(_movementStrategy, true);
            _reportedMissingDependencies = false;
        }

        /// <summary>
        /// 缓存一个新策略并显式传递已经解析完成的 Unity 和数据依赖。
        /// </summary>
        /// <param name="strategyType">策略具体运行时类型。</param>
        /// <param name="strategy">尚未加入当前缓存的策略实例。</param>
        private void CacheStrategy(Type strategyType, UnitMovementStrategy strategy)
        {
            // 初始化只发生在实例首次加入当前启用周期缓存时，切换回缓存策略不会重复创建模块。
            strategy.Initialize(
                _rigidbody,
                _movementCollider,
                transform,
                _body,
                _physicsQuery,
                _resolvedDataProvider,
                _movementReferenceCamera != null ? _movementReferenceCamera.transform : null);
            _movementStrategies.Add(strategyType, strategy);
        }

        /// <summary>
        /// 切换到已缓存的目标策略，并在清空旧回调后重新注入目标策略的生命周期方法。
        /// </summary>
        /// <param name="nextStrategy">已缓存且已经初始化的目标策略。</param>
        /// <param name="invokeInitialize">目标策略是否需要触发首次初始化阶段。</param>
        private void ActivateStrategy(UnitMovementStrategy nextStrategy, bool invokeInitialize)
        {
            if (nextStrategy == null || ReferenceEquals(_activeMovementStrategy, nextStrategy)) return;

            // 切换不清空旧策略模块状态，但容器必须先执行停用回调并完全清空旧策略注册。
            DeactivateActiveStrategy();
            _lifecycleContainer.Clear();
            ActivatePreparedStrategy(nextStrategy, invokeInitialize);
        }

        /// <summary>
        /// 将已完成模块构造的策略绑定到唯一容器，依次触发初始化、依赖刷新和激活阶段。
        /// </summary>
        /// <param name="nextStrategy">已缓存且依赖字段可用的目标策略。</param>
        /// <param name="invokeInitialize">目标策略是否首次创建并需要执行初始化阶段。</param>
        private void ActivatePreparedStrategy(UnitMovementStrategy nextStrategy, bool invokeInitialize)
        {
            if (nextStrategy == null) return;

            // UnitMover 只注入依赖、清空并触发容器；策略自行向容器注册模块包装方法。
            _activeMovementStrategy = nextStrategy;
            _activeMovementStrategy.SetAuthoringDependencies(_movementCollider, transform);
            _activeMovementStrategy.RefreshRuntimeDependencies(
                _resolvedDataProvider,
                _movementReferenceCamera != null ? _movementReferenceCamera.transform : null);
            _activeMovementStrategy.BindLifecycle(_lifecycleContainer);
            if (invokeInitialize) _lifecycleContainer.InvokeInitialize();
            _lifecycleContainer.InvokeDependenciesChanged();
            _lifecycleContainer.InvokeActivated();
        }

        /// <summary>
        /// 停用当前策略并清除活动引用，使后续策略初始化只接触已归还的共享组件状态。
        /// </summary>
        private void DeactivateActiveStrategy()
        {
            if (_activeMovementStrategy == null) return;

            // UnitMover 只触发当前策略已注入的停用回调，不接触任何具体模块。
            _lifecycleContainer.InvokeDeactivated();
            _activeMovementStrategy = null;
        }

        /// <summary>
        /// 验证运行时动态选择的策略类型可安全创建并加入当前策略缓存。
        /// </summary>
        /// <param name="strategyType">调用方请求切换的策略类型。</param>
        /// <returns>类型继承移动策略且满足实例化条件时返回 true。</returns>
        private bool IsValidMovementStrategyType(Type strategyType)
        {
            if (strategyType != null
                && typeof(UnitMovementStrategy).IsAssignableFrom(strategyType)
                && !strategyType.IsAbstract
                && !strategyType.ContainsGenericParameters
                && strategyType.GetConstructor(Type.EmptyTypes) != null)
                return true;

            // 动态 Type API 的错误只在调用方显式选择无效策略时报告，不进入固定步路径。
            Debug.LogError("UnitMover 只能切换到具有公开无参构造函数的具体 UnitMovementStrategy 类型。", this);
            return false;
        }

        /// <summary>
        /// 依次释放当前策略和全部缓存策略，并恢复由刚体适配器接管前的设置。
        /// </summary>
        private void DisposeRuntime()
        {
            // 当前策略先通过容器归还共享状态并释放模块，再逐个释放已经切走的缓存策略。
            UnitMovementStrategy activeStrategy = _activeMovementStrategy;
            if (activeStrategy != null)
            {
                _lifecycleContainer.InvokeDeactivated();
                _lifecycleContainer.InvokeDisposed();
                _lifecycleContainer.Clear();
                _activeMovementStrategy = null;
            }

            foreach (KeyValuePair<Type, UnitMovementStrategy> pair in _movementStrategies)
            {
                if (ReferenceEquals(pair.Value, activeStrategy)) continue;

                // 单容器只保留当前策略回调；释放缓存实例时临时由该策略重新注入 Disposed 回调。
                _lifecycleContainer.Clear();
                pair.Value.BindLifecycle(_lifecycleContainer);
                _lifecycleContainer.InvokeDisposed();
            }

            _lifecycleContainer.Clear();
            _movementStrategies.Clear();
            _body?.RestoreInitialSettings();
            _body = null;
            _physicsQuery = null;
            _resolvedDataProvider = null;
        }

        /// <summary>
        /// 在编辑或运行时请求初始策略同步其浮动胶囊的有效 Authoring 形状。
        /// </summary>
        public void SynchronizeColliderShape()
        {
            if (_movementCollider == null) return;

            // 不向 UnitMover 泄漏具体模块类型，胶囊形状如何同步由策略注入的 Authoring 回调决定。
            UnitMovementStrategy strategy = _activeMovementStrategy ?? _movementStrategy;
            if (strategy == null) return;
            if (!Application.isPlaying) PrepareAuthoringLifecycle(strategy);
            else strategy.SetAuthoringDependencies(_movementCollider, transform);
            _lifecycleContainer.InvokeAuthoringValidate();
        }

        /// <summary>
        /// 在编辑模式替换初始策略前触发旧策略注入的 Authoring 状态归还回调。
        /// </summary>
        public void RestoreInitialStrategyAuthoring()
        {
            if (Application.isPlaying || _movementStrategy == null) return;

            // Inspector 替换 SerializeReference 前没有运行时停用阶段，必须显式触发旧策略的归还回调。
            PrepareAuthoringLifecycle(_movementStrategy);
            _lifecycleContainer.InvokeAuthoringRestore();
            _lifecycleContainer.Clear();
        }

        /// <summary>
        /// 在编辑模式临时清空容器并让指定策略注入 Authoring 与 Gizmo 回调。
        /// </summary>
        /// <param name="strategy">需要为编辑器阶段绑定的初始策略。</param>
        private void PrepareAuthoringLifecycle(UnitMovementStrategy strategy)
        {
            if (strategy == null) return;

            // 编辑模式没有活动策略缓存，始终以初始策略重建唯一容器的当前回调集合。
            _lifecycleContainer.Clear();
            strategy.SetAuthoringDependencies(_movementCollider, transform);
            strategy.BindLifecycle(_lifecycleContainer);
        }

        /// <summary>
        /// 验证组装刚体适配器和默认策略至少需要的 Unity 组件引用。
        /// </summary>
        /// <returns>刚体和胶囊均有效时返回 true。</returns>
        private bool HasRequiredDependencies()
        {
            if (_rigidbody != null && _movementCollider != null) return true;
            if (_reportedMissingDependencies) return false;

            Debug.LogError("UnitMover 需要 Rigidbody 与 CapsuleCollider 才能创建移动策略运行时。", this);
            _reportedMissingDependencies = true;
            return false;
        }

        /// <summary>
        /// 解析 Inspector 指定的 Provider 或自动选择同对象唯一 IDataProvider，不读取其 Blackboard。
        /// </summary>
        /// <returns>已解析的同对象 Provider；不存在或存在歧义时返回 null。</returns>
        private IDataProvider ResolveDataProvider()
        {
            if (_dataProvider != null)
            {
                if (_dataProvider.gameObject == gameObject && _dataProvider is IDataProvider provider)
                    return provider;
                return null;
            }

            // 自动解析只接受唯一候选，多个 Provider 必须由 Inspector 明确指定以避免组件顺序决定行为。
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            IDataProvider candidate = null;
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is not IDataProvider provider) continue;
                if (candidate == null)
                {
                    candidate = provider;
                    continue;
                }

                if (!_reportedAmbiguousDataProvider)
                    Debug.LogError("UnitMover 在同一对象上检测到多个 IDataProvider；请在 Inspector 的 Data Provider 引用中明确指定一个。", this);
                _reportedAmbiguousDataProvider = true;
                return null;
            }

            _reportedAmbiguousDataProvider = false;
            return candidate;
        }

        #endregion
    }
}
