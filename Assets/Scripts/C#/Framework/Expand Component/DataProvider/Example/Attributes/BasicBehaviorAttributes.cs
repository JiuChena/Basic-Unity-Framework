using UnityEngine;
using Framework.ExpandComponent.DataProvider;

namespace Framework.ExpandComponent.DataProvider.Example
{
    /// <summary>
    /// 平面移动输入。setter 自动归一化到单位圆内，保证移动方向均匀。
    /// </summary>
    public sealed class MoveAttribute : BlackboardAttribute<Vector2>
    {
        private Vector2 _value;

        /// <summary>
        /// 移动方向向量。写入时自动 Clamp 到单位圆。
        /// </summary>
        public override Vector2 Value
        {
            get => _value;
            set => _value = value.sqrMagnitude > 1f ? value.normalized : value;
        }
    }

    /// <summary>
    /// 视角增量。每个消费者独立消费自上次读取后的累计位移。
    /// </summary>
    public sealed class LookAttribute : Vector2DeltaAttribute { }

    /// <summary>
    /// 冲刺状态。true 表示当前按住冲刺键。
    /// </summary>
    public sealed class SprintAttribute : BlackboardAttribute<bool> { }

    /// <summary>
    /// 跳跃动作按钮。消费走 ConsumePressed，持续状态读 IsHeld。
    /// </summary>
    public sealed class JumpAttribute : ButtonAttribute { }

    /// <summary>
    /// 下蹲动作按钮。消费走 ConsumePressed，持续状态读 IsHeld。
    /// </summary>
    public sealed class CrouchAttribute : ButtonAttribute { }
}
