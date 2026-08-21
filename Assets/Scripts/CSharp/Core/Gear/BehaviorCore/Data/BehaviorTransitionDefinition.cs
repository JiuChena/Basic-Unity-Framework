using System;
using System.Collections.Generic;
using Core.Gear;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorCore
{
    /// <summary>
    /// 当前行为可切换到其他行为的时间窗与过渡参数。
    /// </summary>
    [Serializable]
    public class BehaviorTransitionDefinition
    {
        [Tooltip("作者期来源的 Timeline 轨道名。用于把 BehaviorClip 回填到 Timeline 时恢复原来的轨道分组。")]
        public string authoringTrackName;

        [Tooltip("允许切换到的目标行为 key，例如 Attack、Talent、Burst、Reload")]
        public string targetBehaviorKey;

        [Tooltip("从行为开始后的多少秒起允许进入该目标行为")]
        [Min(0f)]
        public float startTime;

        [Tooltip("允许进入该目标行为的结束时间，单位为秒")]
        [Min(0f)]
        public float endTime = 1f;

        [Tooltip("切入目标行为时覆盖首段动画的归一化过渡比例，0 表示瞬切，1 表示按当前 Animator 状态完整过渡")]
        [Range(0f, 1f)]
        public float crossFadeDuration = 0.25f;
    }
}
