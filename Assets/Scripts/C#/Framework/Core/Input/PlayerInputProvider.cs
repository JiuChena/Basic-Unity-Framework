using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CoreFramework
{
    /// <summary>
    /// 将本地玩家设备状态写入类型化 Blackboard 数据槽的输入提供者。
    /// </summary>
    public class PlayerInputProvider : MonoBehaviour, IInputProvider
    {
        // 当前输入源拥有的共享数据黑板。
        private readonly Blackboard _board = new Blackboard();

        [Header("灵敏度")]
        [Tooltip("鼠标或视角输入的灵敏度倍率")]
        public float lookSensitivity = 2f;

#if ENABLE_INPUT_SYSTEM
        // 新输入系统的组件与已缓存动作引用。
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

        /// <summary>
        /// 当前 Provider 独占拥有的输入数据黑板。
        /// </summary>
        public Blackboard Board => _board;

        /// <summary>
        /// 缓存可选的新输入系统 Action。
        /// </summary>
        private void Awake()
        {
            CacheInputActions();
        }

        /// <summary>
        /// 激活可选的新输入系统组件。
        /// </summary>
        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            if (_playerInput != null && !_playerInput.inputIsActive)
                _playerInput.ActivateInput();
#endif
        }

        /// <summary>
        /// 在渲染帧采集本地设备状态。
        /// </summary>
        private void Update()
        {
            Tick();
        }

        /// <summary>
        /// 将当前设备状态写入移动、战斗和交互数据槽。
        /// </summary>
        public void Tick()
        {
            LocomotionInputData locomotion = Board.GetOrCreate<LocomotionInputData>();
            CombatInputData combat = Board.GetOrCreate<CombatInputData>();
            InteractionInputData interaction = Board.GetOrCreate<InteractionInputData>();

            Vector2 move = ReadMoveInput();
            locomotion.Move = move.sqrMagnitude > 1f ? move.normalized : move;
            locomotion.Look = ReadLookInput();
            locomotion.IsSprinting = ReadSprintHeld();
            locomotion.Jump.SetState(ReadJumpPressed(), ReadJumpHeld(), ReadJumpReleased());
            locomotion.Crouch.SetState(ReadCrouchPressed(), ReadCrouchHeld(), ReadCrouchReleased());

            combat.Attack.SetState(ReadAttackPressed(), ReadAttackHeld(), ReadAttackReleased());
            combat.Reload.SetState(ReadReloadPressed(), false, false);
            combat.IsAiming = ReadAimHeld();

            interaction.Interact.SetState(ReadInteractPressed(), false, false);
            interaction.ScrollDelta = ReadScrollDelta();
            ReadSwitchInput(interaction);
        }

        /// <summary>
        /// 缓存新输入系统中存在的动作，未配置动作时自动使用旧输入回退。
        /// </summary>
        private void CacheInputActions()
        {
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
            InputActionAsset actions = _playerInput != null ? _playerInput.actions : null;
            if (actions == null) return;

            _moveAction = actions.FindAction("Move", false);
            _lookAction = actions.FindAction("Look", false);
            _jumpAction = actions.FindAction("Jump", false);
            _sprintAction = actions.FindAction("Sprint", false);
            _attackAction = actions.FindAction("Attack", false);
            _aimAction = actions.FindAction("Aim", false);
            _reloadAction = actions.FindAction("Reload", false);
            _interactAction = actions.FindAction("Interact", false);
            _crouchAction = actions.FindAction("Crouch", false);
#endif
        }

        /// <summary>
        /// 读取平面移动输入。
        /// </summary>
        /// <returns>未归一化的二维移动输入。</returns>
        private Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (_moveAction != null) return _moveAction.ReadValue<Vector2>();
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            Vector2 move = Vector2.zero;
            if (Input.GetKey(KeyCode.W)) move.y += 1f;
            if (Input.GetKey(KeyCode.S)) move.y -= 1f;
            if (Input.GetKey(KeyCode.D)) move.x += 1f;
            if (Input.GetKey(KeyCode.A)) move.x -= 1f;
            return move;
#else
            return Vector2.zero;
#endif
        }

        /// <summary>
        /// 读取本帧视角输入。
        /// </summary>
        /// <returns>应用灵敏度后的视角增量。</returns>
        private Vector2 ReadLookInput()
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

        /// <summary>
        /// 读取跳跃按下边沿。
        /// </summary>
        private bool ReadJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (_jumpAction != null) return _jumpAction.WasPressedThisFrame();
#endif
            return LegacyJumpPressed();
        }

        /// <summary>
        /// 读取跳跃持续状态。
        /// </summary>
        private bool ReadJumpHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (_jumpAction != null) return _jumpAction.IsPressed();
#endif
            return LegacyJumpHeld();
        }

        /// <summary>
        /// 读取跳跃抬起边沿。
        /// </summary>
        private bool ReadJumpReleased()
        {
#if ENABLE_INPUT_SYSTEM
            if (_jumpAction != null) return _jumpAction.WasReleasedThisFrame();
#endif
            return LegacyJumpReleased();
        }

        /// <summary>
        /// 读取冲刺持续状态。
        /// </summary>
        private bool ReadSprintHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (_sprintAction != null) return _sprintAction.IsPressed();
