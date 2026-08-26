using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>统一装配能力定义并转发 Unity 生命周期。</summary>
    [DisallowMultipleComponent]
    public sealed class AbilityComponent : MonoBehaviour
    {
        // 按 Inspector 顺序装配的能力定义资产。
        [Header("能力")]
        [Tooltip("按顺序执行生命周期的能力定义资产；运行时每个单位创建独立实例")]
        [SerializeField] private List<AbilityDefinitionSO> _abilities = new List<AbilityDefinitionSO>();
        // 当前启用周期的能力上下文。
        private AbilityContext _context;
        // 当前启用周期的能力运行时列表。
        private readonly List<AbilityRuntime> _runtimes = new List<AbilityRuntime>();
        // 防止重复执行销毁流程。
        private bool _disposed;

        /// <summary>获取当前能力上下文；未装配时返回 null。</summary>
        public AbilityContext Context => _context;

        /// <summary>创建最小单位上下文和独占能力运行时实例。</summary>
        private void Awake()
        {
            _context = new AbilityContext(gameObject);

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

        /// <summary>转发固定帧能力生命周期。</summary>
        private void FixedUpdate()
        {
            if (_context == null) return;
            for (int index = 0; index < _runtimes.Count; index++) _runtimes[index].FixedUpdate(Time.fixedDeltaTime);
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

        /// <summary>释放能力实例和最小运行时上下文。</summary>
        private void OnDestroy()
        {
            if (_disposed) return;
            _disposed = true;
            for (int index = _runtimes.Count - 1; index >= 0; index--) _runtimes[index].Dispose();
            _runtimes.Clear();
            _context = null;
        }
    }
}
