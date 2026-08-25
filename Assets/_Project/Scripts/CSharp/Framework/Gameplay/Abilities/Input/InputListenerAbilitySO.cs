using UnityEngine;
using UnityEngine.InputSystem;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 配置并执行 PlayerInput 到单位 Blackboard 的输入监听能力。
    /// </summary>
    [CreateAssetMenu(fileName = "InputListenerAbility", menuName = "Framework/Gameplay/Abilities/Input Listener")]
    public sealed class InputListenerAbilitySO : AbilityDefinitionSO
    {
        // 输入动作资产。
        [Header("Input System")]
        [Tooltip("用于读取移动、跳跃和冲刺动作的 Input Action Asset")]
        [SerializeField] private InputActionAsset _actions;
        // 默认启用的 Action Map。
        [Tooltip("PlayerInput 默认启用的 Action Map 名称")]
        [SerializeField] private string _defaultActionMap = "Player";
        // 输入动作名称。
        [Tooltip("平面移动动作名称，通常为 Move")]
        [SerializeField] private string _moveAction = "Move";
        [Tooltip("跳跃动作名称，通常为 Jump")]
        [SerializeField] private string _jumpAction = "Jump";
        [Tooltip("冲刺动作名称，通常为 Sprint")]
        [SerializeField] private string _sprintAction = "Sprint";

        /// <summary>根据配置创建输入监听能力运行时。</summary>
        /// <param name="context">能力所属单位上下文。</param>
        /// <returns>单位独占输入监听运行时。</returns>
        public override AbilityRuntime CreateRuntime(AbilityContext context)
        {
            return new InputListenerAbilityRuntime(
                _actions,
                _defaultActionMap,
                _moveAction,
                _jumpAction,
                _sprintAction);
        }
    }

    /// <summary>
    /// 在普通帧读取 PlayerInput 并写入单位独占 InputBlackboard。
    /// </summary>
    public sealed class InputListenerAbilityRuntime : AbilityRuntime
    {
        // 输入动作资产和动作名称配置。
        private readonly InputActionAsset _actions;
        private readonly string _defaultActionMap;
        private readonly string _moveActionName;
        private readonly string _jumpActionName;
        private readonly string _sprintActionName;
        // 单位独占输入黑板。
        private InputBlackboard _blackboard;
        // 运行时使用的 PlayerInput 组件。
        private PlayerInput _playerInput;
        // 是否由本能力创建了 PlayerInput。
        private bool _createdPlayerInput;
        // 缓存后的动作引用，避免普通帧按名称重复查找。
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;

        /// <summary>创建输入监听运行时。</summary>
        /// <param name="actions">Input Action Asset。</param>
        /// <param name="defaultActionMap">默认 Action Map 名称。</param>
        /// <param name="moveActionName">移动动作名称。</param>
        /// <param name="jumpActionName">跳跃动作名称。</param>
        /// <param name="sprintActionName">冲刺动作名称。</param>
        /// <param name="movementReferenceCamera">移动参考相机。</param>
        public InputListenerAbilityRuntime(
            InputActionAsset actions,
            string defaultActionMap,
            string moveActionName,
            string jumpActionName,
            string sprintActionName)
        {
            _actions = actions;
            _defaultActionMap = defaultActionMap;
            _moveActionName = moveActionName;
            _jumpActionName = jumpActionName;
            _sprintActionName = sprintActionName;
        }

        /// <summary>创建单位独占 Blackboard 并解析或补齐 PlayerInput。</summary>
        /// <param name="context">能力所属单位上下文。</param>
        public override void Initialize(AbilityContext context)
        {
            base.Initialize(context);
            if (context == null || context.Owner == null) return;

            _blackboard = new InputBlackboard();
            context.SetBlackboard(_blackboard);
            context.RegisterService(_blackboard);

            _playerInput = context.Owner.GetComponent<PlayerInput>();
            if (_playerInput == null && _actions != null)
            {
                _playerInput = context.Owner.AddComponent<PlayerInput>();
                _createdPlayerInput = true;
            }

            if (_playerInput == null) return;
            if (_actions != null) _playerInput.actions = _actions;
            if (!string.IsNullOrEmpty(_defaultActionMap)) _playerInput.defaultActionMap = _defaultActionMap;
            _moveAction = FindAction(_moveActionName);
            _jumpAction = FindAction(_jumpActionName);
            _sprintAction = FindAction(_sprintActionName);
        }

        /// <summary>启用 PlayerInput 和动作读取。</summary>
        public override void OnEnable()
        {
            if (_playerInput != null) _playerInput.enabled = true;
        }

        /// <summary>开始输入能力并启用配置的 Action Map。</summary>
        public override void Start()
        {
            if (_playerInput == null) return;
            if (!string.IsNullOrEmpty(_defaultActionMap)) _playerInput.SwitchCurrentActionMap(_defaultActionMap);
            _playerInput.ActivateInput();
        }

        /// <summary>读取输入动作并写入 Blackboard。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public override void Update(float deltaTime)
        {
            if (_blackboard == null) return;

            _blackboard.Move.Value = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            _blackboard.Jump.SetHeld(_jumpAction != null && _jumpAction.IsPressed());
            _blackboard.Sprint.SetHeld(_sprintAction != null && _sprintAction.IsPressed());
        }

        /// <summary>禁用 PlayerInput。</summary>
        public override void OnDisable()
        {
            if (_playerInput != null) _playerInput.DeactivateInput();
        }

        /// <summary>销毁本能力创建的 PlayerInput 并释放输入黑板。</summary>
        public override void Dispose()
        {
            if (_createdPlayerInput && _playerInput != null)
                Object.Destroy(_playerInput);
            if (Context != null && ReferenceEquals(Context.GetService<InputBlackboard>(), _blackboard))
                Context.RegisterService<InputBlackboard>(null);
            _playerInput = null;
            _blackboard = null;
            _moveAction = null;
            _jumpAction = null;
            _sprintAction = null;
            base.Dispose();
        }

        /// <summary>按动作名称读取 PlayerInput 中的 InputAction。</summary>
        /// <param name="actionName">动作名称。</param>
        /// <returns>找到的动作；不存在时返回 null。</returns>
        private InputAction FindAction(string actionName)
        {
            if (_playerInput == null || _playerInput.actions == null || string.IsNullOrEmpty(actionName)) return null;
            return _playerInput.actions.FindAction(actionName, false);
        }
    }
}
