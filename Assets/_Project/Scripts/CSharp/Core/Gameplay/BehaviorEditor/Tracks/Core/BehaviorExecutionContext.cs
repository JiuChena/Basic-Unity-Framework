using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 行为轨道执行器访问场景依赖的运行时上下文。
    /// </summary>
    public sealed class BehaviorExecutionContext
    {
        /// <summary>当前行为执行器组件。</summary>
        public BehaviorExecutor Owner { get; }
        /// <summary>行为宿主的 Animator。</summary>
        public Animator Animator => Owner.Animator;
        /// <summary>动画段播放适配器。</summary>
        public IBehaviorAnimationPlayer AnimationPlayer => Owner.AnimationPlayer;
        /// <summary>行为宿主的单位数据。</summary>
        public IBehaviorUnit OwnerData => Owner.OwnerData;
        /// <summary>行为事件的项目侧接收器。</summary>
        public IBehaviorEventReceiver Receiver => Owner.Receiver;

        /// <summary>
        /// 创建轨道执行上下文。
        /// </summary>
        /// <param name="owner">当前行为执行组件。</param>
        public BehaviorExecutionContext(BehaviorExecutor owner)
        {
            Owner = owner;
        }
    }
}
