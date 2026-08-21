using System;
using Core.Gear;
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
}
