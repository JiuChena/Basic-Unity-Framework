namespace Framework.Core
{
    /// <summary>
    /// 滚轮增量属性。正值为上滚，负值为下滚。
    /// </summary>
    public sealed class ScrollAttribute : BlackboardAttribute<int>
    {
    }

    /// <summary>
    /// 角色切换请求属性。携带目标角色序号和消费版本号，
    /// 多个消费者可各自独立消费同一帧的切换请求。
    /// </summary>
    public sealed class SwitchCharacterAttribute : BlackboardAttribute<int>
    {
        /// <summary>切换请求的累计版本号</summary>
        public uint Version { get; private set; }

        /// <summary>
        /// 初始化 Value 为 -1（无请求）。
        /// </summary>
        public SwitchCharacterAttribute()
        {
            Value = -1;
        }

        #region Write

        /// <summary>
        /// 发起一次切换角色的请求。
        /// </summary>
        /// <param name="index">目标角色序号，负数忽略</param>
        public void Request(int index)
        {
            // 负数视为无效请求
            if (index < 0) return;

            Value = index;
            Version++;
        }

        #endregion

        #region Consume

        /// <summary>
        /// 按消费者游标读取一次尚未处理的切换请求。
        /// </summary>
        /// <param name="consumedVersion">调用方持有的最后已消费版本号</param>
        /// <param name="index">成功时返回目标角色序号</param>
        /// <returns>存在未消费的切换请求时返回 true</returns>
        public bool Consume(ref uint consumedVersion, out int index)
        {
            index = -1;

            // 版本号相等说明本消费者已处理过
            if (consumedVersion == Version) return false;

            consumedVersion = Version;
            index = Value;
            return true;
        }

        #endregion
    }
}
