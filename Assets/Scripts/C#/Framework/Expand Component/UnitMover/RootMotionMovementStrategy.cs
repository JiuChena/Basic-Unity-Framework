using System;
using UnityEngine;
using Framework.ExpandComponent.DataProvider;
using Framework.ExpandComponent.DataProvider.Example;

namespace Framework.Core
{
    /// <summary>
    /// Root Motion 移动策略（占位）。由 Animator 动画驱动位移，输入仅控制转向。
    /// </summary>
    [Serializable]
    public class RootMotionMovementStrategy : MovementStrategy
    {
        // 此策略已经消费的跳跃按下事件版本。
        private uint _jumpPressedVersion;

        [Tooltip("转向速度，单位：度/秒")]
        public float turnSpeed = 360f;
        [Tooltip("是否允许输入打断动画")]
        public bool allowInputInterruption = false;

        public override void Execute(Blackboard board, UnitMover mover)
        {
            if (board == null || mover == null) return;

            if (!board.TryGet(out MoveAttribute move)
                || !board.TryGet(out JumpAttribute jump)) return;

            Vector2 input = move.Value;
            Vector3 moveDir = GetCameraRelativeMoveDirection(input, mover.CameraTransform);

            if (moveDir.magnitude > 0.01f)
                mover.RotateTowards(moveDir, turnSpeed);

            if (jump.ConsumePressed(ref _jumpPressedVersion, out _))
                mover.Jump();
        }
    }
}
