using System;

namespace CoreFramework
{
    /// <summary>
    /// 支持多个独立消费者的按钮状态与边沿事件。
    /// </summary>
    [Serializable]
    public sealed class InputButton
    {
        /// <summary>
        /// 当前是否处于按住状态。
        /// </summary>
        public bool IsHeld { get; private set; }

        /// <summary>
        /// 按下事件累计版本号。
        /// </summary>
        public uint PressedVersion { get; private set; }

        /// <summary>
        /// 抬起事件累计版本号。
        /// </summary>
        public uint ReleasedVersion { get; private set; }

        /// <summary>
        /// 根据输入源本帧状态更新按钮。
        /// </summary>
        /// <param name="wasPressed">本帧是否发生按下边沿。</param>
        /// <param name="isHeld">当前是否持续按住。</param>
        /// <param name="wasReleased">本帧是否发生抬起边沿。</param>
        public void SetState(bool wasPressed, bool isHeld, bool wasReleased)
        {
            if (wasPressed) PressedVersion++;
            if (wasReleased) ReleasedVersion++;
            IsHeld = isHeld;
        }

        /// <summary>
        /// 按消费者游标读取一次尚未处理的按下事件。
        /// </summary>
        /// <param name="consumedVersion">调用方持有的最后已消费版本号。</param>
        /// <returns>存在未消费按下事件时返回 true。</returns>
        public bool ConsumePressed(ref uint consumedVersion)
        {
            if (consumedVersion == PressedVersion) return false;

            consumedVersion = PressedVersion;
            return true;
        }

        /// <summary>
        /// 按消费者游标读取一次尚未处理的抬起事件。
        /// </summary>
        /// <param name="consumedVersion">调用方持有的最后已消费版本号。</param>
        /// <returns>存在未消费抬起事件时返回 true。</returns>
        public bool ConsumeReleased(ref uint consumedVersion)
        {
            if (consumedVersion == ReleasedVersion) return false;

            consumedVersion = ReleasedVersion;
            return true;
        }

        /// <summary>
        /// 清空持续状态与所有事件版本。
        /// </summary>
        public void Clear()
        {
            IsHeld = false;
            PressedVersion = 0;
            ReleasedVersion = 0;
        }
    }
}
