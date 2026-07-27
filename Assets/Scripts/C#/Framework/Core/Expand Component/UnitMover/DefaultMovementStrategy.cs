using System;
using UnityEngine;

namespace Framework.Core
{
    [Serializable]
    public class DefaultMovementStrategy : MovementStrategy
    {
        // 此策略已经消费的跳跃按下事件版本。
        private uint _jumpPressedVersion;

        [Tooltip("是否根据移动方向旋转角色朝向。相机跟随角色朝向时请保持关闭，避免 WASD 带动摄像机旋转。")]
        public bool rotateTowardsMovement = false;
        [Tooltip("旋转速度，单位：度/秒")]
        public float rotationSpeed = 720f;

        public override void Execute(Blackboard board, UnitMover mover)
        {
            if (board == null || mover == null) return;

            if (!board.TryGet(out MoveAttribute move)
                || !board.TryGet(out SprintAttribute sprint)
                || !board.TryGet(out JumpAttribute jump)) return;

            Vector2 input = move.Value;
            Vector3 moveDir = GetCameraRelativeMoveDirection(input, mover.CameraTransform);

            float speedMult = sprint.Value && input.y > 0.1f ? mover.sprintMultiplier : 1f;

            if (moveDir.magnitude > 0.01f)
            {
                mover.Move(moveDir, speedMult);

                if (rotateTowardsMovement)
                    mover.RotateTowards(moveDir, rotationSpeed);
            }

            if (jump.Value.ConsumePressed(ref _jumpPressedVersion))
                mover.Jump();
        }
    }
}
