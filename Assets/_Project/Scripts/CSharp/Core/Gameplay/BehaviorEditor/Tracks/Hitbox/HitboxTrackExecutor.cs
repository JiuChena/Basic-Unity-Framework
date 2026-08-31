namespace BehaviorEditor
{
    /// <summary>
    /// 驱动 Hitbox 时间窗与物理命中查询的轨道执行器。
    /// </summary>
    internal sealed class HitboxTrackExecutor : IBehaviorTrackExecutor
    {
        // 当前 Hitbox 轨道运行时数据。
        private readonly HitboxTrackData data;
        // 当前行为的场景执行上下文。
        private readonly BehaviorExecutionContext context;

        /// <summary>Hitbox 轨道的执行顺序。</summary>
        public int ExecutionOrder => data.executionOrder;

        /// <summary>
        /// 创建 Hitbox 轨道执行器。
        /// </summary>
        /// <param name="data">Hitbox 轨道运行时数据。</param>
        /// <param name="context">当前行为执行上下文。</param>
        public HitboxTrackExecutor(HitboxTrackData data, BehaviorExecutionContext context)
        {
            this.data = data;
            this.context = context;
        }

        /// <summary>
        /// 预解析当前行为全部 Hitbox 的骨骼引用。
        /// </summary>
        /// <param name="firstSegmentCrossFadeOverride">Hitbox 轨道不使用的动画过渡覆盖值。</param>
        public void Begin(float firstSegmentCrossFadeOverride)
        {
            if (context?.Owner != null)
                context.Owner.BuildHitboxes(data.hitboxes);
        }

        /// <summary>
        /// 更新当前时间窗内的命中判定。
        /// </summary>
        /// <param name="elapsedTime">当前行为已播放时间。</param>
        public void Tick(float elapsedTime)
        {
            if (context?.Owner != null)
                context.Owner.UpdateHitboxes();
        }

        /// <summary>
        /// Hitbox 临时状态由 BehaviorExecutor 在停止时统一清空。
        /// </summary>
        public void Stop() { }
    }
}
