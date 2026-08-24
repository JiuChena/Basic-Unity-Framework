using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorCore
{
    [TrackColor(0.85f, 0.45f, 0.95f)]
    [TrackClipType(typeof(BehaviorTimelineTransitionClipAsset))]
    public sealed class BehaviorTimelineTransitionTrack : TrackAsset { }
}
