namespace CoreFramework
{
    /// <summary>
    /// 为控制器提供输入快照的统一协议。
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>
        /// 由当前输入源拥有并写入的共享数据黑板。
        /// </summary>
        Blackboard Board { get; }

        /// <summary>
        /// 采集当前输入源状态并写入自身黑板。
        /// </summary>
        void Tick();
    }
}
