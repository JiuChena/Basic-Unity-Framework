using System;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// Hitbox 作者轨道导出的运行时命中判定集合。
    /// </summary>
    [Serializable]
    public sealed class HitboxTrackData : BehaviorTrackData
    {
        // 当前轨道导出的命中判定定义集合。
        [Tooltip("行为播放期间参与命中检测的区域定义。")]
        public HitboxDef[] hitboxes = Array.Empty<HitboxDef>();

        /// <summary>
        /// 创建包含 Hitbox 轨道默认调度顺序的数据。
        /// </summary>
        public HitboxTrackData()
        {
            executionOrder = 20;
        }

        /// <summary>
        /// 获取 Hitbox 轨道的显示名称。
        /// </summary>
        /// <returns>固定的 Hitbox 轨道名称。</returns>
        public override string DisplayName => "Hitbox";

        /// <summary>
        /// 创建命中判定执行器。
        /// </summary>
        /// <param name="context">当前行为执行上下文。</param>
        /// <returns>用于更新 Hitbox 的执行器。</returns>
        public override IBehaviorTrackExecutor CreateExecutor(BehaviorExecutionContext context)
        {
            return new HitboxTrackExecutor(this, context);
        }
    }
}
