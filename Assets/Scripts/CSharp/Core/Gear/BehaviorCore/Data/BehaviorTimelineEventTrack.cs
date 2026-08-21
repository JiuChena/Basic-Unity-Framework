using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorCore
{
    [TrackColor(0.95f, 0.65f, 0.25f)]
    [TrackClipType(typeof(BehaviorTimelineEventClipAsset))]
    public sealed class BehaviorTimelineEventTrack : TrackAsset { }
}
