using UnityEngine;

namespace BehaviorCore
{
    /// <summary>
    /// 行为解释器使用的动画播放抽象层。
    /// </summary>
    public interface IBehaviorAnimationPlayer
    {
        bool Initialize(Animator animator);
        bool TryPlaySegment(AnimationSegment segment, int slotIndex, float crossFadeDurationOverride, out string stateName);
    }
}
