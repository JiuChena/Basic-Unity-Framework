// 此文件由 BehaviorEditor 新轨道脚本工具生成，可按轨道需求修改。

using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 驱动 Test 片段播放的运行时执行器。
    /// </summary>
    internal sealed class TestTrackExecutor : IBehaviorTrackExecutor
    {
        // 当前轨道的静态运行时数据。
        private readonly TestTrackData data;
        // 当前行为播放上下文。
        private readonly BehaviorExecutionContext context;

        /// <summary>获取轨道执行顺序。</summary>
        public int ExecutionOrder => data.executionOrder;

        /// <summary>
        /// 创建 Test 运行时执行器。
        /// </summary>
        /// <param name="data">当前轨道运行时数据。</param>
        /// <param name="context">当前行为播放上下文。</param>
        public TestTrackExecutor(TestTrackData data, BehaviorExecutionContext context)
        {
            this.data = data;
            this.context = context;
        }

        /// <summary>开始一次新的轨道播放。</summary>
        public void Begin()
        {
            // 在这里缓存本轨道需要的宿主组件或重置运行时状态。
        }

        /// <summary>
        /// 推进当前轨道并执行处于时间窗内的片段。
        /// </summary>
        /// <param name="elapsedTime">当前行为已播放时间，单位：秒。</param>
        public void Tick(float elapsedTime)
        {
            if (data == null || data.segments == null || context == null) return;

            // 遍历当前时间命中的片段，在这里填入该轨道的实际执行逻辑。
            for (int index = 0; index < data.segments.Count; index++)
            {
                TestTrackSegment segment = data.segments[index];
                if (segment == null || elapsedTime < segment.startTime ||
                    elapsedTime > segment.startTime + segment.duration) continue;
            }
        }

        /// <summary>停止轨道播放并清理临时状态。</summary>
        public void Stop()
        {
            // 在这里清理本轨道创建或缓存的运行时状态。
        }
    }
}
