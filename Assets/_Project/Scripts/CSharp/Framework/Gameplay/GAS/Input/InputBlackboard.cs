using UnityEngine;

namespace Framework.Gameplay.Abilities.Input
{
    /// <summary>保存输入能力每帧采集的最小输入数据。</summary>
    public sealed class InputBlackboard : IAbilityContextData
    {
        // 当前帧平面移动输入。
        public Vector2 Move { get; private set; }
        // 当前帧跳跃按住状态。
        public bool JumpHeld { get; private set; }
        // 尚未被跳跃能力消费的按下边沿。
        private bool _jumpPressed;
        // 当前帧冲刺按住状态。
        public bool SprintHeld { get; private set; }

        /// <summary>写入一帧输入并生成跳跃按下边沿。</summary>
        /// <param name="move">平面输入，超出单位圆时归一化。</param>
        /// <param name="jumpHeld">当前帧是否按住跳跃键。</param>
        /// <param name="sprintHeld">当前帧是否按住冲刺键。</param>
        public void WriteFrame(Vector2 move, bool jumpHeld, bool sprintHeld)
        {
            Move = move.sqrMagnitude > 1f ? move.normalized : move;
            if (!JumpHeld && jumpHeld) _jumpPressed = true;
            JumpHeld = jumpHeld;
            SprintHeld = sprintHeld;
        }

        /// <summary>消费一次尚未处理的跳跃按下边沿。</summary>
        /// <returns>存在新的跳跃按下事件时返回 true。</returns>
        public bool ConsumeJumpPressed()
        {
            bool pressed = _jumpPressed;
            _jumpPressed = false;
            return pressed;
        }

        /// <summary>清空当前帧输入和待消费事件。</summary>
        public void Reset()
        {
            Move = Vector2.zero;
            JumpHeld = false;
            _jumpPressed = false;
            SprintHeld = false;
        }

        /// <summary>将平面输入转换为指定参考系下的世界方向。</summary>
        /// <param name="reference">移动参考 Transform；为空时使用世界坐标。</param>
        /// <returns>归一化世界空间平面方向。</returns>
        public Vector3 GetWorldMoveDirection(Transform reference)
        {
            if (Move.sqrMagnitude <= 0.0001f) return Vector3.zero;
            if (reference == null) return new Vector3(Move.x, 0f, Move.y).normalized;

            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
            return (forward * Move.y + right * Move.x).normalized;
        }
    }
}
