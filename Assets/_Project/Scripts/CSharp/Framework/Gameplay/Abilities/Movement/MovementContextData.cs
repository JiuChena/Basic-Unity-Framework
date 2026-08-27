namespace Framework.Gameplay.Abilities.Movement
{
    /// <summary>保存基础移动能力向其他能力公开的当前帧状态。</summary>
    public sealed class MovementContextData : IAbilityContextData
    {
        // 当前固定帧运动状态。
        public UnitMovementState CurrentState { get; private set; }
        // 当前固定帧移动命令。
        public UnitMovementCommand CurrentCommand { get; private set; }

        /// <summary>更新当前固定帧运动状态和命令。</summary>
        /// <param name="state">当前地面、速度和模式状态。</param>
        /// <param name="command">当前固定帧移动命令。</param>
        public void Write(UnitMovementState state, UnitMovementCommand command)
        {
            CurrentState = state;
            CurrentCommand = command;
        }

        /// <summary>清空当前运动状态和命令。</summary>
        public void Reset()
        {
            CurrentState = default;
            CurrentCommand = UnitMovementCommand.CreateDefault();
        }
    }
}
