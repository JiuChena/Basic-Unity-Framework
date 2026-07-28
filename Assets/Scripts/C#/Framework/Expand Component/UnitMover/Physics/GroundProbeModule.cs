using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 描述一次经过层、Trigger、自身和坡度过滤后的可行走地面命中。
    /// </summary>
    public readonly struct GroundContact
    {
        /// <summary>
        /// 创建地面命中结果。
        /// </summary>
        /// <param name="hasContact">是否存在可行走地面。</param>
        /// <param name="hit">距离最近的可行走地面命中。</param>
        public GroundContact(bool hasContact, RaycastHit hit)
            : this(hasContact, hit, hit.distance)
        {
        }

        /// <summary>
        /// 创建地面命中结果，并保存从有效碰撞体底部换算后的接地距离。
        /// </summary>
        /// <param name="hasContact">是否存在可行走地面。</param>
        /// <param name="hit">距离最近的可行走地面命中。</param>
        /// <param name="distance">有效碰撞体底部至地面的距离，单位：米。</param>
        public GroundContact(bool hasContact, RaycastHit hit, float distance)
        {
            HasContact = hasContact;
            Hit = hit;
            Distance = distance;
        }

        /// <summary>是否命中可行走地面。</summary>
        public bool HasContact { get; }

        /// <summary>距离最近的可行走地面命中。</summary>
        public RaycastHit Hit { get; }

        /// <summary>有效碰撞体底部至地面的距离。</summary>
        public float Distance { get; }
    }

    /// <summary>
    /// 负责实际 Collider 形状接地、射线支撑确认以及统一的地面有效性过滤。
    /// </summary>
    public sealed class GroundProbeModule
    {
        // 实际参与运动检测的 Collider。
        private readonly Collider _movementCollider;
        // 宿主根节点，用于排除自身和子物体碰撞体。
        private readonly Transform _ownerRoot;
        // 抽象后的无分配 Physics 查询。
        private readonly IPhysicsQuery _physicsQuery;
        // 接地、坡度和层配置。
        private readonly GroundSettings _settings;
        // 提供浮动胶囊相对基础底部的实际世界留空高度。
        private readonly ColliderShapeModule _shapeModule;
        // 复用接地形状检测的命中缓冲区。
        private readonly RaycastHit[] _castHits = new RaycastHit[8];
        // 复用射线和球体检测的命中缓冲区。
        private readonly RaycastHit[] _queryHits = new RaycastHit[8];

        /// <summary>
        /// 构造接地查询模块并缓存其固定依赖。
        /// </summary>
        /// <param name="shapeModule">同步有效碰撞形状并提供浮动底部留空距离的模块。</param>
        /// <param name="ownerRoot">UnitMover 宿主根节点。</param>
        /// <param name="physicsQuery">无分配 Physics 查询实现。</param>
        /// <param name="settings">接地与坡面配置。</param>
        public GroundProbeModule(
            ColliderShapeModule shapeModule,
            Transform ownerRoot,
            IPhysicsQuery physicsQuery,
            GroundSettings settings)
        {
            _shapeModule = shapeModule;
            _movementCollider = shapeModule != null ? shapeModule.MovementCollider : null;
            _ownerRoot = ownerRoot;
            _physicsQuery = physicsQuery;
            _settings = settings;
        }

        /// <summary>获取有效胶囊底部到地面应保持的总支撑距离。</summary>
        public float DesiredGroundDistance => _settings == null
            ? 0f
            : (_shapeModule != null ? _shapeModule.GetFloatingBottomClearance() : 0f)
                + _settings.HoverHeight;

        /// <summary>获取总支撑距离和额外探测长度组成的常规接地距离。</summary>
        public float GroundCheckDistance => _settings == null
            ? 0f
            : DesiredGroundDistance + _settings.ProbeDistance;

        /// <summary>获取当前接地探测期望保持的总支撑距离。</summary>
        public float HoverHeight => DesiredGroundDistance;

        /// <summary>
        /// 使用 BoxCollider 的方体 Cast 或 CapsuleCollider 的脚底球体 Cast，获取距离最近的可行走地面。
        /// </summary>
        /// <returns>经过层、Trigger、自身和坡度过滤后的地面接触结果。</returns>
        public GroundContact ProbeGround()
        {
            if (_movementCollider == null || _settings == null) return new GroundContact(false, default);

            float distance = GroundCheckDistance;
            if (_movementCollider is BoxCollider box)
                return ProbeBoxGround(box, distance);

            return ProbeCapsuleGround(distance);
        }

        /// <summary>
        /// 使用 BoxCollider 当前世界尺寸和朝向进行无分配方体 Cast。
        /// </summary>
        /// <param name="box">参与移动的 BoxCollider。</param>
        /// <param name="distance">碰撞体底部额外向下检测的距离，单位：米。</param>
        /// <returns>经过统一过滤后的地面接触结果。</returns>
        private GroundContact ProbeBoxGround(BoxCollider box, float distance)
        {
            if (_physicsQuery == null) return new GroundContact(false, default);

            Vector3 lossyScale = box.transform.lossyScale;
            Vector3 absoluteScale = new Vector3(
                Mathf.Abs(lossyScale.x),
                Mathf.Abs(lossyScale.y),
                Mathf.Abs(lossyScale.z));
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, absoluteScale);
            Vector3 center = box.transform.TransformPoint(box.center);
            int hitCount = _physicsQuery.BoxCastNonAlloc(
                center,
                halfExtents,
                Vector3.down,
                box.transform.rotation,
                distance,
                _settings.GroundLayer.value,
                _castHits);
            if (!TrySelectWalkableHit(_castHits, hitCount, out RaycastHit hit)) return new GroundContact(false, default);

            return new GroundContact(true, hit);
        }

        /// <summary>
        /// 用与有效 CapsuleCollider 脚底相同半径的球体 Cast 接地，并将命中距离换算为脚底间隙。
        /// </summary>
        /// <param name="distance">胶囊底部额外向下检测的距离，单位：米。</param>
        /// <returns>经过统一过滤后的地面接触结果。</returns>
        private GroundContact ProbeCapsuleGround(float distance)
        {
            if (_physicsQuery == null) return new GroundContact(false, default);

            Bounds bounds = _movementCollider.bounds;
            float supportRadius = Mathf.Max(0.001f, Mathf.Min(bounds.extents.x, bounds.extents.z));
            float centerToSphereBottom = Mathf.Max(0f, bounds.extents.y - supportRadius);
            Vector3 bottomSphereCenter = bounds.center - Vector3.up * centerToSphereBottom;
            int hitCount = _physicsQuery.SphereCastNonAlloc(
                bottomSphereCenter,
                supportRadius,
                Vector3.down,
                distance,
                _settings.GroundLayer.value,
                _castHits);
            if (!TrySelectWalkableHit(_castHits, hitCount, out RaycastHit hit)) return new GroundContact(false, default);

            return new GroundContact(true, hit, Mathf.Max(0f, hit.distance));
        }

        /// <summary>
        /// 从指定位置沿指定方向查询可行走地面。
        /// </summary>
        /// <param name="origin">查询起点。</param>
        /// <param name="direction">查询方向。</param>
        /// <param name="distance">最大查询距离。</param>
        /// <param name="hit">找到时返回最近的可行走命中。</param>
        /// <returns>是否找到可行走地面。</returns>
        public bool TryGetWalkableGround(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            hit = default;
            if (_physicsQuery == null || _settings == null || distance <= 0f) return false;

            int hitCount = _physicsQuery.RaycastNonAlloc(
                origin,
                direction,
                distance,
                _settings.GroundLayer.value,
                _queryHits);
            return TrySelectWalkableHit(_queryHits, hitCount, out hit);
        }

        /// <summary>
        /// 从指定位置沿指定方向查询任意实体障碍物，用于台阶前缘检测。
        /// </summary>
        /// <param name="origin">查询起点。</param>
        /// <param name="direction">查询方向。</param>
        /// <param name="distance">最大查询距离。</param>
        /// <param name="hit">找到时返回最近的实体命中。</param>
        /// <returns>是否找到非自身的实体碰撞体。</returns>
        public bool TryGetSolid(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            hit = default;
            if (_physicsQuery == null || _settings == null || distance <= 0f) return false;

            int hitCount = _physicsQuery.RaycastNonAlloc(
                origin,
                direction,
                distance,
                _settings.GroundLayer.value,
                _queryHits);
            return TrySelectSolidHit(_queryHits, hitCount, out hit);
        }

        /// <summary>
        /// 以脚底球体确认指定位置下方是否存在可覆盖的可行走支撑。
        /// </summary>
        /// <param name="origin">球体检测起点。</param>
        /// <param name="radius">脚底确认球半径。</param>
        /// <param name="distance">向下检测距离。</param>
        /// <returns>是否确认到可行走支撑。</returns>
        public bool HasWalkableSphereSupport(Vector3 origin, float radius, float distance)
        {
            if (_physicsQuery == null || _settings == null || radius <= 0f || distance <= 0f) return false;

            int hitCount = _physicsQuery.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                distance,
                _settings.GroundLayer.value,
                _queryHits);
            return TrySelectWalkableHit(_queryHits, hitCount, out _);
        }

        /// <summary>
        /// 判断给定表面法线是否满足最大可行走坡度。
        /// </summary>
        /// <param name="normal">需要验证的世界空间法线。</param>
        /// <returns>法线是否允许作为可行走地面。</returns>
        public bool IsWalkable(Vector3 normal)
        {
            if (_settings == null) return false;
            return Vector3.Angle(normal, Vector3.up) <= _settings.SlopeLimit;
        }

        /// <summary>
        /// 从命中缓冲区挑选最近的有效可行走地面。
        /// </summary>
        /// <param name="hits">待过滤的命中缓冲区。</param>
        /// <param name="hitCount">缓冲区内有效命中数量。</param>
        /// <param name="selectedHit">找到时返回最近的可行走命中。</param>
        /// <returns>是否找到符合规则的可行走命中。</returns>
        private bool TrySelectWalkableHit(RaycastHit[] hits, int hitCount, out RaycastHit selectedHit)
        {
            selectedHit = default;
            float nearestDistance = float.PositiveInfinity;

            // 统一执行层、Trigger、自身和坡度过滤，保证所有模块使用同一地面定义。
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = hits[index];
                if (!IsValidGroundCollider(candidate.collider)) continue;
                if (!IsWalkable(candidate.normal)) continue;
                if (candidate.distance >= nearestDistance) continue;

                nearestDistance = candidate.distance;
                selectedHit = candidate;
            }

            return nearestDistance < float.PositiveInfinity;
        }

        /// <summary>
        /// 从命中缓冲区挑选最近的非自身实体障碍物。
        /// </summary>
        /// <param name="hits">待过滤的命中缓冲区。</param>
        /// <param name="hitCount">缓冲区内有效命中数量。</param>
        /// <param name="selectedHit">找到时返回最近的实体命中。</param>
        /// <returns>是否找到符合规则的实体命中。</returns>
        private bool TrySelectSolidHit(RaycastHit[] hits, int hitCount, out RaycastHit selectedHit)
        {
            selectedHit = default;
            float nearestDistance = float.PositiveInfinity;

            // 台阶障碍不要求坡面可行走，但仍必须排除 Trigger、自身和非地面层。
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = hits[index];
                if (!IsValidSolidCollider(candidate.collider)) continue;
                if (candidate.distance >= nearestDistance) continue;

                nearestDistance = candidate.distance;
                selectedHit = candidate;
            }

            return nearestDistance < float.PositiveInfinity;
        }

        /// <summary>
        /// 验证碰撞体是否能够作为地面支撑的一部分。
        /// </summary>
        /// <param name="collider">需要验证的命中碰撞体。</param>
        /// <returns>碰撞体是否位于地面层、非 Trigger 且非自身层级。</returns>
        private bool IsValidGroundCollider(Collider collider)
        {
            return IsValidSolidCollider(collider);
        }

        /// <summary>
        /// 验证碰撞体是否可作为非 Trigger 的实体障碍物。
        /// </summary>
        /// <param name="collider">需要验证的命中碰撞体。</param>
        /// <returns>碰撞体是否符合实体障碍物规则。</returns>
        private bool IsValidSolidCollider(Collider collider)
        {
            if (collider == null || collider.isTrigger || _settings == null) return false;
            if (((1 << collider.gameObject.layer) & _settings.GroundLayer.value) == 0) return false;
            if (_ownerRoot != null && collider.transform.IsChildOf(_ownerRoot)) return false;

            return true;
        }
    }
}
