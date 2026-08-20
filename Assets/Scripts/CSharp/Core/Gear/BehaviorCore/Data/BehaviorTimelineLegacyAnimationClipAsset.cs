using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorCore
{
    /// <summary>
    /// 旧版 Behavior Timeline 动画片段兼容类型。
    /// 仅用于恢复历史 .playable 资源的反序列化与导出，不再作为新作者流程入口。
    /// </summary>
    [Serializable]
    public sealed class BehaviorTimelineAnimationClipAsset : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("旧版作者轨中绑定的动画片段。")]
        public AnimationClip animationClip;

        [Tooltip("旧版运行时使用的动画层级。当前仅在导出时继续参与数据编译。")]
        public int layer;

        [Tooltip("旧版片段记录的切换时长。当前仅在导出时继续参与数据编译。")]
        [Min(0f)]
        public float crossFadeDuration = 0.15f;

        public ClipCaps clipCaps => ClipCaps.ClipIn | ClipCaps.SpeedMultiplier | ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            if (animationClip == null)
                return Playable.Null;

            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, animationClip);
            playable.SetApplyFootIK(false);
            return playable;
        }
    }
}
