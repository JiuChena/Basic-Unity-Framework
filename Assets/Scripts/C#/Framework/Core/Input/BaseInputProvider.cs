using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CoreFramework
{
    /// <summary>
    /// IInputProvider 的抽象基类，提供 Input System / Legacy 双轨输入绑定与读取能力。
    /// 子类只需实现 Tick() 决定如何将读取到的输入写入 Blackboard。
    /// </summary>
    public abstract class BaseInputProvider : MonoBehaviour, IInputProvider
    {
        [Header("灵敏度")]
        [Tooltip("鼠标或视角输入的灵敏度倍率")]
        public float lookSensitivity = 2f;

        [Header("输入管线")]
        [Tooltip("可选的逻辑动作到 Input System Action 映射。未配置时继续使用当前字段的兼容读取链路")]
        [SerializeField] private InputBindingProfile _bindingProfile;

        /// <summary>
        /// 当前 Provider 可选使用的逻辑动作绑定配置。
        /// </summary>
        protected InputBindingProfile BindingProfile => _bindingProfile;

#if ENABLE_INPUT_SYSTEM
        [Header("Input Action 绑定")]
        [Tooltip("移动输入对应的 Action 名称，留空使用 Legacy 回退")]
        [SerializeField] private string _moveActionName = "Move";

        [Tooltip("视角输入对应的 Action 名称")]
        [SerializeField] private string _lookActionName = "Look";

        [Tooltip("跳跃输入对应的 Action 名称")]
        [SerializeField] private string _jumpActionName = "Jump";

        [Tooltip("冲刺输入对应的 Action 名称")]
        [SerializeField] private string _sprintActionName = "Sprint";

        [Tooltip("下蹲输入对应的 Action 名称")]
        [SerializeField] private string _crouchActionName = "Crouch";

        [Tooltip("普攻输入对应的 Action 名称")]
        [SerializeField] private string _attackActionName = "Attack";

        [Tooltip("瞄准输入对应的 Action 名称")]
        [SerializeField] private string _aimActionName = "Aim";

        [Tooltip("装填输入对应的 Action 名称")]
        [SerializeField] private string _reloadActionName = "Reload";

        [Tooltip("交互输入对应的 Action 名称")]
        [SerializeField] private string _interactActionName = "Interact";

        private PlayerInput _playerInput;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _attackAction;
        private InputAction _aimAction;
        private InputAction _reloadAction;
        private InputAction _interactAction;
        private InputAction _crouchAction;
#endif

        [Header("Legacy 键位绑定")]
        [Tooltip("前进")]
        [SerializeField] private KeyCode _moveForwardKey = KeyCode.W;

        [Tooltip("后退")]
        [SerializeField] private KeyCode _moveBackKey = KeyCode.S;

        [Tooltip("左移")]
        [SerializeField] private KeyCode _moveLeftKey = KeyCode.A;

        [Tooltip("右移")]
        [SerializeField] private KeyCode _moveRightKey = KeyCode.D;

        [Tooltip("跳跃")]
        [SerializeField] private KeyCode _jumpKey = KeyCode.Space;

        [Tooltip("冲刺")]
        [SerializeField] private KeyCode _sprintKey = KeyCode.LeftShift;

        [Tooltip("下蹲")]
        [SerializeField] private KeyCode _crouchKey = KeyCode.C;

        [Tooltip("普攻")]
        [SerializeField] private KeyCode _attackKey = KeyCode.Mouse0;

        [Tooltip("瞄准")]
        [SerializeField] private KeyCode _aimKey = KeyCode.Mouse1;

        [Tooltip("装填")]
        [SerializeField] private KeyCode _reloadKey = KeyCode.R;

        [Tooltip("交互")]
        [SerializeField] private KeyCode _interactKey = KeyCode.F;

        public abstract Blackboard Board { get; }
        public abstract void Tick();

        protected virtual void Awake()
        {
            CacheInputActions();
        }

        protected virtual void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            if (_playerInput != null && !_playerInput.inputIsActive)
                _playerInput.ActivateInput();
#endif
        }

        protected virtual void Update()
        {
            Tick();
        }

        /// <summary>
        /// 创建本 Provider 使用的动作 Reader。旧场景没有配置 Profile 时保持现有读取行为。
        /// </summary>
        protected IInputReader CreateInputReader()
        {
#if ENABLE_INPUT_SYSTEM
            if (_bindingProfile != null && _bindingProfile.HasInputSystemBindings)
            {
                PlayerInput playerInput = GetComponent<PlayerInput>();
                if (playerInput != null)
                    return new InputSystemReader(playerInput, _bindingProfile);
            }
#endif
            return new DelegateInputReader(ReadStandardInputSnapshot);
        }

        /// <summary>
        /// 将既有的按键读取 API 汇总为标准动作快照，作为迁移期间的兼容 Reader 数据源。
        /// </summary>
        protected StandardInputSnapshot ReadStandardInputSnapshot()
        {
            StandardInputSnapshot snapshot = new StandardInputSnapshot
            {
                move = ReadMoveInput(),
                look = Mathf.Approximately(lookSensitivity, 0f)
                    ? Vector2.zero
                    : ReadLookInput() / lookSensitivity,
                scroll = ReadScrollDelta(),
                jumpPressed = ReadJumpPressed(),
                jumpHeld = ReadJumpHeld(),
                jumpReleased = ReadJumpReleased(),
                sprintHeld = ReadSprintHeld(),
                crouchPressed = ReadCrouchPressed(),
                crouchHeld = ReadCrouchHeld(),
                crouchReleased = ReadCrouchReleased(),
                attackPressed = ReadAttackPressed(),
                attackHeld = ReadAttackHeld(),
                attackReleased = ReadAttackReleased(),
                aimHeld = ReadAimHeld(),
                reloadPressed = ReadReloadPressed(),
                interactPressed = ReadInteractPressed()
            };

#if ENABLE_LEGACY_INPUT_MANAGER
            snapshot.switch1Pressed = Input.GetKeyDown(KeyCode.Alpha1);
            snapshot.switch2Pressed = Input.GetKeyDown(KeyCode.Alpha2);
            snapshot.switch3Pressed = Input.GetKeyDown(KeyCode.Alpha3);
            snapshot.switch4Pressed = Input.GetKeyDown(KeyCode.Alpha4);
#endif
            return snapshot;
        }

        // ── Input Action 缓存 ──

        private void CacheInputActions()
        {
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
            InputActionAsset actions = _playerInput != null ? _playerInput.actions : null;
            if (actions == null) return;

            _moveAction     = FindActionSafe(actions, _moveActionName);
            _lookAction     = FindActionSafe(actions, _lookActionName);
            _jumpAction     = FindActionSafe(actions, _jumpActionName);
            _sprintAction   = FindActionSafe(actions, _sprintActionName);
            _attackAction   = FindActionSafe(actions, _attackActionName);
            _aimAction      = FindActionSafe(actions, _aimActionName);
            _reloadAction   = FindActionSafe(actions, _reloadActionName);
            _interactAction = FindActionSafe(actions, _interactActionName);
            _crouchAction   = FindActionSafe(actions, _crouchActionName);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static InputAction FindActionSafe(InputActionAsset actions, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return actions.FindAction(name, false);
        }
#endif

        // ── 读取方法（子类在 Tick() 中调用）──

        protected Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (_moveAction != null) return _moveAction.ReadValue<Vector2>();
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            Vector2 move = Vector2.zero;
            if (Input.GetKey(_moveForwardKey)) move.y += 1f;
            if (Input.GetKey(_moveBackKey))    move.y -= 1f;
            if (Input.GetKey(_moveRightKey))   move.x += 1f;
            if (Input.GetKey(_moveLeftKey))    move.x -= 1f;
            return move;
#else
            return Vector2.zero;
#endif
        }

        protected Vector2 ReadLookInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (_lookAction != null) return _lookAction.ReadValue<Vector2>() * lookSensitivity;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * lookSensitivity;
#else
            return Vector2.zero;
#endif
        }

        protected bool ReadJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (_jumpAction != null) return _jumpAction.WasPressedThisFrame();
