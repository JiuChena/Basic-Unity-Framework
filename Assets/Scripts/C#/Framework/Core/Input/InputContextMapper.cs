using System.Collections.Generic;
using UnityEngine;

namespace CoreFramework
{
    /// <summary>
    /// 将逻辑动作状态翻译为既有领域输入数据槽的边界协议。
    /// </summary>
    public interface IInputContextMapper
    {
        void Write(Blackboard board, InputActionStateStore actions);
    }

    /// <summary>
    /// 标准角色上下文映射：移动、战斗与交互系统继续消费原有数据槽。
    /// </summary>
    public sealed class StandardInputContextMapper : IInputContextMapper
    {
        private struct ButtonCursor
        {
            public uint pressed;
            public uint released;
        }

        private readonly Dictionary<InputActionId, ButtonCursor> _buttonCursors =
            new Dictionary<InputActionId, ButtonCursor>();
        private readonly float _lookSensitivity;

        public StandardInputContextMapper(float lookSensitivity = 1f)
        {
            _lookSensitivity = Mathf.Max(0f, lookSensitivity);
        }

        public void Write(Blackboard board, InputActionStateStore actions)
        {
            LocomotionInputData locomotion = board.GetOrCreate<LocomotionInputData>();
            CombatInputData combat = board.GetOrCreate<CombatInputData>();
            InteractionInputData interaction = board.GetOrCreate<InteractionInputData>();

            locomotion.Move = ReadVector2(actions, StandardInputActions.Move);
            if (locomotion.Move.sqrMagnitude > 1f) locomotion.Move = locomotion.Move.normalized;
            locomotion.Look = ReadVector2(actions, StandardInputActions.Look) * _lookSensitivity;
            locomotion.IsSprinting = ReadHeld(actions, StandardInputActions.Sprint);
            WriteButton(actions, StandardInputActions.Jump, locomotion.Jump);
            WriteButton(actions, StandardInputActions.Crouch, locomotion.Crouch);

            WriteButton(actions, StandardInputActions.Attack, combat.Attack);
            WriteButton(actions, StandardInputActions.Reload, combat.Reload);
            combat.IsAiming = ReadHeld(actions, StandardInputActions.Aim);

            WriteButton(actions, StandardInputActions.Interact, interaction.Interact);
            interaction.ScrollDelta = Mathf.RoundToInt(ReadFloat(actions, StandardInputActions.Scroll));
            TryWriteSwitch(actions, interaction, StandardInputActions.Switch1, 1);
            TryWriteSwitch(actions, interaction, StandardInputActions.Switch2, 2);
            TryWriteSwitch(actions, interaction, StandardInputActions.Switch3, 3);
            TryWriteSwitch(actions, interaction, StandardInputActions.Switch4, 4);
        }

        private void TryWriteSwitch(
            InputActionStateStore actions,
            InteractionInputData interaction,
            InputActionId actionId,
            int switchIndex)
        {
            if (!actions.TryGet(actionId, out InputActionState state)) return;

            ButtonCursor cursor = GetCursor(actionId);
            bool wasPressed = state.ConsumePressed(ref cursor.pressed);
            SetCursor(actionId, cursor);
            if (wasPressed) interaction.RequestSwitch(switchIndex);
        }

        private void WriteButton(InputActionStateStore actions, InputActionId actionId, InputButton target)
        {
            if (!actions.TryGet(actionId, out InputActionState state))
            {
                target.SetState(false, false, false);
                return;
            }

            ButtonCursor cursor = GetCursor(actionId);
            bool wasPressed = state.ConsumePressed(ref cursor.pressed);
            bool wasReleased = state.ConsumeReleased(ref cursor.released);
            SetCursor(actionId, cursor);
            target.SetState(wasPressed, state.IsHeld, wasReleased);
        }

        private ButtonCursor GetCursor(InputActionId actionId)
        {
            return _buttonCursors.TryGetValue(actionId, out ButtonCursor cursor) ? cursor : default;
        }

        private void SetCursor(InputActionId actionId, ButtonCursor cursor)
        {
            _buttonCursors[actionId] = cursor;
        }

        private static bool ReadHeld(InputActionStateStore actions, InputActionId actionId)
        {
            return actions.TryGet(actionId, out InputActionState state) && state.IsHeld;
        }

        private static float ReadFloat(InputActionStateStore actions, InputActionId actionId)
        {
            return actions.TryGet(actionId, out InputActionState state) ? state.FloatValue : 0f;
        }

        private static Vector2 ReadVector2(InputActionStateStore actions, InputActionId actionId)
        {
            return actions.TryGet(actionId, out InputActionState state) ? state.Vector2Value : Vector2.zero;
        }
    }
}
