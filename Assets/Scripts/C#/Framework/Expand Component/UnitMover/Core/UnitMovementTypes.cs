using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 标识当前由哪一种通用运动规则解释移动命令。
    /// </summary>
    public enum MovementMode
    {
        Ground,
        Air,
        Swimming,
        Flying
    }

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

    /// <summary>
    /// 提供给命令来源、运动模式和业务桥接层读取的只读运动状态快照。
    /// </summary>
    public readonly struct UnitMovementState
    {
        /// <summary>
        /// 初始化当前物理步的运动状态快照。
        /// </summary>
        /// <param name="isGrounded">是否检测到可行走地面。</param>
        /// <param name="isStableGrounded">脚底支撑是否满足稳定规则。</param>
        /// <param name="groundNormal">当前可行走地面的法线。</param>
        /// <param name="groundPoint">当前地面命中点。</param>
        /// <param name="groundDistance">碰撞体到地面的检测距离。</param>
        /// <param name="currentVelocity">刚体在本步开始时的速度。</param>
        /// <param name="mode">当前实际生效的运动模式。</param>
        /// <param name="isJumping">是否处于跳跃起跳后的豁免阶段。</param>
        public UnitMovementState(
            bool isGrounded,
            bool isStableGrounded,
            Vector3 groundNormal,
            Vector3 groundPoint,
            float groundDistance,
            Vector3 currentVelocity,
            MovementMode mode,
            bool isJumping)
        {
            IsGrounded = isGrounded;
            IsStableGrounded = isStableGrounded;
            GroundNormal = groundNormal;
            GroundPoint = groundPoint;
            GroundDistance = groundDistance;
            CurrentVelocity = currentVelocity;
            Mode = mode;
            IsJumping = isJumping;
        }

        /// <summary>是否检测到可站立的地面。</summary>
        public bool IsGrounded { get; }

        /// <summary>脚底支撑是否足以记录安全位置并启用边缘保护。</summary>
        public bool IsStableGrounded { get; }

        /// <summary>当前可行走地面的世界法线。</summary>
        public Vector3 GroundNormal { get; }

        /// <summary>当前地面检测的世界命中点。</summary>
        public Vector3 GroundPoint { get; }

        /// <summary>当前碰撞体与地面之间的距离。</summary>
        public float GroundDistance { get; }

        /// <summary>本物理步开始时读取的刚体速度。</summary>
        public Vector3 CurrentVelocity { get; }

        /// <summary>当前参与速度计算的运动模式。</summary>
        public MovementMode Mode { get; }

        /// <summary>是否刚执行跳跃且仍应忽略短暂地面接触。</summary>
        public bool IsJumping { get; }
    }

}
