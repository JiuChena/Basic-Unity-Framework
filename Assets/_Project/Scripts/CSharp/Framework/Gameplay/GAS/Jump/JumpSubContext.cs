namespace Framework.Gameplay.Abilities.Movement
{
    /// <summary>保存跳跃能力向其他能力公开的跳跃状态。</summary>
    public sealed class JumpSubContext : IAbilitySubContext
    {
        // 当前是否处于主动跳跃状态。
        public bool IsJumping { get; private set; }

        /// <summary>更新当前跳跃状态。</summary>
        /// <param name="isJumping">当前是否处于主动跳跃阶段。</param>
        public void Write(bool isJumping)
        {
            IsJumping = isJumping;
        }

        /// <summary>清除主动跳跃状态。</summary>
        public void Reset()
        {
            IsJumping = false;
        }
    }
}
