using System;
using Core.Gear;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorCore
{
    [TrackColor(0.45f, 0.85f, 0.45f)]
    [TrackClipType(typeof(BehaviorTimelineMetaClipAsset))]
    public sealed class BehaviorTimelineMetaTrack : TrackAsset { }
}
