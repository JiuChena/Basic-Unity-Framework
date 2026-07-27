using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Framework.Core
{
    /// <summary>
    /// 玩家数据提供器。优先使用 Unity Input System，Legacy Input Manager 作为回退。
    /// 直接读取设备原始数据并写入 Blackboard 属性，不经过中间管线。
    /// </summary>
    public sealed class PlayerDataProvider : DataProviderBase
    {
        [Header("灵敏度")]
        [Tooltip("鼠标或视角输入的灵敏度倍率，值越大视角转动越快")]
        [Min(0f)]
        [SerializeField] private float _lookSensitivity = 2f;

        [Header("Input System 动作名称")]
        [Tooltip("移动输入对应的 Input Action 名称")]
        [SerializeField] private string _moveAction = "Move";
        [Tooltip("视角输入对应的 Input Action 名称")]
        [SerializeField] private string _lookAction = "Look";
        [Tooltip("跳跃输入对应的 Input Action 名称")]
        [SerializeField] private string _jumpAction = "Jump";
        [Tooltip("冲刺输入对应的 Input Action 名称")]
        [SerializeField] private string _sprintAction = "Sprint";
        [Tooltip("下蹲输入对应的 Input Action 名称")]
        [SerializeField] private string _crouchAction = "Crouch";
        [Tooltip("普攻输入对应的 Input Action 名称")]
        [SerializeField] private string _attackAction = "Attack";
        [Tooltip("天赋技能输入对应的 Input Action 名称")]
        [SerializeField] private string _talentAction = "Talent";
        [Tooltip("爆发技能输入对应的 Input Action 名称")]
        [SerializeField] private string _burstAction = "Burst";
        [Tooltip("瞄准输入对应的 Input Action 名称")]
        [SerializeField] private string _aimAction = "Aim";
        [Tooltip("装填输入对应的 Input Action 名称")]
        [SerializeField] private string _reloadAction = "Reload";
        [Tooltip("交互输入对应的 Input Action 名称")]
        [SerializeField] private string _interactAction = "Interact";
        [Tooltip("滚轮输入对应的 Input Action 名称")]
        [SerializeField] private string _scrollAction = "Scroll";
        [Tooltip("切换到角色 1 对应的 Input Action 名称")]
        [SerializeField] private string _switch1Action = "Switch1";
        [Tooltip("切换到角色 2 对应的 Input Action 名称")]
        [SerializeField] private string _switch2Action = "Switch2";
        [Tooltip("切换到角色 3 对应的 Input Action 名称")]
        [SerializeField] private string _switch3Action = "Switch3";
        [Tooltip("切换到角色 4 对应的 Input Action 名称")]
        [SerializeField] private string _switch4Action = "Switch4";

#if ENABLE_INPUT_SYSTEM
        [Header("Input System 引用")]
        [Tooltip("PlayerInput 组件引用，Awake 时若为空则自动从当前 GameObject 获取")]
        [SerializeField] private PlayerInput _playerInput;
#endif

        [Header("Legacy 键位")]
        [Tooltip("前进键")]
        [SerializeField] private KeyCode _moveForwardKey = KeyCode.W;
        [Tooltip("后退键")]
        [SerializeField] private KeyCode _moveBackKey = KeyCode.S;
        [Tooltip("左移键")]
        [SerializeField] private KeyCode _moveLeftKey = KeyCode.A;
        [Tooltip("右移键")]
        [SerializeField] private KeyCode _moveRightKey = KeyCode.D;
        [Tooltip("跳跃键")]
        [SerializeField] private KeyCode _jumpKey = KeyCode.Space;
        [Tooltip("冲刺键")]
        [SerializeField] private KeyCode _sprintKey = KeyCode.LeftShift;
        [Tooltip("下蹲键")]
        [SerializeField] private KeyCode _crouchKey = KeyCode.C;
        [Tooltip("普攻键")]
        [SerializeField] private KeyCode _attackKey = KeyCode.Mouse0;
        [Tooltip("天赋技能键")]
        [SerializeField] private KeyCode _talentKey = KeyCode.Q;
        [Tooltip("爆发技能键")]
        [SerializeField] private KeyCode _burstKey = KeyCode.E;
        [Tooltip("瞄准键")]
        [SerializeField] private KeyCode _aimKey = KeyCode.Mouse1;
        [Tooltip("装填键")]
        [SerializeField] private KeyCode _reloadKey = KeyCode.R;
        [Tooltip("交互键")]
        [SerializeField] private KeyCode _interactKey = KeyCode.F;

        // ── 缓存的属性实例，Tick 中直写，零字典查找 ──
        private MoveAttribute _move;
        private LookAttribute _look;
        private SprintAttribute _sprint;
        private CrouchAttribute _crouch;
        private JumpAttribute _jump;
        private AttackAttribute _attack;
        private TalentAttribute _talent;
        private BurstAttribute _burst;
        private AimAttribute _aim;
        private ReloadAttribute _reload;
        private InteractAttribute _interact;
        private ScrollAttribute _scroll;
        private SwitchCharacterAttribute _switchCharacter;

        // ── 最近一次滚轮方向，用于 Input System 读取 ──
        private float _lastScrollY;

        #region Lifecycle

        /// <summary>
        /// 缓存 PlayerInput 引用并调用基类初始化。
        /// </summary>
        protected override void Awake()
        {
#if ENABLE_INPUT_SYSTEM
            // 未显式拖拽引用时自动从当前 GameObject 获取
            if (_playerInput == null) _playerInput = GetComponent<PlayerInput>();
#endif
            base.Awake();
        }

        /// <summary>
        /// 注册玩家所需全部 13 个属性到 Blackboard。
        /// </summary>
        protected override void RegisterAttributes(Blackboard board)
        {
            // 移动相关
            _move = Register(new MoveAttribute());
            _look = Register(new LookAttribute());
            _sprint = Register(new SprintAttribute());

            // 姿态相关
            _crouch = Register(new CrouchAttribute());
            _jump = Register(new JumpAttribute());

            // 战斗相关
            _attack = Register(new AttackAttribute());
            _talent = Register(new TalentAttribute());
            _burst = Register(new BurstAttribute());
            _aim = Register(new AimAttribute());

            // 交互相关
            _reload = Register(new ReloadAttribute());
            _interact = Register(new InteractAttribute());
            _scroll = Register(new ScrollAttribute());
            _switchCharacter = Register(new SwitchCharacterAttribute());
        }

        #endregion

        #region Tick

        /// <summary>
        /// 从设备读取原始输入并写入所有已注册属性。
        /// </summary>
        public override void Tick()
        {
            // ── 连续值类型属性 ──
            _move.Value = ReadMove();
            _look.Value = ReadLook() * _lookSensitivity;
            _sprint.Value = ReadHeld(_sprintAction, _sprintKey);
            _aim.Value = ReadHeld(_aimAction, _aimKey);
            _scroll.Value = ReadScroll();

            // ── 按钮类型属性：写入原始采样（按压 / 保持 / 抬起）──
            _crouch.SetState(
                ReadPressed(_crouchAction, _crouchKey),
                ReadHeld(_crouchAction, _crouchKey),
                ReadReleased(_crouchAction, _crouchKey));

            _jump.SetState(
                ReadPressed(_jumpAction, _jumpKey),
                ReadHeld(_jumpAction, _jumpKey),
                ReadReleased(_jumpAction, _jumpKey));

            _attack.SetState(
                ReadPressed(_attackAction, _attackKey),
                ReadHeld(_attackAction, _attackKey),
                ReadReleased(_attackAction, _attackKey));

            _talent.SetState(
                ReadPressed(_talentAction, _talentKey),
                ReadHeld(_talentAction, _talentKey),
                ReadReleased(_talentAction, _talentKey));

            _burst.SetState(
                ReadPressed(_burstAction, _burstKey),
                ReadHeld(_burstAction, _burstKey),
                ReadReleased(_burstAction, _burstKey));

            _reload.SetState(
                ReadPressed(_reloadAction, _reloadKey),
                ReadHeld(_reloadAction, _reloadKey),
                ReadReleased(_reloadAction, _reloadKey));

            _interact.SetState(
                ReadPressed(_interactAction, _interactKey),
                ReadHeld(_interactAction, _interactKey),
                ReadReleased(_interactAction, _interactKey));

            // ── 角色切换：按下对应键时发起请求 ──
            if (ReadPressed(_switch1Action, KeyCode.Alpha1)) _switchCharacter.Request(0);
            if (ReadPressed(_switch2Action, KeyCode.Alpha2)) _switchCharacter.Request(1);
            if (ReadPressed(_switch3Action, KeyCode.Alpha3)) _switchCharacter.Request(2);
            if (ReadPressed(_switch4Action, KeyCode.Alpha4)) _switchCharacter.Request(3);
        }

        #endregion

        #region Read — 连续值

        /// <summary>
        /// 读取二维移动输入。优先 Input System，回退 Legacy WASD 组合。
        /// </summary>
        private Vector2 ReadMove()
        {
#if ENABLE_INPUT_SYSTEM
            InputAction action = FindAction(_moveAction);
            if (action != null) return action.ReadValue<Vector2>();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            // Legacy 回退：WASD 按键组合
            Vector2 value = Vector2.zero;
            if (Input.GetKey(_moveForwardKey)) value.y += 1f;
            if (Input.GetKey(_moveBackKey)) value.y -= 1f;
            if (Input.GetKey(_moveRightKey)) value.x += 1f;
            if (Input.GetKey(_moveLeftKey)) value.x -= 1f;
            return value;
#else
            return Vector2.zero;
#endif
        }

        /// <summary>
        /// 读取视角增量输入。优先 Input System，回退 Legacy 鼠标轴。
        /// </summary>
        private Vector2 ReadLook()
        {
#if ENABLE_INPUT_SYSTEM
            InputAction action = FindAction(_lookAction);
            if (action != null) return action.ReadValue<Vector2>();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        /// <summary>
        /// 读取滚轮增量。优先 Input System，回退 Legacy 鼠标滚轮。
        /// </summary>
        private int ReadScroll()
        {
#if ENABLE_INPUT_SYSTEM
            InputAction action = FindAction(_scrollAction);
            if (action != null)
            {
                float scroll = action.ReadValue<Vector2>().y;
                // 仅在值变化时返回舍入值，避免高频零值噪音
                if (Mathf.Abs(scroll - _lastScrollY) > 0.001f)
                {
                    _lastScrollY = scroll;
                    return Mathf.RoundToInt(scroll);
                }

                return 0;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Mathf.RoundToInt(Input.mouseScrollDelta.y);
#else
            return 0;
#endif
        }

        #endregion

        #region Read — 按钮状态

        /// <summary>
        /// 读取按钮本帧是否按下。优先 Input System 的 WasPressedThisFrame。
        /// </summary>
        private bool ReadPressed(string actionName, KeyCode legacyKey)
        {
#if ENABLE_INPUT_SYSTEM
            InputAction action = FindAction(actionName);
            if (action != null) return action.WasPressedThisFrame();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(legacyKey);
#else
            return false;
#endif
        }

        /// <summary>
        /// 读取按钮当前是否按住。优先 Input System 的 IsPressed。
        /// </summary>
        private bool ReadHeld(string actionName, KeyCode legacyKey)
        {
#if ENABLE_INPUT_SYSTEM
            InputAction action = FindAction(actionName);
            if (action != null) return action.IsPressed();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(legacyKey);
#else
            return false;
#endif
        }

        /// <summary>
        /// 读取按钮本帧是否抬起。优先 Input System 的 WasReleasedThisFrame。
        /// </summary>
        private bool ReadReleased(string actionName, KeyCode legacyKey)
        {
#if ENABLE_INPUT_SYSTEM
            InputAction action = FindAction(actionName);
            if (action != null) return action.WasReleasedThisFrame();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyUp(legacyKey);
#else
            return false;
#endif
        }

        #endregion

        #region Input System 辅助

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// 从 PlayerInput 的 Action Asset 中按名称查找 InputAction。
        /// </summary>
        /// <param name="actionName">Action 名称，为空或 PlayerInput 缺失时返回 null</param>
        /// <returns>匹配的 InputAction，未找到则返回 null</returns>
        private InputAction FindAction(string actionName)
        {
            // PlayerInput 或 Action 名无效 → 回退 Legacy
            if (_playerInput == null || string.IsNullOrEmpty(actionName)) return null;

            return _playerInput.actions?.FindAction(actionName, false);
        }
#endif

        #endregion
    }
}
