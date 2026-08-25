using Framework.ExpandComponent.DataProvider;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 保存输入监听能力写入的单位独占输入数据。
    /// </summary>
    public sealed class InputBlackboard : Blackboard
    {
        // 当前帧平面移动输入。
        public MoveInputAttribute Move { get; }
        // 当前帧跳跃按钮状态和按下边沿。
        public JumpInputAttribute Jump { get; }
        // 当前帧冲刺按钮状态。
        public SprintInputAttribute Sprint { get; }

        /// <summary>创建并注册移动、跳跃和冲刺输入属性。</summary>
        public InputBlackboard()
        {
            Move = Register(new MoveInputAttribute());
            Jump = Register(new JumpInputAttribute());
            Sprint = Register(new SprintInputAttribute());
        }

        /// <summary>将平面输入转换到指定参考系的世界空间方向。</summary>
        /// <param name="reference">移动参考 Transform；为空时使用世界坐标。</param>
        /// <returns>归一化世界空间平面方向。</returns>
        public Vector3 GetWorldMoveDirection(Transform reference)
        {
            Vector2 input = Move.Value;
            if (input.sqrMagnitude <= 0.0001f) return Vector3.zero;
            if (reference == null) return new Vector3(input.x, 0f, input.y).normalized;

            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
            return (forward * input.y + right * input.x).normalized;
        }
    }

    /// <summary>保存限制在单位圆内的平面移动输入。</summary>
    public sealed class MoveInputAttribute : BlackboardAttribute<Vector2>
    {
        // 当前平面移动值。
        private Vector2 _value;

        /// <summary>获取或设置当前平面移动输入。</summary>
        public override Vector2 Value
        {
            get => _value;
            set => _value = value.sqrMagnitude > 1f ? value.normalized : value;
        }
    }

    /// <summary>保存跳跃按钮状态和边沿事件。</summary>
    public sealed class JumpInputAttribute : ButtonAttribute { }

    /// <summary>保存冲刺按钮状态。</summary>
    public sealed class SprintInputAttribute : ButtonAttribute { }
}
