using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 为单次行为播放中的轨道执行器提供宿主依赖与运行环境配置。
    /// </summary>
    public sealed class BehaviorExecutionContext
    {
        /// <summary>当前行为总调度器。</summary>
        public BehaviorExecutor Executor { get; }
        /// <summary>行为宿主对象。</summary>
        public GameObject OwnerGameObject { get; }
        /// <summary>行为宿主变换。</summary>
        public Transform OwnerTransform { get; }
        /// <summary>行为宿主的 Animator。</summary>
        public Animator Animator { get; }
        /// <summary>动画片段播放适配器。</summary>
        public IBehaviorAnimationPlayer AnimationPlayer { get; }
        /// <summary>轨道共享的骨骼路径解析服务。</summary>
        public BehaviorTransformResolver TransformResolver { get; }
        /// <summary>Hitbox 查询使用的目标层过滤。</summary>
        public LayerMask TargetLayerMask { get; }
        /// <summary>单次 Hitbox 查询最大结果数。</summary>
        public int MaxOverlapResults { get; }
        /// <summary>是否输出流程与动画切段日志。</summary>
        public bool LogBehaviorFlow { get; }
        /// <summary>是否输出事件触发日志。</summary>
        public bool LogBehaviorEvents { get; }
        /// <summary>是否输出 Hitbox 命中日志。</summary>
        public bool LogHitResults { get; }

        /// <summary>
        /// 创建一次播放专用的轨道执行上下文。
        /// </summary>
        /// <param name="executor">当前行为总调度器；不得为 null。</param>
        /// <param name="animator">行为宿主 Animator；允许为 null。</param>
        /// <param name="animationPlayer">动画片段播放适配器；允许为 null。</param>
        /// <param name="targetLayerMask">Hitbox 查询目标层过滤。</param>
        /// <param name="maxOverlapResults">Hitbox 查询最大结果数；小于一时钳制为一。</param>
        /// <param name="logBehaviorFlow">是否输出流程日志。</param>
        /// <param name="logBehaviorEvents">是否输出事件日志。</param>
        /// <param name="logHitResults">是否输出 Hitbox 日志。</param>
        public BehaviorExecutionContext(BehaviorExecutor executor, Animator animator,
            IBehaviorAnimationPlayer animationPlayer, LayerMask targetLayerMask, int maxOverlapResults,
            bool logBehaviorFlow, bool logBehaviorEvents, bool logHitResults)
        {
            // 缓存轨道需要的稳定宿主引用与当前播放环境。
            Executor = executor;
            OwnerGameObject = executor != null ? executor.gameObject : null;
            OwnerTransform = executor != null ? executor.transform : null;
            Animator = animator;
            AnimationPlayer = animationPlayer;
            TargetLayerMask = targetLayerMask;
            MaxOverlapResults = Mathf.Max(1, maxOverlapResults);
            LogBehaviorFlow = logBehaviorFlow;
            LogBehaviorEvents = logBehaviorEvents;
            LogHitResults = logHitResults;
            TransformResolver = new BehaviorTransformResolver(OwnerTransform, OwnerGameObject);
        }
    }
}
