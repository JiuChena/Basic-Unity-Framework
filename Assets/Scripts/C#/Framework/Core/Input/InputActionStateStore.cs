using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoreFramework
{
    /// <summary>
    /// 单个逻辑动作的当前值和可由多个消费者独立读取的边沿事件。
    /// </summary>
    public sealed class InputActionState
    {
        public InputValueType ValueType { get; }
        public bool IsHeld { get; private set; }
        public float FloatValue { get; private set; }
        public Vector2 Vector2Value { get; private set; }
        public uint PressedVersion { get; private set; }
        public uint ReleasedVersion { get; private set; }

        internal InputActionState(InputValueType valueType)
        {
            ValueType = valueType;
        }

        internal void SetButton(bool wasPressed, bool isHeld, bool wasReleased)
        {
            if (ValueType != InputValueType.Button)
                throw new InvalidOperationException("Only Button actions can receive button state.");

            if (wasPressed) PressedVersion++;
            if (wasReleased) ReleasedVersion++;
            IsHeld = isHeld;
            FloatValue = isHeld ? 1f : 0f;
            Vector2Value = Vector2.zero;
        }

        internal void SetFloat(float value)
        {
            if (ValueType != InputValueType.Value1D)
                throw new InvalidOperationException("Only Value1D actions can receive float state.");

            FloatValue = value;
            Vector2Value = Vector2.zero;
            IsHeld = !Mathf.Approximately(value, 0f);
        }

        internal void SetVector2(Vector2 value)
        {
            if (ValueType != InputValueType.Value2D)
                throw new InvalidOperationException("Only Value2D actions can receive Vector2 state.");

            Vector2Value = value;
            FloatValue = 0f;
            IsHeld = value.sqrMagnitude > 0.0001f;
        }

        public bool ConsumePressed(ref uint consumedVersion)
        {
            if (consumedVersion == PressedVersion) return false;

            consumedVersion = PressedVersion;
            return true;
        }

        public bool ConsumeReleased(ref uint consumedVersion)
        {
            if (consumedVersion == ReleasedVersion) return false;

            consumedVersion = ReleasedVersion;
            return true;
        }
    }

    /// <summary>
    /// 绑定到单个实体 Blackboard 的逻辑输入动作状态表。
    /// </summary>
    public sealed class InputActionStateStore : IBlackboardData
    {
        private readonly Dictionary<InputActionId, InputActionState> _states =
            new Dictionary<InputActionId, InputActionState>();

        public void Register(InputActionId actionId, InputValueType valueType)
        {
            if (!actionId.IsValid) throw new ArgumentException("Input action ID must be valid.", nameof(actionId));

            if (_states.TryGetValue(actionId, out InputActionState existing))
            {
                if (existing.ValueType != valueType)
                    throw new InvalidOperationException($"Input action '{actionId}' is already registered as {existing.ValueType}.");
                return;
            }

            _states.Add(actionId, new InputActionState(valueType));
        }

        public bool Unregister(InputActionId actionId)
        {
            return _states.Remove(actionId);
        }

        public bool TryGet(InputActionId actionId, out InputActionState state)
        {
            return _states.TryGetValue(actionId, out state);
        }

        public void SetButton(InputActionId actionId, bool wasPressed, bool isHeld, bool wasReleased)
        {
            GetRequiredState(actionId, InputValueType.Button).SetButton(wasPressed, isHeld, wasReleased);
        }

        public void SetFloat(InputActionId actionId, float value)
        {
            GetRequiredState(actionId, InputValueType.Value1D).SetFloat(value);
        }

        public void SetVector2(InputActionId actionId, Vector2 value)
        {
            GetRequiredState(actionId, InputValueType.Value2D).SetVector2(value);
        }

        private InputActionState GetRequiredState(InputActionId actionId, InputValueType valueType)
        {
            if (!_states.TryGetValue(actionId, out InputActionState state))
                throw new InvalidOperationException($"Input action '{actionId}' has not been registered.");
            if (state.ValueType != valueType)
                throw new InvalidOperationException($"Input action '{actionId}' expects {state.ValueType}, not {valueType}.");
            return state;
        }
    }
}
