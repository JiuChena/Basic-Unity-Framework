using System;
using Framework.Core;
using UnityEngine;

namespace BehaviorCore
{
    [Serializable]
    public enum BehaviorAuthoringTrackKind
    {
        Meta,
        Animation,
        Audio,
        VfxControl,
        VfxActivation,
        Event,
        Hitbox,
        Transition,
    }

    [Serializable]
    public sealed class BehaviorTimelineMetaSnapshot
    {
        public WrapMode wrapMode = WrapMode.Once;
        public float speedMultiplier = 1f;
        public InterruptPriority priority = InterruptPriority.Normal;
    }

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
        public BehaviorTransitionDefinition transitionDefinition;
    }

    [Serializable]
    public sealed class BehaviorAuthoringTrackSnapshot
    {
        public string trackName;
        public BehaviorAuthoringTrackKind trackKind;
        public int sortIndex;
        public BehaviorAuthoringClipSnapshot[] clips = Array.Empty<BehaviorAuthoringClipSnapshot>();
    }
}
