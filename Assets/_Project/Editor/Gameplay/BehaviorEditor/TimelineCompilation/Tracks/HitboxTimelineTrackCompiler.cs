using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 编译自定义 Hitbox 轨道为运行时命中判定数据。
    /// </summary>
    [BehaviorTrackCompiler(typeof(BehaviorTimelineHitboxTrack))]
    internal sealed class HitboxTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取自定义 Hitbox 轨道类型。
        /// </summary>
        /// <returns>BehaviorTimelineHitboxTrack 类型。</returns>
        public System.Type TrackType => typeof(BehaviorTimelineHitboxTrack);

        /// <summary>
        /// 导出全部 Hitbox 片段为带有时间窗的独立数据。
        /// </summary>
        /// <param name="track">待导出的 Hitbox 轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not BehaviorTimelineHitboxTrack hitboxTrack || context == null)
                return;

            // 以 Timeline 的起止时间覆盖作者期定义，形成独立运行时命中窗口。
            foreach (TimelineClip clip in hitboxTrack.GetClips())
            {
                if (clip?.asset is not BehaviorTimelineHitboxClipAsset asset)
                    continue;

                context.ConsiderEndTime(clip.end);
                context.AddHitbox(BehaviorEditorWindow.CloneHitboxDef(asset.hitboxData, (float)clip.start,
                    (float)clip.duration, hitboxTrack.name));
            }
        }
    }
}
