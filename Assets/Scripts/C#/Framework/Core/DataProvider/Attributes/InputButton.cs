using System;

namespace Framework.Core
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

        /// <summary>本帧是否发生按下边沿事件</summary>
        public bool Pressed { get; private set; }

        /// <summary>本帧是否发生抬起边沿事件</summary>
        public bool Released { get; private set; }

        /// <summary>按下事件累计版本号，每帧最多递增 1</summary>
        public uint PressedVersion { get; private set; }

        /// <summary>抬起事件累计版本号，每帧最多递增 1</summary>
        public uint ReleasedVersion { get; private set; }

        #region Write

        /// <summary>
        /// 根据输入源本帧采样结果更新按钮状态与边沿版本。
        /// </summary>
        /// <param name="wasPressed">本帧是否发生按下边沿</param>
        /// <param name="isHeld">当前是否持续按住</param>
        /// <param name="wasReleased">本帧是否发生抬起边沿</param>
        public void SetState(bool wasPressed, bool isHeld, bool wasReleased)
        {
            Pressed = wasPressed;
            Released = wasReleased;

            // 边沿发生时递增对应的版本号
            if (wasPressed) PressedVersion++;
            if (wasReleased) ReleasedVersion++;

            IsHeld = isHeld;
        }

        #endregion

        #region Consume

        /// <summary>
        /// 按消费者游标读取一次尚未处理的按下事件。
        /// 每帧的按下事件可被多个消费者各自独立消费，互不干扰。
        /// </summary>
        /// <param name="consumedVersion">调用方持有的最后已消费版本号</param>
        /// <returns>存在未消费的按下事件时返回 true</returns>
        public bool ConsumePressed(ref uint consumedVersion)
        {
            // 版本号相等说明本消费者已处理过此次按下
            if (consumedVersion == PressedVersion) return false;

            consumedVersion = PressedVersion;
            return true;
        }

        /// <summary>
        /// 按消费者游标读取一次尚未处理的抬起事件。
        /// </summary>
        /// <param name="consumedVersion">调用方持有的最后已消费版本号</param>
        /// <returns>存在未消费的抬起事件时返回 true</returns>
        public bool ConsumeReleased(ref uint consumedVersion)
        {
            if (consumedVersion == ReleasedVersion) return false;

            consumedVersion = ReleasedVersion;
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
            Pressed = false;
            Released = false;
            PressedVersion = 0;
            ReleasedVersion = 0;
        }

        #endregion
    }
}
