using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 编译 Behavior Meta 轨道为行为播放头数据。
    /// </summary>
    [BehaviorTrackCompiler(typeof(BehaviorTimelineMetaTrack))]
    internal sealed class MetaTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取 Meta 轨道类型。
        /// </summary>
        /// <returns>BehaviorTimelineMetaTrack 类型。</returns>
        public System.Type TrackType => typeof(BehaviorTimelineMetaTrack);

        /// <summary>
        /// 确保 Timeline 存在包含默认片段的 Meta 轨道。
        /// </summary>
        /// <param name="context">当前作者期上下文。</param>
        public void Ensure(BehaviorAuthoringContext context)
        {
            if (context?.Timeline == null)
                return;

            BehaviorTimelineMetaTrack track = BehaviorEditorWindow.EnsureTrack<BehaviorTimelineMetaTrack>(
                context.Timeline, "Behavior Meta", null, out _);
            if (track == null)
                return;

            // 已有 Meta 片段时保留用户作者配置。
            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip?.asset is BehaviorTimelineMetaClipAsset)
                    return;
            }

            // 空轨道首次创建时补齐可直接编辑的默认 Meta 片段。
            TimelineClip defaultClip = track.CreateDefaultClip();
            defaultClip.displayName = "Behavior Meta";
            defaultClip.start = 0d;
            defaultClip.duration = 0.1d;
            EditorUtility.SetDirty(track);
        }

        /// <summary>
        /// 导出首个 Meta 片段作为行为播放头配置。
        /// </summary>
        /// <param name="track">待导出的 Meta 轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not BehaviorTimelineMetaTrack metaTrack || context == null)
                return;

            BehaviorMetaData meta = context.GetMetaData();
            bool assigned = false;
            foreach (TimelineClip clip in metaTrack.GetClips())
            {
                if (clip?.asset is not BehaviorTimelineMetaClipAsset asset)
                    continue;

                context.ConsiderEndTime(clip.end);
                if (assigned)
                {
                    context.AddWarning($"Meta 轨道 '{metaTrack.name}' 包含多个片段，仅导出第一个片段。");
                    continue;
                }

                meta.wrapMode = asset.wrapMode;
                meta.speedMultiplier = Mathf.Max(0.01f, asset.speedMultiplier);
                meta.priority = asset.priority;
                assigned = true;
            }
        }
    }
}
