using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 将 UnitMover 的统一运动结果写入 Rigidbody，并负责恢复接管前设置。
    /// </summary>
    public sealed class RigidbodyUnitBody : IUnitBody
    {
        // 被 UnitMover 接管的刚体组件。
        private readonly Rigidbody _rigidbody;
        // 接管前是否使用 Unity 内置重力的快照。
        private readonly bool _initialUseGravity;
        // 接管前的刚体插值模式快照。
        private readonly RigidbodyInterpolation _initialInterpolation;
        // 接管前的运动学状态快照。
        private readonly bool _initialIsKinematic;
        // 接管前的位置与旋转约束快照。
        private readonly RigidbodyConstraints _initialConstraints;
        // 是否由 UnitMover 冻结刚体旋转并清空角速度。
        private readonly bool _freezeRotation;

        /// <summary>
        /// 记录刚体初始状态并切换到由 UnitMover 统一处理重力的模式。
        /// </summary>
        /// <param name="rigidbody">需要接管的刚体组件。</param>
        /// <param name="freezeRotation">是否在接管期间冻结刚体旋转。</param>
        public RigidbodyUnitBody(Rigidbody rigidbody, bool freezeRotation)
        {
            _rigidbody = rigidbody;
            _freezeRotation = freezeRotation;
            _initialUseGravity = rigidbody != null && rigidbody.useGravity;
            _initialInterpolation = rigidbody != null ? rigidbody.interpolation : RigidbodyInterpolation.None;
            _initialIsKinematic = rigidbody != null && rigidbody.isKinematic;
            _initialConstraints = rigidbody != null
                ? rigidbody.constraints
                : RigidbodyConstraints.None;

            if (_rigidbody == null) return;

            // UnitMover 负责重力和速度结算，避免 Unity 重力重复作用。
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = false;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            if (_freezeRotation)
            {
                _rigidbody.constraints = _initialConstraints | RigidbodyConstraints.FreezeRotation;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>获取底层刚体是否仍然可用。</summary>
        public bool IsValid => _rigidbody != null;

        /// <summary>获取当前刚体世界位置。</summary>
        public Vector3 Position => _rigidbody != null ? _rigidbody.position : Vector3.zero;

        /// <summary>获取当前刚体世界旋转。</summary>
        public Quaternion Rotation => _rigidbody != null ? _rigidbody.rotation : Quaternion.identity;

        /// <summary>获取当前刚体线速度。</summary>
        public Vector3 Velocity => _rigidbody != null ? _rigidbody.velocity : Vector3.zero;

        /// <summary>
        /// 一次性提交所有模块合并后的最终线速度，防止功能模块彼此覆盖结果。
        /// </summary>
        /// <param name="velocity">当前物理步最终线速度。</param>
        public void Commit(Vector3 velocity)
        {
            if (_rigidbody == null) return;

            // Rigidbody 速度仅在本适配器中写入，其他模块只能返回计算结果。
            _rigidbody.velocity = velocity;
            if (_freezeRotation) _rigidbody.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// 回到业务层显式记录的检查点并清除线速度；旋转冻结时同时清除角速度。
        /// </summary>
        /// <param name="position">安全快照中的世界位置。</param>
        /// <param name="rotation">安全快照中的世界旋转。</param>
        public void RestoreCheckpoint(Vector3 position, Quaternion rotation)
        {
            if (_rigidbody == null) return;

            _rigidbody.position = position;
            _rigidbody.rotation = rotation;
            _rigidbody.velocity = Vector3.zero;
            if (_freezeRotation) _rigidbody.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// 恢复 UnitMover 接管前的刚体设置，避免禁用组件后残留物理配置。
        /// </summary>
        public void RestoreInitialSettings()
        {
            if (_rigidbody == null) return;

            _rigidbody.useGravity = _initialUseGravity;
            _rigidbody.interpolation = _initialInterpolation;
            _rigidbody.isKinematic = _initialIsKinematic;
            _rigidbody.constraints = _initialConstraints;
        }
    }

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
