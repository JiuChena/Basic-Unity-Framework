using System;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 动画作者轨道导出的运行时片段集合。
    /// </summary>
    [Serializable]
    public sealed class AnimationTrackData : BehaviorTrackData
    {
        // 当前轨道导出的动画片段集合。
        [Tooltip("按时间顺序播放的动画片段。")]
        public AnimationSegment[] segments = Array.Empty<AnimationSegment>();

        /// <summary>
        /// 创建包含动画轨道默认调度顺序的数据。
        /// </summary>
        public AnimationTrackData()
        {
            executionOrder = 0;
        }

        /// <summary>
        /// 获取动画轨道的显示名称。
        /// </summary>
        /// <returns>固定的动画轨道名称。</returns>
        public override string DisplayName => "Animation";

        /// <summary>
        /// 创建动画片段执行器。
        /// </summary>
        /// <param name="context">当前行为执行上下文。</param>
        /// <returns>用于播放动画片段的执行器。</returns>
        public override IBehaviorTrackExecutor CreateExecutor(BehaviorExecutionContext context)
        {
            return new AnimationTrackExecutor(this, context);
        }
    }
}
