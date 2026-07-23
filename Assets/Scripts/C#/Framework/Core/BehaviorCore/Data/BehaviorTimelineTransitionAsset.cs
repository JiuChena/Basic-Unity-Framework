using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorCore
{
    [Serializable]
    public sealed class BehaviorTimelineTransitionClipAsset : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("该时间片段导出为当前行为切入其他行为的允许时间窗")]
        public BehaviorTransitionDefinition transitionData = new BehaviorTransitionDefinition();

        public ClipCaps clipCaps => ClipCaps.ClipIn;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<BehaviorTimelineNullPlayableBehaviour>.Create(graph);
        }
    }

    [TrackColor(0.85f, 0.45f, 0.95f)]
    [TrackClipType(typeof(BehaviorTimelineTransitionClipAsset))]
    public sealed class BehaviorTimelineTransitionTrack : TrackAsset { }
}
