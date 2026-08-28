namespace Framework.Gameplay.Abilities
{
    /// <summary>定义可注册到能力上下文的数据契约。</summary>
    public interface IAbilitySubContext
    {
        /// <summary>清空数据对象持有的运行时状态。</summary>
        void Reset();
    }
}
