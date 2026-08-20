using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 提供给命令来源、运动模式和业务桥接层读取的只读运动状态快照。
    /// </summary>
    public readonly struct UnitMovementState
    {
        /// <summary>
        /// 初始化当前物理步的运动状态快照。
        /// </summary>
        /// <param name="hasGroundContact">脚底是否命中有效支撑面，包含不可行走斜坡。</param>
        /// <param name="isGrounded">是否检测到可行走地面。</param>
        /// <param name="isStableGrounded">脚底支撑是否满足稳定规则。</param>
        /// <param name="groundNormal">当前有效支撑面的法线。</param>
        /// <param name="groundPoint">当前地面命中点。</param>
        /// <param name="groundDistance">碰撞体到地面的检测距离。</param>
        /// <param name="currentVelocity">刚体在本步开始时的速度。</param>
        /// <param name="mode">当前实际生效的运动模式。</param>
        /// <param name="isJumping">是否处于跳跃起跳后的豁免阶段。</param>
        public UnitMovementState(
            bool hasGroundContact,
            bool isGrounded,
            bool isStableGrounded,
            Vector3 groundNormal,
            Vector3 groundPoint,
            float groundDistance,
            Vector3 currentVelocity,
            MovementMode mode,
            bool isJumping)
        {
            HasGroundContact = hasGroundContact;
            IsGrounded = isGrounded;
            IsStableGrounded = isStableGrounded;
            GroundNormal = groundNormal;
            GroundPoint = groundPoint;
            GroundDistance = groundDistance;
            CurrentVelocity = currentVelocity;
            Mode = mode;
            IsJumping = isJumping;
        }

        /// <summary>脚底是否命中有效支撑面，包含超过可行走限制的斜坡。</summary>
        public bool HasGroundContact { get; }

        /// <summary>是否检测到可站立的地面。</summary>
        public bool IsGrounded { get; }

        /// <summary>脚底支撑是否足以记录安全位置并启用边缘保护。</summary>
        public bool IsStableGrounded { get; }

        /// <summary>当前有效支撑面的世界法线。</summary>
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
