namespace BehaviorEditor
{
    /// <summary>
    /// 驱动定时行为事件分发与循环音频回收的轨道执行器。
    /// </summary>
    internal sealed class EventTrackExecutor : IBehaviorTrackExecutor
    {
        // 当前事件轨道运行时数据。
        private readonly EventTrackData data;
        // 当前行为的场景执行上下文。
        private readonly BehaviorExecutionContext context;

        /// <summary>事件轨道的执行顺序。</summary>
        public int ExecutionOrder => data.executionOrder;

        /// <summary>
        /// 创建事件轨道执行器。
        /// </summary>
        /// <param name="data">事件轨道运行时数据。</param>
        /// <param name="context">当前行为执行上下文。</param>
        public EventTrackExecutor(EventTrackData data, BehaviorExecutionContext context)
        {
            this.data = data;
            this.context = context;
        }

        /// <summary>
        /// 构建当前行为的事件时间表。
        /// </summary>
        /// <param name="firstSegmentCrossFadeOverride">事件轨道不使用的动画过渡覆盖值。</param>
        public void Begin(float firstSegmentCrossFadeOverride)
        {
            if (context?.Owner != null)
                context.Owner.BuildSortedEvents(data.events);
        }

        /// <summary>
        /// 执行当前时间之前尚未触发的事件。
        /// </summary>
        /// <param name="elapsedTime">当前行为已播放时间。</param>
        public void Tick(float elapsedTime)
        {
            if (context?.Owner != null)
                context.Owner.ExecuteDueEvents();
        }

        /// <summary>
        /// 停止事件轨道创建的全部循环音频。
        /// </summary>
        public void Stop()
        {
            if (context?.Owner != null)
                context.Owner.StopLoopingAudios();
        }
    }
}
