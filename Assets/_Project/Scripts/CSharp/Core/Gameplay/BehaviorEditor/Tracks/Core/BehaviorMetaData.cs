using System;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 行为播放头信息的运行时数据。
    /// </summary>
    [Serializable]
    public sealed class BehaviorMetaData : BehaviorTrackData
    {
        // 行为在未缩放时间轴中的总时长。
        [Tooltip("行为在未缩放时间轴中的总时长，单位：秒。")]
        [Min(0.01f)]
        public float duration = 1f;

        // 行为播放结束后的包裹模式。
        [Tooltip("行为播放完成后的包裹模式。")]
        public WrapMode wrapMode = WrapMode.Once;

        // 行为全局播放速度倍率。
        [Tooltip("行为全局播放速度倍率。")]
        [Min(0.01f)]
        public float speedMultiplier = 1f;

        /// <summary>
        /// 创建包含 Meta 轨道默认调度顺序的数据。
        /// </summary>
        public BehaviorMetaData()
        {
            executionOrder = -100;
        }

        /// <summary>
        /// 获取 Meta 轨道的显示名称。
        /// </summary>
        /// <returns>固定的 Meta 轨道名称。</returns>
        public override string DisplayName => "Meta";

        /// <summary>
        /// 创建 Meta 轨道执行器。
        /// </summary>
        /// <param name="context">当前行为执行上下文。</param>
        /// <returns>不持有逐帧逻辑的 Meta 执行器。</returns>
        public override IBehaviorTrackExecutor CreateExecutor(BehaviorExecutionContext context)
        {
            return new MetaTrackExecutor(executionOrder);
        }
    }
}
