using System;
using CoreFramework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorCore
{
    [Serializable]
    public sealed class BehaviorTimelineMetaClipAsset : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("行为播放完成后的包裹模式")]
        public WrapMode wrapMode = WrapMode.Once;

        [Tooltip("行为全局播放速度倍率")]
        [Min(0.01f)]
        public float speedMultiplier = 1f;

        [Tooltip("行为打断优先级")]
        public InterruptPriority priority = InterruptPriority.Normal;


        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<BehaviorTimelineNullPlayableBehaviour>.Create(graph);
        }
    }

    [TrackColor(0.45f, 0.85f, 0.45f)]
    [TrackClipType(typeof(BehaviorTimelineMetaClipAsset))]
    public sealed class BehaviorTimelineMetaTrack : TrackAsset { }
}
