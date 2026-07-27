using System;

namespace Framework.ExpandComponent.DataProvider
{
    /// <summary>
    /// 支持多个独立消费者的按钮状态与边沿事件。
    /// 通过递增版本号实现多消费者无竞态的边沿消费。
    /// </summary>
    [Serializable]
    public sealed class InputButton
    {
        /// <summary>当前是否处于按住状态</summary>
        public bool IsHeld { get; private set; }

        /// <summary>按下事件累计版本号</summary>
        public uint PressedVersion { get; private set; }

        /// <summary>抬起事件累计版本号</summary>
        public uint ReleasedVersion { get; private set; }

        #region Write

        /// <summary>
        /// 根据持续按住状态自动推导按下与抬起边沿。
        /// 普通设备输入应优先使用此方法，避免调用方构造不完整的三元状态。
        /// </summary>
        public void SetHeld(bool isHeld)
        {
            if (!IsHeld && isHeld) PressedVersion++;
            if (IsHeld && !isHeld) ReleasedVersion++;
            IsHeld = isHeld;
        }

        /// <summary>
        /// 写入一次不改变持续状态的按下命令，用于 AI、回放等瞬时动作源。
        /// </summary>
        public void Trigger()
        {
            PressedVersion++;
        }

        #endregion

        #region Consume

        /// <summary>将按下事件游标初始化到当前版本，不回放此前事件。</summary>
        public void InitializePressedCursor(ref uint consumedVersion)
        {
            consumedVersion = PressedVersion;
        }

        /// <summary>将抬起事件游标初始化到当前版本，不回放此前事件。</summary>
        public void InitializeReleasedCursor(ref uint consumedVersion)
        {
            consumedVersion = ReleasedVersion;
        }

        /// <summary>
        /// 按消费者游标读取一次尚未处理的按下事件。
        /// 消费者应在开始监听时调用 InitializePressedCursor；未初始化的默认游标会消费首个真实事件。
        /// </summary>
        /// <param name="consumedVersion">调用方持有的最后已消费版本号</param>
        /// <param name="pressed">未消费的按下事件存在时为 true</param>
        /// <returns>存在未消费的按下事件时返回 true</returns>
        public bool ConsumePressed(ref uint consumedVersion, out bool pressed)
        {
            if (consumedVersion == PressedVersion)
            {
                pressed = false;
                return false;
            }

            consumedVersion = PressedVersion;
            pressed = true;
            return true;
        }

        /// <summary>
        /// 按消费者游标读取一次尚未处理的抬起事件。
        /// 消费者应在开始监听时调用 InitializeReleasedCursor；未初始化的默认游标会消费首个真实事件。
        /// </summary>
        /// <param name="consumedVersion">调用方持有的最后已消费版本号</param>
        /// <param name="released">未消费的抬起事件存在时为 true</param>
        /// <returns>存在未消费的抬起事件时返回 true</returns>
        public bool ConsumeReleased(ref uint consumedVersion, out bool released)
        {
            if (consumedVersion == ReleasedVersion)
            {
                released = false;
                return false;
            }

            consumedVersion = ReleasedVersion;
            released = true;
            return true;
        }

        #endregion

        #region Clear

        /// <summary>
        /// 清空持续状态并归零所有事件版本号。
        /// </summary>
        public void Clear()
        {
            IsHeld = false;
            PressedVersion = 0;
            ReleasedVersion = 0;
        }

        #endregion
    }
}
