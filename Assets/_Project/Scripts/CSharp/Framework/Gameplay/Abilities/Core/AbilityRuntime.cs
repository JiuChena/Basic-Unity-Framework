namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 提供能力运行时生命周期的空实现基类。
    /// </summary>
    public abstract class AbilityRuntime : IAbilityRuntime
    {
        // 当前能力绑定的单位运行时上下文。
        protected AbilityContext Context { get; private set; }

        /// <summary>绑定能力上下文并执行一次初始化。</summary>
        /// <param name="context">当前单位独占的能力上下文。</param>
        public virtual void Initialize(AbilityContext context)
        {
            Context = context;
        }

        /// <summary>能力所在组件启用时执行一次。</summary>
        public virtual void OnEnable() { }

        /// <summary>能力所在组件完成启用后执行一次。</summary>
        public virtual void Start() { }

        /// <summary>执行普通帧能力逻辑。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public virtual void Update(float deltaTime) { }

        /// <summary>执行固定物理帧能力逻辑。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public virtual void FixedUpdate(float fixedDeltaTime) { }

        /// <summary>执行延迟帧能力逻辑。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public virtual void LateUpdate(float deltaTime) { }

        /// <summary>能力所在组件禁用时执行一次。</summary>
        public virtual void OnDisable() { }

        /// <summary>释放能力运行时持有的组件和事件引用。</summary>
        public virtual void Dispose()
        {
            Context = null;
        }
    }
}
