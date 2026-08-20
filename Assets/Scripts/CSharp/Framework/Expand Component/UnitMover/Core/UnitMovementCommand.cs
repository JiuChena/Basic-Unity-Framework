using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 由命令来源提交给运动执行器的无业务移动意图。
    /// </summary>
    public struct UnitMovementCommand
    {
        // 世界空间中的期望移动方向，不要求已经投影到地面。
        public Vector3 WorldMoveDirection;
        // 通用速度倍率，由冲刺、减速区或技能效果等外部系统提供。
        public float SpeedScale;
        // 请求执行一次普通跳跃，不代表本物理步一定允许跳跃。
        public bool RequestJump;
        // 表示跳跃输入仍在按住，用于可选的跳跃截断规则。
        public bool IsJumpHeld;

        /// <summary>
        /// 创建不移动、不跳跃且速度倍率为一的默认命令。
        /// </summary>
        /// <returns>可安全参与本物理步合并的空命令。</returns>
        public static UnitMovementCommand CreateDefault()
        {
            return new UnitMovementCommand
            {
                SpeedScale = 1f
            };
        }
    }
}
