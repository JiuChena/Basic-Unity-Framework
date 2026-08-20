using System;
using UnityEngine;
using Framework.ExpandComponent.DataProvider;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Framework.ExpandComponent.DataProvider.Example
{
    /// <summary>
    /// 采集玩家设备输入并写入 PlayerBlackboard 的纯 C# 处理器。
    /// 优先使用 Unity Input System，Legacy Input Manager 作为回退。
    /// 键位绑定通过序列化字段暴露在 Inspector 中，无需自定义 Editor。
    /// </summary>
    [Serializable]
    public sealed class PlayerDataSourceHandler : DataSourceHandler<PlayerBlackboard>
    {
        [Min(0f)] [SerializeField] private float _lookSensitivity = 2f;

        [Header("Input System Actions")]
        [SerializeField] private string _moveAction = "Move";
        [SerializeField] private string _lookAction = "Look";
        [SerializeField] private string _jumpAction = "Jump";
        [SerializeField] private string _sprintAction = "Sprint";
        [SerializeField] private string _crouchAction = "Crouch";
        [SerializeField] private string _interactAction = "Interact";
        [SerializeField] private string _scrollAction = "Scroll";

#if ENABLE_INPUT_SYSTEM
        [SerializeField] private PlayerInput _playerInput;
#endif

        [Header("Legacy Keys")]
        [SerializeField] private KeyCode _moveForwardKey = KeyCode.W;
        [SerializeField] private KeyCode _moveBackKey = KeyCode.S;
        [SerializeField] private KeyCode _moveLeftKey = KeyCode.A;
        [SerializeField] private KeyCode _moveRightKey = KeyCode.D;
        [SerializeField] private KeyCode _jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode _sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode _crouchKey = KeyCode.C;
        [SerializeField] private KeyCode _interactKey = KeyCode.F;

        #region Lifecycle

        /// <summary>
        /// 初始化阶段自动从 Owner GameObject 获取 PlayerInput 组件。
        /// </summary>
        protected override void OnInitialize()
        {
#if ENABLE_INPUT_SYSTEM
            if (_playerInput == null)
                _playerInput = Owner.GetComponent<PlayerInput>();
#endif
        }

        /// <summary>
        /// 每帧执行数据采集，从设备读取原始输入并写入 Blackboard 各属性。
        /// 连续值属性写 Value，增量属性写 Add，按钮属性写 SetHeld。
        /// </summary>
        /// <param name="blackboard">当前实体的 PlayerBlackboard 实例</param>
        protected override void ProcessData(PlayerBlackboard blackboard)
        {
            blackboard.Move.Value = ReadMove();
            blackboard.Look.Add(ReadLook() * _lookSensitivity);
            blackboard.Sprint.Value = ReadHeld(_sprintAction, _sprintKey);
            blackboard.Scroll.Add(ReadScroll());
            blackboard.Crouch.SetHeld(ReadHeld(_crouchAction, _crouchKey));
            blackboard.Jump.SetHeld(ReadHeld(_jumpAction, _jumpKey));
            blackboard.Interact.SetHeld(ReadHeld(_interactAction, _interactKey));
        }

        #endregion

        #region Device Input — Continuous Values

        /// <summary>
        /// 读取二维移动输入。优先 Input System 的 Vector2 值，回退 Legacy WASD 组合键。
        /// </summary>
        /// <returns>未归一化的移动向量</returns>
        private Vector2 ReadMove()
        {
#if ENABLE_INPUT_SYSTEM
            InputAction action = FindAction(_moveAction);
            if (action != null) return action.ReadValue<Vector2>();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
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
        /// <returns>未乘灵敏度的原始视角增量</returns>
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
        /// 读取滚轮增量。优先 Input System Vector2 的 y 分量，回退 Legacy 鼠标滚轮。
        /// </summary>
        /// <returns>本帧滚轮离散增量</returns>
        private int ReadScroll()
        {
#if ENABLE_INPUT_SYSTEM
            InputAction action = FindAction(_scrollAction);
            if (action != null) return Mathf.RoundToInt(action.ReadValue<Vector2>().y);
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Mathf.RoundToInt(Input.mouseScrollDelta.y);
#else
            return 0;
#endif
        }

        #endregion

        #region Device Input — Button State

        /// <summary>
        /// 读取按钮当前是否按住。优先 Input System 的 IsPressed，回退 Legacy 按键。
        /// </summary>
        /// <param name="actionName">Input System Action 名称，为空时跳过</param>
        /// <param name="legacyKey">Legacy 回退键值</param>
        /// <returns>按住时返回 true</returns>
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

        #endregion

        #region Input System Helpers

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// 从 PlayerInput 的 Action Asset 中按名称查找 InputAction。
        /// </summary>
        /// <param name="actionName">Action 名称，为空或 PlayerInput 未配置时返回 null</param>
        /// <returns>匹配的 InputAction，未找到返回 null 触发 Legacy 回退</returns>
        private InputAction FindAction(string actionName)
        {
            return _playerInput == null || string.IsNullOrEmpty(actionName)
                ? null
                : _playerInput.actions?.FindAction(actionName, false);
        }
#endif

        #endregion
    }
}
