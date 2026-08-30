using System;
using Core.Gear;
using UnityEngine;

namespace BehaviorCore
{
    [Serializable]
    public sealed class BehaviorAuthoringClipSnapshot
    {
        public string displayName;
        public float startTime;
        public float duration;
        public string boundObjectPath;
        public int controlPostPlayback = -1;
        public BehaviorTimelineMetaSnapshot meta;
        public AnimationSegment animationSegment;
        public BehaviorEvent behaviorEvent;
        public HitboxDef hitboxDef;
    }
}
