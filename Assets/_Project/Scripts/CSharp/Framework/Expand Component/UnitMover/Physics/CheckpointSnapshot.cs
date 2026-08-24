using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 保存由业务层显式记录并可手动恢复的检查点位置快照。
    /// </summary>
    public readonly struct CheckpointSnapshot
    {
        /// <summary>
        /// 创建检查点位置快照。
        /// </summary>
        /// <param name="position">业务层记录的刚体位置。</param>
        /// <param name="rotation">业务层记录的刚体旋转。</param>
        public CheckpointSnapshot(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        /// <summary>检查点的世界坐标。</summary>
        public Vector3 Position { get; }

        /// <summary>检查点的世界旋转。</summary>
        public Quaternion Rotation { get; }
    }
}
