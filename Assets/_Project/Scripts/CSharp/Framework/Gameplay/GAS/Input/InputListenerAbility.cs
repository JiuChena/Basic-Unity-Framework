using Framework.Gameplay.Abilities.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Framework.Gameplay.Abilities.Input
{
    /// <summary>每帧读取 PlayerInput 并写入单位输入黑板。</summary>
    public sealed class InputListenerAbility : AbilityRuntime
    {
        // 输入动作静态配置表。
        private readonly InputListenerAbilitySO _configuration;
        // 当前单位的输入来源组件。
        private PlayerInput _playerInput;
        // 当前单位独占的输入黑板。
        private InputBlackboard _blackboard;
        // 缓存的平面移动动作。
        private InputAction _moveAction;
        // 缓存的跳跃动作。
        private InputAction _jumpAction;
        // 缓存的冲刺动作。
        private InputAction _sprintAction;

        /// <summary>创建输入监听运行时并保存配置表引用。</summary>
        /// <param name="configuration">输入动作配置表；允许为 null 并使用默认动作名称。</param>
        public InputListenerAbility(InputListenerAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>创建输入黑板并缓存 PlayerInput 动作引用。</summary>
        /// <param name="context">当前单位能力上下文。</param>
        public override void Initialize(AbilityContext context)
        {
            base.Initialize(context);
            _blackboard = new InputBlackboard();
            Context?.Register(AbilityContextDataType.Input, _blackboard);
            if (context == null || context.Owner == null) return;

            // 获取或创建输入来源组件，并在缺失动作资产时由配置表补齐。
            _playerInput = EnsurePlayerInput(context.Owner);

            // 初始化阶段缓存动作引用，运行帧只读取缓存。
            CacheActions(_playerInput);
        }

        /// <summary>读取当前帧输入并更新输入黑板。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒；输入采集不依赖该值。</param>
        public override void UpdateAbility(float deltaTime)
        {
            if (_blackboard == null) return;

            // 从缓存动作读取当前帧值，缺失动作按中性输入处理。
            Vector2 move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            bool jumpHeld = _jumpAction != null && _jumpAction.IsPressed();
            bool sprintHeld = _sprintAction != null && _sprintAction.IsPressed();

            // 写入完整输入帧，黑板负责生成并保留跳跃按下边沿。
            _blackboard.WriteFrame(move, jumpHeld, sprintHeld);
        }

        /// <summary>清空输入状态，避免能力禁用后残留按键边沿。</summary>
        public override void OnAbilityDisable()
        {
            _blackboard?.Reset();
        }

        /// <summary>重新启用输入来源并清空上一轮输入状态。</summary>
        public override void OnAbilityEnable()
        {
            _blackboard?.Reset();
            if (_playerInput == null) return;
            _playerInput.ActivateInput();
            CacheActions(_playerInput);
        }

        /// <summary>释放输入动作和黑板引用。</summary>
        public override void DisposeAbility()
        {
            OnAbilityDisable();
            Context?.Unregister(AbilityContextDataType.Input, _blackboard);
            _moveAction = null;
            _jumpAction = null;
            _sprintAction = null;
            _playerInput = null;
            _blackboard = null;
            base.DisposeAbility();
        }

        /// <summary>从 PlayerInput 的动作资产缓存输入动作。</summary>
        private void CacheActions(PlayerInput playerInput)
        {
            if (playerInput == null || playerInput.actions == null) return;

            // 优先从指定动作地图查找，地图为空时回退到全资产查找。
            string actionMapName = _configuration != null ? _configuration.ActionMapName : "Player";
            string moveActionName = _configuration != null ? _configuration.MoveActionName : "Move";
            string jumpActionName = _configuration != null ? _configuration.JumpActionName : "Jump";
            string sprintActionName = _configuration != null ? _configuration.SprintActionName : "Sprint";
            InputActionMap actionMap = string.IsNullOrWhiteSpace(actionMapName)
                ? null
                : playerInput.actions.FindActionMap(actionMapName, false);
            _moveAction = FindAction(playerInput.actions, actionMap, moveActionName);
            _jumpAction = FindAction(playerInput.actions, actionMap, jumpActionName);
            _sprintAction = FindAction(playerInput.actions, actionMap, sprintActionName);
        }

        /// <summary>确保单位拥有可供本能力读取的 PlayerInput 组件。</summary>
        /// <param name="owner">能力所属单位对象；为 null 时返回 null。</param>
        /// <returns>已有或由配置资产创建完成的 PlayerInput；缺少动作资产时返回 null。</returns>
        private PlayerInput EnsurePlayerInput(GameObject owner)
        {
            if (owner == null) return null;

            // 已有组件优先保留业务层配置，仅在它缺少动作资产时应用能力配置。
            PlayerInput playerInput = owner.GetComponent<PlayerInput>();
            if (!Application.isPlaying) return playerInput;
            if (playerInput == null)
            {
                if (_configuration == null || _configuration.Actions == null) return null;
                playerInput = owner.AddComponent<PlayerInput>();
            }

            // 新建或未配置的 PlayerInput 由输入 SO 提供动作资产和默认动作地图。
            if (playerInput.actions == null && _configuration != null)
                playerInput.actions = _configuration.Actions;
            if (playerInput.actions == null) return null;

            string actionMapName = _configuration != null ? _configuration.ActionMapName : string.Empty;
            if (!string.IsNullOrWhiteSpace(actionMapName)) playerInput.defaultActionMap = actionMapName;
            if (!playerInput.enabled) playerInput.enabled = true;
            playerInput.ActivateInput();
            return playerInput;
        }

        /// <summary>从指定动作地图或动作资产查找动作。</summary>
        /// <param name="actions">当前 PlayerInput 使用的完整动作资产；为空时返回 null。</param>
        /// <param name="actionMap">优先查找的动作地图；为空时直接查找动作资产。</param>
        /// <param name="actionName">动作名称；为空时返回 null。</param>
        /// <returns>找到的输入动作；未找到时返回 null。</returns>
        private InputAction FindAction(
            InputActionAsset actions,
            InputActionMap actionMap,
            string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName)) return null;
            if (actionMap != null) return actionMap.FindAction(actionName, false);
            return actions != null ? actions.FindAction(actionName, false) : null;
        }
    }
}