#endif
            return LegacyJumpPressed();
        }

        protected bool ReadJumpHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (_jumpAction != null) return _jumpAction.IsPressed();
#endif
            return LegacyJumpHeld();
        }

        protected bool ReadJumpReleased()
        {
#if ENABLE_INPUT_SYSTEM
            if (_jumpAction != null) return _jumpAction.WasReleasedThisFrame();
#endif
            return LegacyJumpReleased();
        }

        protected bool ReadSprintHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (_sprintAction != null) return _sprintAction.IsPressed();
#endif
            return LegacySprintHeld();
        }

        protected bool ReadAttackHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (_attackAction != null) return _attackAction.IsPressed();
#endif
            return LegacyAttackHeld();
        }

        protected bool ReadAimHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (_aimAction != null) return _aimAction.IsPressed();
#endif
            return LegacyAimHeld();
        }

        protected bool ReadAttackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (_attackAction != null) return _attackAction.WasPressedThisFrame();
#endif
            return LegacyAttackPressed();
        }

        protected bool ReadAttackReleased()
        {
#if ENABLE_INPUT_SYSTEM
            if (_attackAction != null) return _attackAction.WasReleasedThisFrame();
#endif
            return LegacyAttackReleased();
        }

        protected bool ReadReloadPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (_reloadAction != null) return _reloadAction.WasPressedThisFrame();
