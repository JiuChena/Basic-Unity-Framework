using Framework.ExpandComponent.UnitMover;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 配置并执行浮动胶囊及脚底辅助碰撞体能力。
    /// </summary>
    [CreateAssetMenu(fileName = "FloatingCapsuleAbility", menuName = "Framework/Gameplay/Abilities/Movement/Floating Capsule")]
    public sealed class FloatingCapsuleAbilitySO : AbilityDefinitionSO
    {
        // 浮动胶囊静态参数；运行时会复制为单位独占实例。
        [Header("浮动胶囊")]
        [Tooltip("浮动胶囊的启用状态、底部留空高度和脚底 BoxCollider 参数")]
        [SerializeField] private FloatingCapsuleModule _configuration = new FloatingCapsuleModule();

        /// <summary>根据配置创建浮动胶囊能力运行时。</summary>
        /// <param name="context">能力所属单位上下文。</param>
        /// <returns>单位独占浮动胶囊运行时。</returns>
        public override AbilityRuntime CreateRuntime(AbilityContext context)
        {
            return new FloatingCapsuleAbilityRuntime(_configuration != null
                ? _configuration.CreateRuntimeCopy()
                : new FloatingCapsuleModule());
        }
    }

    /// <summary>
    /// 在能力生命周期中同步浮动胶囊形状并维护脚底辅助碰撞体。
    /// </summary>
    public sealed class FloatingCapsuleAbilityRuntime : AbilityRuntime
    {
        // 当前单位独占的浮动胶囊配置。
        private readonly FloatingCapsuleModule _configuration;
        // 当前单位主胶囊对应的形状同步模块。
        private ColliderShapeModule _shapeModule;

        /// <summary>创建浮动胶囊能力运行时。</summary>
        /// <param name="configuration">单位独占浮动胶囊配置。</param>
        public FloatingCapsuleAbilityRuntime(FloatingCapsuleModule configuration)
        {
            _configuration = configuration;
        }

        /// <summary>创建形状模块并注册为单位共享服务。</summary>
        /// <param name="context">能力所属单位上下文。</param>
        public override void Initialize(AbilityContext context)
        {
            base.Initialize(context);
            if (context == null || context.MovementCollider == null) return;

            _shapeModule = new ColliderShapeModule(
                context.MovementCollider,
                context.Owner,
                _configuration);
            context.RegisterService(_shapeModule);
        }

        /// <summary>启用时立即同步浮动胶囊形状。</summary>
        public override void OnEnable()
        {
            _shapeModule?.Synchronize();
        }

        /// <summary>固定帧持续同步形状，响应配置和外部胶囊修改。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public override void FixedUpdate(float fixedDeltaTime)
        {
            _shapeModule?.Synchronize();
        }

        /// <summary>禁用时恢复基础胶囊并清理本能力维护的脚底碰撞体。</summary>
        public override void OnDisable()
        {
            _shapeModule?.RestoreAuthoringShape();
        }

        /// <summary>释放形状模块并解除共享服务注册。</summary>
        public override void Dispose()
        {
            _shapeModule?.RestoreAuthoringShape();
            if (Context != null && ReferenceEquals(Context.GetService<ColliderShapeModule>(), _shapeModule))
                Context.RegisterService<ColliderShapeModule>(null);
            _shapeModule = null;
            base.Dispose();
        }
    }
}
