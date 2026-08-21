using System;
using Core.Gear;
using UnityEngine;

namespace BehaviorCore
{
    [Serializable]
    public sealed class BehaviorAuthoringTrackSnapshot
    {
        public string trackName;
        public BehaviorAuthoringTrackKind trackKind;
        public int sortIndex;
        public BehaviorAuthoringClipSnapshot[] clips = Array.Empty<BehaviorAuthoringClipSnapshot>();
    }
}
