using System;
using UnityEngine;
using Framework.ExpandComponent.DataProvider;
using Framework.ExpandComponent.DataProvider.Example;

namespace Framework.Core
{
    /// <summary>
    /// 平滑目标速度策略。最终仍通过 UnitMover 的 Rigidbody 力驱动执行移动。
    /// </summary>
    [Serializable]
    public class KinematicMovementStrategy : MovementStrategy
    {
        // 此策略已经消费的跳跃按下事件版本。
        private uint _jumpPressedVersion;

        [Tooltip("加速度，单位：米/秒²")]
        public float acceleration = 30f;
        [Tooltip("减速阻尼")]
        public float damping = 10f;

        private Vector3 _currentVelocity;

        public override void Execute(Blackboard board, UnitMover mover)
        {
            if (board == null || mover == null) return;

            if (!board.TryGet(out MoveAttribute move)
                || !board.TryGet(out SprintAttribute sprint)
                || !board.TryGet(out JumpAttribute jump)) return;

            Vector2 input = move.Value;
            Vector3 targetDir = GetCameraRelativeMoveDirection(input, mover.CameraTransform);

            float speedMult = sprint.Value && input.y > 0.1f ? mover.sprintMultiplier : 1f;
            float targetSpeed = mover.moveSpeed * speedMult;

            if (targetDir.magnitude > 0.01f)
            {
                Vector3 targetVelocity = targetDir * targetSpeed;
                _currentVelocity = Vector3.MoveTowards(_currentVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
            }
            else
            {
                _currentVelocity = Vector3.MoveTowards(_currentVelocity, Vector3.zero, damping * Time.fixedDeltaTime);
            }

            float speedMultiplier = mover.moveSpeed > 0.001f ? _currentVelocity.magnitude / mover.moveSpeed : 0f;
            mover.Move(_currentVelocity.normalized, speedMultiplier);

            if (jump.ConsumePressed(ref _jumpPressedVersion, out _))
                mover.Jump();
        }
    }
}
