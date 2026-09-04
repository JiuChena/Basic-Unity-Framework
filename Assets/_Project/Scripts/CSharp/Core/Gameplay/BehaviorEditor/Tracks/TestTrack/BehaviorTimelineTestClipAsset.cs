// 此文件由 BehaviorEditor 新轨道脚本工具生成，可按轨道需求修改。

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// Test Timeline 片段资产。
    /// </summary>
    public sealed class BehaviorTimelineTestClipAsset : PlayableAsset, ITimelineClipAsset
    {
        // 当前片段导出的运行时参数。
        [Tooltip("当前片段导出的运行时参数。")]
        public float value;

        /// <summary>
        /// 获取当前片段支持的 Timeline 编辑能力。
        /// </summary>
        public ClipCaps clipCaps => ClipCaps.ClipIn;

        /// <summary>
        /// 创建作者期占位 Playable。
        /// </summary>
        /// <param name="graph">当前 Timeline 播放图。</param>
        /// <param name="owner">当前 Timeline 宿主对象。</param>
        /// <returns>BehaviorEditor 使用的空 Playable。</returns>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<BehaviorTimelineNullPlayableBehaviour>.Create(graph);
        }
    }
}
