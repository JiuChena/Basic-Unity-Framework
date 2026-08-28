using System;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Input
{
    /// <summary>定义一个通用按钮标识与 Input Action 名称之间的配置映射。</summary>
    [Serializable]
    public sealed class InputButtonBinding
    {
        // 写入输入上下文的通用按钮标识。
        [Tooltip("读取到动作状态后写入输入上下文的通用按钮标识")]
        [SerializeField] private InputButton _button;
        // PlayerInput 动作资产中需要读取的动作名称。
        [Tooltip("PlayerInput 动作资产中需要读取的按钮动作名称")]
        [SerializeField] private string _actionName;

        /// <summary>获取绑定写入的通用按钮标识。</summary>
        public InputButton Button => _button;

        /// <summary>获取 PlayerInput 动作资产中的动作名称。</summary>
        public string ActionName => _actionName;

        /// <summary>创建供 Unity Inspector 填写的空按钮动作映射。</summary>
        public InputButtonBinding() { }

        /// <summary>创建默认输入配置使用的按钮动作映射。</summary>
        /// <param name="button">写入输入上下文的按钮标识。</param>
        /// <param name="actionName">PlayerInput 动作资产中的动作名称。</param>
        public InputButtonBinding(InputButton button, string actionName)
        {
            _button = button;
            _actionName = actionName;
        }
    }
}
