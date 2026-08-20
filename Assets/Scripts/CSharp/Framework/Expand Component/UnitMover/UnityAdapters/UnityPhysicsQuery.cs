using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 使用 Unity Physics NonAlloc API 实现物理查询契约。
    /// </summary>
    public sealed class UnityPhysicsQuery : IPhysicsQuery
    {
        /// <summary>
        /// 执行忽略 Trigger 的无分配射线检测。
        /// </summary>
        /// <param name="origin">射线起点。</param>
        /// <param name="direction">射线方向。</param>
        /// <param name="distance">最大检测距离。</param>
        /// <param name="layerMask">参与检测的物理层。</param>
        /// <param name="results">复用的命中结果缓冲区。</param>
        /// <returns>写入缓冲区的命中数量。</returns>
        public int RaycastNonAlloc(Vector3 origin, Vector3 direction, float distance, int layerMask, RaycastHit[] results)
        {
            return Physics.RaycastNonAlloc(
                origin,
                direction,
                results,
                distance,
                layerMask,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// 执行忽略 Trigger 的无分配球体检测。
        /// </summary>
        /// <param name="origin">球体检测起点。</param>
        /// <param name="radius">球体半径。</param>
        /// <param name="direction">检测方向。</param>
        /// <param name="distance">最大检测距离。</param>
        /// <param name="layerMask">参与检测的物理层。</param>
        /// <param name="results">复用的命中结果缓冲区。</param>
        /// <returns>写入缓冲区的命中数量。</returns>
        public int SphereCastNonAlloc(Vector3 origin, float radius, Vector3 direction, float distance, int layerMask, RaycastHit[] results)
        {
            return Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                results,
                distance,
                layerMask,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// 执行忽略 Trigger 的无分配方体检测，仅供内部脚底 BoxCollider 使用。
        /// </summary>
        /// <param name="center">方体检测中心。</param>
        /// <param name="halfExtents">方体半尺寸。</param>
        /// <param name="direction">检测方向。</param>
        /// <param name="orientation">方体世界朝向。</param>
        /// <param name="distance">最大检测距离。</param>
        /// <param name="layerMask">参与检测的物理层。</param>
        /// <param name="results">复用的命中结果缓冲区。</param>
        /// <returns>写入缓冲区的命中数量。</returns>
        public int FootBoxCastNonAlloc(
            Vector3 center,
            Vector3 halfExtents,
            Vector3 direction,
            Quaternion orientation,
            float distance,
            int layerMask,
            RaycastHit[] results)
        {
            return Physics.BoxCastNonAlloc(
                center,
                halfExtents,
                direction,
                results,
                orientation,
                distance,
                layerMask,
                QueryTriggerInteraction.Ignore);
        }
    }
}
