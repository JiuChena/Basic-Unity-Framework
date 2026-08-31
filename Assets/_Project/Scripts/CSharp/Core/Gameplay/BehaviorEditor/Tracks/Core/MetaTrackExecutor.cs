namespace BehaviorEditor
{
    /// <summary>
    /// Meta 轨道的空执行器，播放头配置已由 BehaviorExecutor 在开始时应用。
    /// </summary>
    internal sealed class MetaTrackExecutor : IBehaviorTrackExecutor
    {
        /// <summary>当前 Meta 轨道执行顺序。</summary>
        public int ExecutionOrder { get; }

        /// <summary>
        /// 创建 Meta 轨道执行器。
        /// </summary>
        /// <param name="executionOrder">调度顺序。</param>
        public MetaTrackExecutor(int executionOrder)
        {
            ExecutionOrder = executionOrder;
        }

        /// <summary>
        /// Meta 数据不需要开始阶段执行。
        /// </summary>
        /// <param name="firstSegmentCrossFadeOverride">未使用的动画过渡覆盖值。</param>
        public void Begin(float firstSegmentCrossFadeOverride) { }

        /// <summary>
        /// Meta 数据不需要逐帧执行。
        /// </summary>
        /// <param name="elapsedTime">当前行为播放时间。</param>
        public void Tick(float elapsedTime) { }

        /// <summary>
        /// Meta 数据不持有临时运行时状态。
        /// </summary>
        public void Stop() { }
    }
}
