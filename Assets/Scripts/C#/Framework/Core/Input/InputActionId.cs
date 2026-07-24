using System;
using UnityEngine;

namespace CoreFramework
{
    /// <summary>
    /// 逻辑输入动作的稳定标识。显示名可修改，GUID 用于绑定、回放和运行时查找。
    /// </summary>
    [Serializable]
    public struct InputActionId : IEquatable<InputActionId>
    {
        [SerializeField] private string _guid;
        [SerializeField] private string _displayName;

        public string Guid => _guid;
        public string DisplayName => _displayName;
        public bool IsValid => !string.IsNullOrEmpty(_guid);

        public InputActionId(string guid, string displayName)
        {
            _guid = guid;
            _displayName = displayName;
        }

        public bool Equals(InputActionId other)
        {
            return string.Equals(_guid, other._guid, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is InputActionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _guid == null ? 0 : StringComparer.Ordinal.GetHashCode(_guid);
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(_displayName) ? _guid : _displayName;
        }

        public static bool operator ==(InputActionId left, InputActionId right) => left.Equals(right);
        public static bool operator !=(InputActionId left, InputActionId right) => !left.Equals(right);
    }

    /// <summary>
    /// 逻辑动作承载的数值类型。
    /// </summary>
    public enum InputValueType
    {
        Button,
        Value1D,
        Value2D
    }

    /// <summary>
    /// 框架内置动作的稳定 ID 集合。新增游戏动作应使用独立定义资产或同样固定的 GUID。
    /// </summary>
    public static class StandardInputActions
    {
        public static readonly InputActionId Move = new InputActionId("a24f821b-8949-4a95-a1b3-000000000001", "Move");
        public static readonly InputActionId Look = new InputActionId("a24f821b-8949-4a95-a1b3-000000000002", "Look");
        public static readonly InputActionId Jump = new InputActionId("a24f821b-8949-4a95-a1b3-000000000003", "Jump");
        public static readonly InputActionId Sprint = new InputActionId("a24f821b-8949-4a95-a1b3-000000000004", "Sprint");
        public static readonly InputActionId Crouch = new InputActionId("a24f821b-8949-4a95-a1b3-000000000005", "Crouch");
        public static readonly InputActionId Attack = new InputActionId("a24f821b-8949-4a95-a1b3-000000000006", "Attack");
        public static readonly InputActionId Aim = new InputActionId("a24f821b-8949-4a95-a1b3-000000000007", "Aim");
        public static readonly InputActionId Reload = new InputActionId("a24f821b-8949-4a95-a1b3-000000000008", "Reload");
        public static readonly InputActionId Interact = new InputActionId("a24f821b-8949-4a95-a1b3-000000000009", "Interact");
        public static readonly InputActionId Scroll = new InputActionId("a24f821b-8949-4a95-a1b3-000000000010", "Scroll");
        public static readonly InputActionId Switch1 = new InputActionId("a24f821b-8949-4a95-a1b3-000000000011", "Switch 1");
        public static readonly InputActionId Switch2 = new InputActionId("a24f821b-8949-4a95-a1b3-000000000012", "Switch 2");
        public static readonly InputActionId Switch3 = new InputActionId("a24f821b-8949-4a95-a1b3-000000000013", "Switch 3");
        public static readonly InputActionId Switch4 = new InputActionId("a24f821b-8949-4a95-a1b3-000000000014", "Switch 4");
    }

    /// <summary>
    /// 将框架内置动作注册到指定实体的状态表。
    /// </summary>
    public static class StandardInputActionRegistration
    {
        public static void Register(InputActionStateStore stateStore)
        {
            stateStore.Register(StandardInputActions.Move, InputValueType.Value2D);
            stateStore.Register(StandardInputActions.Look, InputValueType.Value2D);
            stateStore.Register(StandardInputActions.Scroll, InputValueType.Value1D);
            stateStore.Register(StandardInputActions.Jump, InputValueType.Button);
            stateStore.Register(StandardInputActions.Sprint, InputValueType.Button);
            stateStore.Register(StandardInputActions.Crouch, InputValueType.Button);
            stateStore.Register(StandardInputActions.Attack, InputValueType.Button);
            stateStore.Register(StandardInputActions.Aim, InputValueType.Button);
            stateStore.Register(StandardInputActions.Reload, InputValueType.Button);
            stateStore.Register(StandardInputActions.Interact, InputValueType.Button);
            stateStore.Register(StandardInputActions.Switch1, InputValueType.Button);
            stateStore.Register(StandardInputActions.Switch2, InputValueType.Button);
            stateStore.Register(StandardInputActions.Switch3, InputValueType.Button);
            stateStore.Register(StandardInputActions.Switch4, InputValueType.Button);
        }
    }
}
