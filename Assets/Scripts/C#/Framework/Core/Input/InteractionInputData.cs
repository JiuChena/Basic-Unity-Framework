using System;

namespace CoreFramework
{
    /// <summary>
    /// 交互、角色切换与滚轮相关的输入数据槽。
    /// </summary>
    [Serializable]
    public sealed class InteractionInputData : IBlackboardData
    {
        /// <summary>
        /// 交互按钮状态。
        /// </summary>
        public InputButton Interact { get; } = new InputButton();

        /// <summary>
        /// 交互 — 本帧按下。
        /// </summary>
        public bool InteractPressed => Interact.Pressed;

        /// <summary>
        /// 交互 — 按住中。
        /// </summary>
        public bool Interacting => Interact.IsHeld;

        /// <summary>
        /// 交互 — 本帧抬起。
        /// </summary>
        public bool InteractEnd => Interact.Released;

        /// <summary>
        /// 最近一次请求的角色序号，-1 表示无请求。
        /// </summary>
        public int SwitchIndex { get; private set; } = -1;

        /// <summary>
        /// 角色切换请求累计版本号。
        /// </summary>
        public uint SwitchVersion { get; private set; }

        /// <summary>
        /// 最近一次滚轮增量。
        /// </summary>
        public int ScrollDelta { get; set; }

        /// <summary>
        /// 记录一次角色切换请求。
        /// </summary>
        /// <param name="switchIndex">目标角色序号。</param>
        public void RequestSwitch(int switchIndex)
        {
            if (switchIndex < 0) return;

            SwitchIndex = switchIndex;
            SwitchVersion++;
        }

        /// <summary>
        /// 清空全部交互输入状态。
        /// </summary>
        public void Clear()
        {
            Interact.Clear();
            SwitchIndex = -1;
            SwitchVersion = 0;
            ScrollDelta = 0;
        }
    }
}
