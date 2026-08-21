using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorCore
{
    [TrackColor(0.95f, 0.35f, 0.35f)]
    [TrackClipType(typeof(BehaviorTimelineHitboxClipAsset))]
    public sealed class BehaviorTimelineHitboxTrack : TrackAsset { }
}
