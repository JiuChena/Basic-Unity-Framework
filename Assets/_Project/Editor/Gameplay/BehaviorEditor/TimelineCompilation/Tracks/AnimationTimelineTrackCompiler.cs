using System.Collections.Generic;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 编译原生 AnimationTrack 为运行时动画轨道数据。
    /// </summary>
    [BehaviorTrackCompiler(typeof(AnimationTrack))]
    internal sealed class AnimationTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取原生动画轨道类型。
        /// </summary>
        /// <returns>AnimationTrack 类型。</returns>
        public System.Type TrackType => typeof(AnimationTrack);

        /// <summary>
        /// 确保默认动画轨道存在。
        /// </summary>
        /// <param name="context">当前作者期上下文。</param>
        public void Ensure(BehaviorAuthoringContext context)
        {
            if (context?.Timeline == null)
                return;

            BehaviorEditorWindow.EnsureTrack<AnimationTrack>(context.Timeline, "Behavior Animation L0", null, out _);
        }

        /// <summary>
        /// 导出原生动画片段。
        /// </summary>
        /// <param name="track">待导出的动画轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not AnimationTrack animationTrack || context == null)
                return;

            // 复用既有的动画片段解析规则，并提交到统一轨道数据。
            List<BehaviorEditorWindow.AnimationSegmentEntry> entries = new List<BehaviorEditorWindow.AnimationSegmentEntry>();
            double maxEndTime = context.MaxEndTime;
            BehaviorEditorWindow.ExportNativeAnimationTrack(animationTrack, entries, null, ref maxEndTime);
            context.ConsiderEndTime(maxEndTime);
            entries.Sort(BehaviorEditorWindow.CompareAnimationSegmentEntries);
            for (int i = 0; i < entries.Count; i++)
                context.AddAnimationSegment(entries[i].segment);
        }
    }
}
