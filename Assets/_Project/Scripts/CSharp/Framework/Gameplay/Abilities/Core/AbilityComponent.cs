using System.Collections.Generic;
using Framework.ExpandComponent.DataProvider;
using Framework.ExpandComponent.UnitMover;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>统一装配能力定义并转发 Unity 生命周期。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class AbilityComponent : MonoBehaviour
    {
        // 按 Inspector 顺序装配的能力定义资产。
        [Header("能力")]
        [Tooltip("按顺序执行生命周期的能力定义资产；运行时每个单位创建独立实例")]
        [SerializeField] private List<AbilityDefinitionSO> _abilities = new List<AbilityDefinitionSO>();
        // 是否在能力运行时冻结刚体旋转并关闭 Unity 自动重力。
        [Header("物理")]
        [Tooltip("是否由能力运行时接管刚体旋转和重力设置")]
        [SerializeField] private bool _takeoverRigidbody = true;
        // 缓存的 Rigidbody 组件。
        private Rigidbody _rigidbody;
        // 缓存的主 CapsuleCollider 组件。
        private CapsuleCollider _movementCollider;
        // 当前启用周期的刚体适配器。
        private IUnitBody _body;
        // 当前启用周期的能力上下文。
        private AbilityContext _context;
        // 当前启用周期的能力运行时列表。
        private readonly List<AbilityRuntime> _runtimes = new List<AbilityRuntime>();
        // 防止重复执行销毁流程。
        private bool _disposed;

        /// <summary>获取当前能力上下文；未装配时返回 null。</summary>
        public AbilityContext Context => _context;

        /// <summary>
        /// 解析 Unity 依赖并创建单位独占能力运行时实例。
        /// </summary>
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _movementCollider = GetComponent<CapsuleCollider>();
            _body = new RigidbodyUnitBody(_rigidbody, true, _takeoverRigidbody);
            _context = new AbilityContext(gameObject, _rigidbody, _movementCollider, _body, new UnityPhysicsQuery(), null);

            // 所有能力在启用前一次性创建，避免固定帧热路径分配。
            for (int index = 0; index < _abilities.Count; index++)
            {
                AbilityDefinitionSO definition = _abilities[index];
                if (definition == null) continue;
                AbilityRuntime runtime = definition.CreateRuntime(_context);
                if (runtime == null) continue;
                runtime.Initialize(_context);
                _runtimes.Add(runtime);
            }
        }

        /// <summary>转发能力启用生命周期。</summary>
        private void OnEnable()
        {
            if (_context == null) return;
            for (int index = 0; index < _runtimes.Count; index++) _runtimes[index].OnEnable();
        }

        /// <summary>转发能力启动生命周期。</summary>
        private void Start()
        {
            if (_context == null) return;
            for (int index = 0; index < _runtimes.Count; index++) _runtimes[index].Start();
        }

        /// <summary>转发普通帧能力生命周期。</summary>
        private void Update()
        {
            if (_context == null) return;
            for (int index = 0; index < _runtimes.Count; index++) _runtimes[index].Update(Time.deltaTime);
        }

        /// <summary>转发固定帧能力生命周期并统一提交最终速度。</summary>
        private void FixedUpdate()
        {
            if (_context == null) return;
            _context.Velocity = _body != null && _body.IsValid ? _body.Velocity : Vector3.zero;
            for (int index = 0; index < _runtimes.Count; index++) _runtimes[index].FixedUpdate(Time.fixedDeltaTime);
            _context.CommitVelocity();
        }

        /// <summary>转发延迟帧能力生命周期。</summary>
        private void LateUpdate()
        {
            if (_context == null) return;
            for (int index = 0; index < _runtimes.Count; index++) _runtimes[index].LateUpdate(Time.deltaTime);
        }

        /// <summary>转发能力禁用生命周期。</summary>
        private void OnDisable()
        {
            for (int index = 0; index < _runtimes.Count; index++) _runtimes[index].OnDisable();
        }

        /// <summary>释放能力实例、组件适配器和运行时服务。</summary>
        private void OnDestroy()
        {
            if (_disposed) return;
            _disposed = true;
            for (int index = _runtimes.Count - 1; index >= 0; index--) _runtimes[index].Dispose();
            _runtimes.Clear();
            _context?.ClearServices();
            _body?.RestoreInitialSettings();
            _context = null;
            _body = null;
        }
    }
}
