using System;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 定时行为事件作者轨道导出的运行时事件集合。
    /// </summary>
    [Serializable]
    public sealed class EventTrackData : BehaviorTrackData
    {
        // 当前轨道导出的定时行为事件集合。
        [Tooltip("按触发时间排序的运行时行为事件。")]
        public BehaviorEvent[] events = Array.Empty<BehaviorEvent>();

        /// <summary>
        /// 创建包含事件轨道默认调度顺序的数据。
        /// </summary>
        public EventTrackData()
        {
            executionOrder = 10;
        }

        /// <summary>
        /// 获取事件轨道的显示名称。
        /// </summary>
        /// <returns>固定的事件轨道名称。</returns>
        public override string DisplayName => "Events";

        /// <summary>
        /// 创建定时行为事件执行器。
        /// </summary>
        /// <param name="context">当前行为执行上下文。</param>
        /// <returns>用于分发定时事件的执行器。</returns>
        public override IBehaviorTrackExecutor CreateExecutor(BehaviorExecutionContext context)
        {
            return new EventTrackExecutor(this, context);
        }
    }
}
