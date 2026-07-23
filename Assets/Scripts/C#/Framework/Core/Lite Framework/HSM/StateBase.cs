namespace CoreFramework
{
    /// <summary>
    /// HSM 状态基类。子类实现 OnEnter / OnUpdate / OnExit，由 HSM 统一调度。
    /// </summary>
    public abstract class StateBase
    {
        protected readonly HSM Hsm;

        protected StateBase(HSM hsm)
        {
            Hsm = hsm;
        }

        public abstract void OnEnter();
        public abstract void OnUpdate();
        public abstract void OnExit();
    }
}
