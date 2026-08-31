using System.Collections.Generic;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 编译原生 AudioTrack 为定时行为事件。
    /// </summary>
    [BehaviorTrackCompiler(typeof(AudioTrack))]
    internal sealed class AudioTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取原生音频轨道类型。
        /// </summary>
        /// <returns>AudioTrack 类型。</returns>
        public System.Type TrackType => typeof(AudioTrack);

        /// <summary>
        /// 导出原生音频片段为 PlayAudio 事件。
        /// </summary>
        /// <param name="track">待导出的音频轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not AudioTrack audioTrack || context == null)
                return;

            // 音频作者规则单独处理，运行时仍统一落入事件语义。
            List<BehaviorEvent> events = new List<BehaviorEvent>();
            double maxEndTime = context.MaxEndTime;
            BehaviorEditorWindow.ExportNativeAudioTrack(audioTrack, context.Director, context.ReferenceRoot, events, null,
                ref maxEndTime);
            context.ConsiderEndTime(maxEndTime);
            for (int i = 0; i < events.Count; i++)
                context.AddEvent(events[i]);
        }
    }
}
