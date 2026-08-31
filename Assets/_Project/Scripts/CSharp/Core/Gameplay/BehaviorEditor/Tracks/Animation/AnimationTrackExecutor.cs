namespace BehaviorEditor
{
    /// <summary>
    /// 驱动动画段切换与首段播放的轨道执行器。
    /// </summary>
    internal sealed class AnimationTrackExecutor : IBehaviorTrackExecutor
    {
        // 当前动画轨道运行时数据。
        private readonly AnimationTrackData data;
        // 当前行为的场景执行上下文。
        private readonly BehaviorExecutionContext context;

        /// <summary>动画轨道的执行顺序。</summary>
        public int ExecutionOrder => data.executionOrder;

        /// <summary>
        /// 创建动画轨道执行器。
        /// </summary>
        /// <param name="data">动画轨道运行时数据。</param>
        /// <param name="context">当前行为执行上下文。</param>
        public AnimationTrackExecutor(AnimationTrackData data, BehaviorExecutionContext context)
        {
            this.data = data;
            this.context = context;
        }

        /// <summary>
        /// 构建动画段时间表并播放首段。
        /// </summary>
        /// <param name="firstSegmentCrossFadeOverride">首段动画过渡覆盖值。</param>
        public void Begin(float firstSegmentCrossFadeOverride)
        {
            if (context?.Owner == null)
                return;

            // 直接使用当前轨道导出的片段数据构建时间表。
            context.Owner.BuildSegments(data.segments);
            if (context.Animator != null)
                context.Owner.PlaySegment(data.segments, 0, firstSegmentCrossFadeOverride);
        }

        /// <summary>
        /// 根据经过时间推进到后续动画段。
        /// </summary>
        /// <param name="elapsedTime">当前行为已播放时间。</param>
        public void Tick(float elapsedTime)
        {
            if (context?.Owner != null)
                context.Owner.UpdateAnimationSegments();
        }

        /// <summary>
        /// 动画轨道不持有额外临时状态。
        /// </summary>
        public void Stop() { }
    }
}
