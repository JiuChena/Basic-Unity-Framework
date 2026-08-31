using System.Collections.Generic;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 编译原生 ControlTrack 为特效或对象激活事件。
    /// </summary>
    [BehaviorTrackCompiler(typeof(ControlTrack))]
    internal sealed class VfxControlTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取原生控制轨道类型。
        /// </summary>
        /// <returns>ControlTrack 类型。</returns>
        public System.Type TrackType => typeof(ControlTrack);

        /// <summary>
        /// 导出控制轨道片段。
        /// </summary>
        /// <param name="track">待导出的控制轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not ControlTrack controlTrack || context == null)
                return;

            // ControlTrack 的预制体与层级路径规则独立，最终转换为事件语义。
            List<BehaviorEvent> events = new List<BehaviorEvent>();
            double maxEndTime = context.MaxEndTime;
            BehaviorEditorWindow.ExportNativeVfxTrack(controlTrack, context.Director, context.ReferenceRoot, events, null,
                ref maxEndTime);
            context.ConsiderEndTime(maxEndTime);
            for (int i = 0; i < events.Count; i++)
                context.AddEvent(events[i]);
        }
    }
}
