using System;
using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// 移动策略抽象基类（纯 C# 类）。子类实现 Execute，从 Blackboard 读取输入，驱动 UnitMover 移动。
    /// 通过 UnitMover 的 Inspector 拖拽 .cs 脚本文件来指定具体策略类型。
    /// </summary>
    [Serializable]
    public abstract class MovementStrategy
    {
        /// <summary>
        /// 当前策略是否依赖 IDataProvider 提供的 Blackboard。
        /// </summary>
        public virtual bool RequiresDataProvider => true;

        /// <summary>
        /// 将二维输入转换为相对指定相机的世界空间移动方向。
        /// </summary>
        /// <param name="input">二维平面输入。</param>
        /// <param name="cameraTransform">已缓存的相机参考，可为 null。</param>
        /// <returns>归一化后的世界空间方向。</returns>
        protected static Vector3 GetCameraRelativeMoveDirection(Vector2 input, Transform cameraTransform)
        {
            if (input.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            Vector3 forward = Vector3.forward;
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
                if (forward.sqrMagnitude <= 0.0001f)
                    forward = Vector3.ProjectOnPlane(cameraTransform.up, Vector3.up);
            }

            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            return (right * input.x + forward * input.y).normalized;
        }

        /// <summary>
        /// 每帧由 UnitMover 调用。写入 mover 的 Move/Jump。
        /// </summary>
        /// <param name="board">共享输入数据黑板</param>
        /// <param name="mover">宿主单位移动组件</param>
        public abstract void Execute(Blackboard board, UnitMover mover);
    }
}