#endif
            return LegacyReloadPressed();
        }

        protected bool ReadInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (_interactAction != null) return _interactAction.WasPressedThisFrame();
#endif
            return LegacyInteractPressed();
        }

        protected bool ReadCrouchPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (_crouchAction != null) return _crouchAction.WasPressedThisFrame();
#endif
            return LegacyCrouchPressed();
        }

        protected bool ReadCrouchHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (_crouchAction != null) return _crouchAction.IsPressed();
#endif
            return LegacyCrouchHeld();
        }

        protected bool ReadCrouchReleased()
        {
#if ENABLE_INPUT_SYSTEM
            if (_crouchAction != null) return _crouchAction.WasReleasedThisFrame();
#endif
            return LegacyCrouchReleased();
        }

        protected static int ReadScrollDelta()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Mathf.RoundToInt(Input.mouseScrollDelta.y);
#else
            return 0;
#endif
        }

        // ── Legacy 回退 ──

#if ENABLE_LEGACY_INPUT_MANAGER
        private bool LegacyJumpPressed()    => Input.GetKeyDown(_jumpKey);
        private bool LegacyJumpHeld()       => Input.GetKey(_jumpKey);
        private bool LegacyJumpReleased()   => Input.GetKeyUp(_jumpKey);
        private bool LegacySprintHeld()     => Input.GetKey(_sprintKey);
        private bool LegacyAttackHeld()     => Input.GetKey(_attackKey);
        private bool LegacyAimHeld()        => Input.GetKey(_aimKey);
        private bool LegacyAttackPressed()  => Input.GetKeyDown(_attackKey);
        private bool LegacyAttackReleased() => Input.GetKeyUp(_attackKey);
        private bool LegacyReloadPressed()  => Input.GetKeyDown(_reloadKey);
        private bool LegacyInteractPressed() => Input.GetKeyDown(_interactKey);
        private bool LegacyCrouchPressed()  => Input.GetKeyDown(_crouchKey);
        private bool LegacyCrouchHeld()     => Input.GetKey(_crouchKey);
        private bool LegacyCrouchReleased() => Input.GetKeyUp(_crouchKey);
#else
        private bool LegacyJumpPressed()    => false;
        private bool LegacyJumpHeld()       => false;
        private bool LegacyJumpReleased()   => false;
        private bool LegacySprintHeld()     => false;
        private bool LegacyAttackHeld()     => false;
        private bool LegacyAimHeld()        => false;
        private bool LegacyAttackPressed()  => false;
        private bool LegacyAttackReleased() => false;
        private bool LegacyReloadPressed()  => false;
        private bool LegacyInteractPressed() => false;
        private bool LegacyCrouchPressed()  => false;
        private bool LegacyCrouchHeld()     => false;
        private bool LegacyCrouchReleased() => false;
#endif
    }
}
