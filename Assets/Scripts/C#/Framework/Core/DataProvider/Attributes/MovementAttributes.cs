using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// 移动输入属性。值为二维移动输入向量，setter 自动归一化到单位圆内。
    /// </summary>
    public sealed class MoveAttribute : BlackboardAttribute<Vector2>
    {
        // 内部存储，与 Value 属性交互
        private Vector2 _value;

        /// <summary>
        /// 移动方向。setter 自动 Clamp 到单位圆。
        /// </summary>
        public override Vector2 Value
        {
            get => _value;
            set => _value = value.sqrMagnitude > 1f ? value.normalized : value;
        }
    }

    /// <summary>
    /// 视角增量属性。值为二维鼠标/摇杆视角变化量。
    /// </summary>
    public sealed class LookAttribute : BlackboardAttribute<Vector2>
    {
    }

    /// <summary>
    /// 冲刺按压属性。true 表示当前按住冲刺键。
    /// </summary>
    public sealed class SprintAttribute : BlackboardAttribute<bool>
    {
    }

    /// <summary>
    /// 瞄准按压属性。true 表示当前按住瞄准键。
    /// </summary>
    public sealed class AimAttribute : BlackboardAttribute<bool>
    {
    }
}