#endif
            return LegacySprintHeld();
        }

        /// <summary>
        /// 读取普攻持续状态。
        /// </summary>
        private bool ReadAttackHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (_attackAction != null) return _attackAction.IsPressed();
#endif
            return LegacyAttackHeld();
        }

        /// <summary>
        /// 读取瞄准持续状态。
        /// </summary>
        private bool ReadAimHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (_aimAction != null) return _aimAction.IsPressed();
#endif
            return LegacyAimHeld();
        }

        /// <summary>
        /// 读取普攻按下边沿。
        /// </summary>
        private bool ReadAttackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (_attackAction != null) return _attackAction.WasPressedThisFrame();
#endif
            return LegacyAttackPressed();
        }

        /// <summary>
        /// 读取普攻抬起边沿。
        /// </summary>
        private bool ReadAttackReleased()
        {
#if ENABLE_INPUT_SYSTEM
            if (_attackAction != null) return _attackAction.WasReleasedThisFrame();
#endif
            return LegacyAttackReleased();
        }

        /// <summary>
        /// 读取装填按下边沿。
        /// </summary>
        private bool ReadReloadPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (_reloadAction != null) return _reloadAction.WasPressedThisFrame();
#endif
            return LegacyReloadPressed();
        }

        /// <summary>
        /// 读取交互按下边沿。
        /// </summary>
        private bool ReadInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (_interactAction != null) return _interactAction.WasPressedThisFrame();
#endif
            return LegacyInteractPressed();
        }

        /// <summary>
        /// 读取下蹲按下边沿。
        /// </summary>
        private bool ReadCrouchPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (_crouchAction != null) return _crouchAction.WasPressedThisFrame();
#endif
            return LegacyCrouchPressed();
        }

        /// <summary>
        /// 读取下蹲持续状态。
        /// </summary>
        private bool ReadCrouchHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (_crouchAction != null) return _crouchAction.IsPressed();
#endif
            return LegacyCrouchHeld();
        }

        /// <summary>
        /// 读取下蹲抬起边沿。
        /// </summary>
        private bool ReadCrouchReleased()
        {
#if ENABLE_INPUT_SYSTEM
            if (_crouchAction != null) return _crouchAction.WasReleasedThisFrame();
#endif
            return LegacyCrouchReleased();
        }

        /// <summary>
        /// 读取角色切换请求。
        /// </summary>
        /// <param name="interaction">要写入的交互输入数据槽。</param>
        private static void ReadSwitchInput(InteractionInputData interaction)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1)) interaction.RequestSwitch(1);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) interaction.RequestSwitch(2);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) interaction.RequestSwitch(3);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) interaction.RequestSwitch(4);
#endif
        }

        /// <summary>
        /// 读取鼠标滚轮增量。
        /// </summary>
        /// <returns>本帧滚轮离散增量。</returns>
        private static int ReadScrollDelta()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Mathf.RoundToInt(Input.mouseScrollDelta.y);
#else
            return 0;
#endif
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private static bool LegacyJumpPressed() => Input.GetKeyDown(KeyCode.Space);
        private static bool LegacyJumpHeld() => Input.GetKey(KeyCode.Space);
        private static bool LegacyJumpReleased() => Input.GetKeyUp(KeyCode.Space);
        private static bool LegacySprintHeld() => Input.GetKey(KeyCode.LeftShift);
        private static bool LegacyAttackHeld() => Input.GetMouseButton(0);
        private static bool LegacyAimHeld() => Input.GetMouseButton(1);
        private static bool LegacyAttackPressed() => Input.GetMouseButtonDown(0);
        private static bool LegacyAttackReleased() => Input.GetMouseButtonUp(0);
        private static bool LegacyReloadPressed() => Input.GetKeyDown(KeyCode.R);
        private static bool LegacyInteractPressed() => Input.GetKeyDown(KeyCode.F);
        private static bool LegacyCrouchPressed() => Input.GetKeyDown(KeyCode.C);
        private static bool LegacyCrouchHeld() => Input.GetKey(KeyCode.C);
        private static bool LegacyCrouchReleased() => Input.GetKeyUp(KeyCode.C);
#else
        private static bool LegacyJumpPressed() => false;
        private static bool LegacyJumpHeld() => false;
        private static bool LegacyJumpReleased() => false;
        private static bool LegacySprintHeld() => false;
        private static bool LegacyAttackHeld() => false;
        private static bool LegacyAimHeld() => false;
        private static bool LegacyAttackPressed() => false;
        private static bool LegacyAttackReleased() => false;
        private static bool LegacyReloadPressed() => false;
        private static bool LegacyInteractPressed() => false;
        private static bool LegacyCrouchPressed() => false;
        private static bool LegacyCrouchHeld() => false;
        private static bool LegacyCrouchReleased() => false;
#endif
    }
}
