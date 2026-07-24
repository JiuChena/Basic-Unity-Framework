using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CoreFramework
{
    /// <summary>
    /// 逻辑动作到具体输入 Reader 配置的映射资产。Input System 的实际 Binding 仍保留在 .inputactions。
    /// </summary>
    [CreateAssetMenu(menuName = "Core Framework/Input Binding Profile", fileName = "InputBindingProfile")]
    public sealed class InputBindingProfile : ScriptableObject
    {
#if ENABLE_INPUT_SYSTEM
        [Serializable]
        public struct InputSystemBinding
        {
            [Tooltip("框架逻辑动作的稳定 ID")]
            public InputActionId actionId;

            [Tooltip("逻辑动作的值类型，必须与 Input System Action 的预期值一致")]
            public InputValueType valueType;

            [Tooltip(".inputactions 资产中对应的 Action 引用")]
            public InputActionReference inputAction;
        }

        [Tooltip("用于运行时解析 Action 实例的 Input System 资产")]
        public InputActionAsset inputActionAsset;

        [Tooltip("逻辑动作与 Input System Action 的映射")]
        public List<InputSystemBinding> inputSystemBindings = new List<InputSystemBinding>();

        public IReadOnlyList<InputSystemBinding> InputSystemBindings => inputSystemBindings;
        public bool HasInputSystemBindings => inputSystemBindings != null && inputSystemBindings.Count > 0;
#else
        public bool HasInputSystemBindings => false;
#endif
    }
}
