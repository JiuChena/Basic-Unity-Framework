using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 表示尚未写入 Unity 组件的局部胶囊形状计算结果。
    /// </summary>
    internal readonly struct FloatingCapsuleShape
    {
        /// <summary>
        /// 创建局部胶囊形状数据。
        /// </summary>
        /// <param name="center">胶囊局部中心。</param>
        /// <param name="height">胶囊局部高度。</param>
        /// <param name="radius">胶囊局部半径。</param>
        /// <param name="direction">胶囊局部轴向索引。</param>
        internal FloatingCapsuleShape(Vector3 center, float height, float radius, int direction)
        {
            Center = center;
            Height = height;
            Radius = radius;
            Direction = direction;
        }

        /// <summary>获取胶囊局部中心。</summary>
        internal Vector3 Center { get; }

        /// <summary>获取胶囊局部高度。</summary>
        internal float Height { get; }

        /// <summary>获取胶囊局部半径。</summary>
        internal float Radius { get; }

        /// <summary>获取胶囊局部轴向索引。</summary>
        internal int Direction { get; }
    }
}
