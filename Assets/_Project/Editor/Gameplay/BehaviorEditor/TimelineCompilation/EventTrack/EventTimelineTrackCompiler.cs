using UnityEngine;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 编译自定义事件轨道为运行时行为事件。
    /// </summary>
    [BehaviorTrackCompiler(typeof(BehaviorTimelineEventTrack))]
    internal sealed class EventTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取自定义事件轨道类型。
        /// </summary>
        /// <returns>BehaviorTimelineEventTrack 类型。</returns>
        public System.Type TrackType => typeof(BehaviorTimelineEventTrack);

        /// <summary>
        /// 导出全部有效的自定义行为事件。
        /// </summary>
        /// <param name="track">待导出的事件轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not BehaviorTimelineEventTrack eventTrack || context == null)
                return;

            // 每个 Timeline 片段独立构建运行时事件，作者资产不参与运行时修改。
            foreach (TimelineClip clip in eventTrack.GetClips())
            {
                if (clip?.asset is not BehaviorTimelineEventClipAsset asset)
                    continue;

                context.ConsiderEndTime(clip.end);
                if (asset.eventData?.execute == null)
                {
                    context.AddWarning($"Behavior Events 轨道中的片段 '{clip.displayName}' 未配置执行资产，已跳过。");
                    continue;
                }

                context.AddEvent(new BehaviorEvent
                {
                    authoringTrackName = eventTrack.name,
                    time = Mathf.Max(0f, (float)clip.start),
                    referenceBone = asset.eventData.referenceBone,
                    positionOffset = asset.eventData.positionOffset,
                    rotationOffset = asset.eventData.rotationOffset,
                    scaleOffset = asset.eventData.scaleOffset,
                    execute = asset.eventData.execute
                });
            }
        }
    }
}
