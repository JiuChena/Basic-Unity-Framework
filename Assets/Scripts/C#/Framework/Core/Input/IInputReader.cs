using System;
using UnityEngine;

namespace CoreFramework
{
    /// <summary>
    /// 将某种设备或数据源采样结果写入逻辑动作状态表的适配器。
    /// </summary>
    public interface IInputReader
    {
        void RegisterActions(InputActionStateStore stateStore);
        void Tick(InputActionStateStore stateStore);
    }

    /// <summary>
    /// 当前框架标准动作的一帧采样结果，用于兼容既有 Input System / Legacy 读取方法。
    /// </summary>
    public struct StandardInputSnapshot
    {
        public Vector2 move;
        public Vector2 look;
        public float scroll;
        public bool jumpPressed;
        public bool jumpHeld;
        public bool jumpReleased;
        public bool sprintHeld;
        public bool crouchPressed;
        public bool crouchHeld;
        public bool crouchReleased;
        public bool attackPressed;
        public bool attackHeld;
        public bool attackReleased;
        public bool aimHeld;
        public bool reloadPressed;
        public bool interactPressed;
        public bool switch1Pressed;
        public bool switch2Pressed;
        public bool switch3Pressed;
        public bool switch4Pressed;
    }

    /// <summary>
    /// 将已有读取逻辑适配为标准动作状态。用于旧场景未配置 Binding Profile 的兼容路径。
    /// </summary>
    public sealed class DelegateInputReader : IInputReader
    {
        private readonly Func<StandardInputSnapshot> _readSnapshot;

        public DelegateInputReader(Func<StandardInputSnapshot> readSnapshot)
        {
            _readSnapshot = readSnapshot ?? throw new ArgumentNullException(nameof(readSnapshot));
        }

        public void RegisterActions(InputActionStateStore stateStore)
        {
            StandardInputActionRegistration.Register(stateStore);
        }

        public void Tick(InputActionStateStore stateStore)
        {
            StandardInputSnapshot input = _readSnapshot();
            stateStore.SetVector2(StandardInputActions.Move, input.move);
            stateStore.SetVector2(StandardInputActions.Look, input.look);
            stateStore.SetFloat(StandardInputActions.Scroll, input.scroll);
            stateStore.SetButton(StandardInputActions.Jump, input.jumpPressed, input.jumpHeld, input.jumpReleased);
            stateStore.SetButton(StandardInputActions.Sprint, false, input.sprintHeld, false);
            stateStore.SetButton(StandardInputActions.Crouch, input.crouchPressed, input.crouchHeld, input.crouchReleased);
            stateStore.SetButton(StandardInputActions.Attack, input.attackPressed, input.attackHeld, input.attackReleased);
            stateStore.SetButton(StandardInputActions.Aim, false, input.aimHeld, false);
            stateStore.SetButton(StandardInputActions.Reload, input.reloadPressed, false, false);
            stateStore.SetButton(StandardInputActions.Interact, input.interactPressed, false, false);
            stateStore.SetButton(StandardInputActions.Switch1, input.switch1Pressed, false, false);
            stateStore.SetButton(StandardInputActions.Switch2, input.switch2Pressed, false, false);
            stateStore.SetButton(StandardInputActions.Switch3, input.switch3Pressed, false, false);
            stateStore.SetButton(StandardInputActions.Switch4, input.switch4Pressed, false, false);
        }
    }
}
