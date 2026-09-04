// 此文件由 BehaviorEditor 新轨道脚本工具生成，可按轨道需求修改。

using UnityEngine;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 将 Test Timeline 轨道导出为运行时数据。
    /// </summary>
    [BehaviorTrackCompiler(typeof(BehaviorTimelineTestTrack))]
    internal sealed class TestTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>获取当前编译器支持的 Timeline 轨道类型。</summary>
        public System.Type TrackType => typeof(BehaviorTimelineTestTrack);

        /// <summary>导出当前轨道的全部有效片段。</summary>
        /// <param name="track">待导出的 Timeline 轨道。</param>
        /// <param name="context">当前 Timeline 导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not BehaviorTimelineTestTrack sourceTrack || context == null) return;

            TestTrackData data = context.GetOrCreateTrackData<TestTrackData>();
            foreach (TimelineClip clip in sourceTrack.GetClips())
            {
                if (clip?.asset is not BehaviorTimelineTestClipAsset asset) continue;

                // Timeline 时间信息由 Clip 提供，运行时数据只保存导出结果。
                context.ConsiderEndTime(clip.end);
                data.segments.Add(new TestTrackSegment
                {
                    startTime = Mathf.Max(0f, (float)clip.start),
                    duration = Mathf.Max(0f, (float)clip.duration),
                    value = asset.value
                });
            }
        }
    }
}
