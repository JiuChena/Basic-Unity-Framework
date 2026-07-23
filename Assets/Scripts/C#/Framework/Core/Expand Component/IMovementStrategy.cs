using UnityEngine;

namespace CoreFramework
{
    /// <summary>
    /// 移动策略接口。读取 Blackboard 的 MoveInput，驱动 CharacterController 移动。
    /// 玩家和敌人共用此接口，切换移动方式只需替换实现。
    /// </summary>
    public interface IMovementStrategy
    {
        /// <summary>
        /// 每帧执行移动逻辑。
        /// </summary>
        /// <param name="board">共享输入黑板</param>
        /// <param name="cc">宿主单位的 CharacterController</param>
        void Execute(Blackboard board, CharacterController cc);
    }
}
