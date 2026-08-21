using System;
using System.Collections.Generic;
using Core.Gear;
using UnityEngine;
using UnityEngine.Profiling;

namespace BehaviorCore
{
    public readonly struct BehaviorPlaybackStartedEvent
    {
        public readonly BehaviorInterpreter Interpreter;
        public readonly BehaviorClip Clip;

        public BehaviorPlaybackStartedEvent(BehaviorInterpreter interpreter, BehaviorClip clip)
        {
            Interpreter = interpreter;
            Clip = clip;
        }
    }
}
