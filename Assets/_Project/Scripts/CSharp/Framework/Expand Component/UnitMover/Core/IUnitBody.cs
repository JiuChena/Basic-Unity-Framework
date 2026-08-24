using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 封装 UnitMover 对刚体的唯一写入边界。
    /// </summary>
    public interface IUnitBody
    {
        /// <summary>刚体是否仍然有效。</summary>
        bool IsValid { get; }

        /// <summary>当前刚体世界位置。</summary>
        Vector3 Position { get; }

        /// <summary>当前刚体世界旋转。</summary>
        Quaternion Rotation { get; }

        /// <summary>当前刚体线速度。</summary>
        Vector3 Velocity { get; }

        /// <summary>
        /// 将本物理步经所有运动模块合并后的最终线速度写入底层刚体。
        /// </summary>
        /// <param name="velocity">已由所有运动模块合并完成的最终线速度。</param>
        void Commit(Vector3 velocity);

        /// <summary>
        /// 恢复到业务层显式记录的检查点并清空惯性。
        /// </summary>
        /// <param name="position">需要恢复到的世界位置。</param>
        /// <param name="rotation">需要恢复到的世界旋转。</param>
        void RestoreCheckpoint(Vector3 position, Quaternion rotation);

        /// <summary>
        /// 接管刚体前记录的物理设置恢复给外部系统。
        /// </summary>
        void RestoreInitialSettings();
    }
}
