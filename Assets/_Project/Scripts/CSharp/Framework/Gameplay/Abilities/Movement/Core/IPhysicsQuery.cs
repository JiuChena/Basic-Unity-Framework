using UnityEngine;

namespace Framework.Gameplay.Abilities.Movement
{
    /// <summary>
    /// 隔离 Physics 静态查询，使物理模块不直接依赖场景查找和 Unity 生命周期。
    /// </summary>
    public interface IPhysicsQuery
    {
        /// <summary>
        /// 执行无分配射线检测。
        /// </summary>
        /// <param name="origin">射线起点。</param>
        /// <param name="direction">射线方向。</param>
        /// <param name="distance">最大检测距离。</param>
        /// <param name="layerMask">参与检测的物理层。</param>
        /// <param name="results">复用的命中结果缓冲区。</param>
        /// <returns>写入缓冲区的命中数量。</returns>
        int RaycastNonAlloc(Vector3 origin, Vector3 direction, float distance, int layerMask, RaycastHit[] results);

        /// <summary>
        /// 执行无分配球体检测，用于确认可跨越窄缝后的脚底支撑。
        /// </summary>
        /// <param name="origin">球体检测起点。</param>
        /// <param name="radius">球体半径。</param>
        /// <param name="direction">检测方向。</param>
        /// <param name="distance">最大检测距离。</param>
        /// <param name="layerMask">参与检测的物理层。</param>
        /// <param name="results">复用的命中结果缓冲区。</param>
        /// <returns>写入缓冲区的命中数量。</returns>
        int SphereCastNonAlloc(Vector3 origin, float radius, Vector3 direction, float distance, int layerMask, RaycastHit[] results);

        /// <summary>
        /// 执行无分配方体检测，仅用于浮动胶囊内部脚底 BoxCollider 的接地检测。
        /// </summary>
        /// <param name="center">方体中心点。</param>
        /// <param name="halfExtents">方体在三个轴向上的半尺寸。</param>
        /// <param name="direction">检测方向。</param>
        /// <param name="orientation">方体的世界空间朝向。</param>
        /// <param name="distance">最大检测距离。</param>
        /// <param name="layerMask">参与检测的物理层。</param>
        /// <param name="results">复用的命中结果缓冲区。</param>
        /// <returns>写入缓冲区的命中数量。</returns>
        int FootBoxCastNonAlloc(
            Vector3 center,
            Vector3 halfExtents,
            Vector3 direction,
            Quaternion orientation,
            float distance,
            int layerMask,
            RaycastHit[] results);
    }
}

