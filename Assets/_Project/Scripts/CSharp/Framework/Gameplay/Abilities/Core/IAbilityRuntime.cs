namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 定义能力运行时可接收的统一生命周期入口。
    /// </summary>
    public interface IAbilityRuntime
    {
        /// <summary>初始化能力依赖和单位级运行时状态。</summary>
        void Initialize(AbilityContext context);

        /// <summary>能力所在组件启用时执行一次。</summary>
        void OnEnable();

        /// <summary>能力所在组件完成启用后执行一次。</summary>
        void Start();

        /// <summary>执行普通帧能力逻辑。</summary>
        void Update(float deltaTime);

        /// <summary>执行固定物理帧能力逻辑。</summary>
        void FixedUpdate(float fixedDeltaTime);

        /// <summary>执行延迟帧能力逻辑。</summary>
        void LateUpdate(float deltaTime);

        /// <summary>能力所在组件禁用时执行一次。</summary>
        void OnDisable();

        /// <summary>释放能力运行时持有的组件和事件引用。</summary>
        void Dispose();
    }
}
