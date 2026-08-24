using System;
using Core.Gear;
using UnityEngine;

namespace BehaviorCore
{
    [Serializable]
    public sealed class BehaviorTimelineMetaSnapshot
    {
        public WrapMode wrapMode = WrapMode.Once;
        public float speedMultiplier = 1f;
        public InterruptPriority priority = InterruptPriority.Normal;
    }
}
