using System.Collections.Generic;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 编译原生 ActivationTrack 为对象激活事件。
    /// </summary>
    [BehaviorTrackCompiler(typeof(ActivationTrack))]
    internal sealed class ActivationTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取原生激活轨道类型。
        /// </summary>
        /// <returns>ActivationTrack 类型。</returns>
        public System.Type TrackType => typeof(ActivationTrack);

        /// <summary>
        /// 导出激活轨道片段。
        /// </summary>
        /// <param name="track">待导出的激活轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not ActivationTrack activationTrack || context == null)
                return;

            // 激活轨道转换为进入和退出两个定时事件。
            List<BehaviorEvent> events = new List<BehaviorEvent>();
            double maxEndTime = context.MaxEndTime;
            BehaviorEditorWindow.ExportNativeActivationTrack(activationTrack, context.Director, context.ReferenceRoot,
                events, null, ref maxEndTime);
            context.ConsiderEndTime(maxEndTime);
            for (int i = 0; i < events.Count; i++)
                context.AddEvent(events[i]);
        }
    }
}
