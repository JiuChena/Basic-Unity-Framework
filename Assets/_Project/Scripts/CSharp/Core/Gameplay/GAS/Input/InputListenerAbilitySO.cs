using System.Collections.Generic;
using Framework.Gameplay.Abilities.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存输入监听能力的动作资产和通用按钮映射配置。</summary>
    [CreateAssetMenu(fileName = "InputListenerAbility", menuName = "Framework/Gameplay/Abilities/Input Listener")]
    public sealed class InputListenerAbilitySO : AbilityDefinitionSO
    {
        // 输入能力自动创建 PlayerInput 时使用的动作资产。
        [Header("输入动作")]
        [Tooltip("目标单位没有 PlayerInput 时自动创建组件并绑定的 Input Actions 资产")]
        [SerializeField] private InputActionAsset _actions;
        // 输入动作所在的动作地图名称。
        [Tooltip("读取输入动作的地图名称")]
        [SerializeField] private string _actionMapName = "Player";
        // 平面移动动作名称。
        [Tooltip("读取 Vector2 平面移动输入的动作名称")]
        [SerializeField] private string _moveActionName = "Move";
        // 通用按钮与 Input Action 的映射表。
        [Header("按钮映射")]
        [Tooltip("每项将一个 Input Action 的按住、按下和松开状态写入对应通用按钮；同一按钮只应配置一次")]
        [SerializeField] private List<InputButtonBinding> _buttonBindings = new List<InputButtonBinding>
        {
            new InputButtonBinding(InputButton.Jump, "Jump"),
        };

        /// <summary>获取自动创建 PlayerInput 时使用的动作资产。</summary>
        public InputActionAsset Actions => _actions;
        /// <summary>获取输入动作地图名称。</summary>
        public string ActionMapName => _actionMapName;
        /// <summary>获取平面移动动作名称。</summary>
        public string MoveActionName => _moveActionName;
        /// <summary>获取通用按钮与 Input Action 的配置映射表。</summary>
        public IReadOnlyList<InputButtonBinding> ButtonBindings => _buttonBindings;

        /// <summary>创建输入监听能力运行时。</summary>
        /// <returns>使用当前动作配置的输入监听运行时。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new InputListenerAbilityRuntime(this);
        }
    }
}
