using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 描述一次经过层、Trigger 和自身过滤后的地面命中及其可站立性。
    /// </summary>
    public readonly struct GroundContact
    {
        /// <summary>
        /// 创建地面命中结果。
        /// </summary>
        /// <param name="hasContact">是否存在有效地面命中。</param>
        /// <param name="hit">距离最近的有效地面命中。</param>
        public GroundContact(bool hasContact, RaycastHit hit)
            : this(
                hasContact,
                hasContact,
                hit,
                hit.distance,
                hasContact ? Vector3.Angle(hit.normal, Vector3.up) : 0f)
        {
        }

        /// <summary>
        /// 创建地面命中结果，并保存从有效碰撞体底部换算后的接地距离。
        /// </summary>
        /// <param name="hasContact">是否存在有效地面命中。</param>
        /// <param name="isWalkable">命中地面是否在允许站立的坡度范围内。</param>
        /// <param name="hit">距离最近的有效地面命中。</param>
        /// <param name="distance">有效碰撞体底部至地面的距离，单位：米。</param>
        /// <param name="slopeAngle">命中法线相对世界向上的坡度角，单位：度。</param>
        public GroundContact(bool hasContact, bool isWalkable, RaycastHit hit, float distance, float slopeAngle)
        {
            HasContact = hasContact;
            IsWalkable = hasContact && isWalkable;
            Hit = hit;
            Distance = distance;
            SlopeAngle = hasContact ? slopeAngle : 0f;
        }

        /// <summary>是否命中有效地面。</summary>
        public bool HasContact { get; }

        /// <summary>命中地面是否允许作为正常站立地面。</summary>
        public bool IsWalkable { get; }

        /// <summary>是否可作为当前物理步的正常接地结果。</summary>
        public bool IsGrounded => HasContact && IsWalkable;

        /// <summary>距离最近的有效地面命中。</summary>
        public RaycastHit Hit { get; }

        /// <summary>有效碰撞体底部至地面的距离。</summary>
        public float Distance { get; }

        /// <summary>命中斜面相对世界向上的坡度角，单位：度。</summary>
        public float SlopeAngle { get; }
    }

    /// <summary>
    /// 负责实际 Collider 形状接地、射线支撑确认以及统一的地面有效性过滤。
    /// </summary>
    public sealed class GroundProbeModule
    {
        // 主体运动胶囊，用于在形状模块不可用时的接地回退。
        private readonly CapsuleCollider _movementCollider;
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
        // 以胶囊脚底半径为基准采样稳定支撑时使用的水平偏移比例。
        private const float StableSupportOffsetRatio = 0.55f;
        // 除中心点外至少需要命中的方向采样数量。
        private const int RequiredPeripheralSupportCount = 2;

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

        /// <summary>获取有效支撑形状底部到地面应保持的总支撑距离。</summary>
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
        /// 使用内部脚底 BoxCollider 或主胶囊脚底球体 Cast，获取距离最近的有效地面。
        /// </summary>
        /// <returns>经过层、Trigger 和自身过滤后的地面接触结果，并保留可站立性。</returns>
        public GroundContact ProbeGround()
        {
            if (_movementCollider == null || _settings == null) return new GroundContact(false, default);

            float distance = GroundCheckDistance;
            BoxCollider footCollider = _shapeModule != null ? _shapeModule.ActiveFootCollider : null;
            if (footCollider != null)
                return ProbeFootBoxGround(footCollider, distance);

            return ProbeCapsuleGround(_movementCollider, distance);
        }

        /// <summary>
        /// 以脚底 BoxCast 为主探针，并在区域未命中时使用中心射线兜底。
        /// </summary>
        /// <param name="footCollider">仅由浮动胶囊内部维护的脚底 BoxCollider。</param>
        /// <param name="distance">碰撞体底部额外向下检测的距离，单位：米。</param>
        /// <returns>经过统一过滤后的地面接触结果。</returns>
        private GroundContact ProbeFootBoxGround(BoxCollider footCollider, float distance)
        {
            if (_physicsQuery == null) return new GroundContact(false, default);

            // 区域检测优先覆盖脚底实际占地，并以最近命中面提供坡度法线。
            Vector3 halfExtents = _shapeModule.GetFootSupportProbeHalfExtents();
            Vector3 center = footCollider.transform.TransformPoint(footCollider.center) + Vector3.up * distance;
            int hitCount = _physicsQuery.FootBoxCastNonAlloc(
                center,
                halfExtents,
                Vector3.down,
                footCollider.transform.rotation,
                distance * 2f,
                _settings.GroundLayer.value,
                _castHits);
            bool hasBoxGroundHit = TrySelectGroundHit(_castHits, hitCount, out RaycastHit boxHit);
            if (hasBoxGroundHit)
            {
                // BoxCast 起点比当前脚底底面高 distance，因此需要换算回真实脚底间隙。
                float boxBottomDistance = Mathf.Max(0f, boxHit.distance - distance);
                return CreateGroundContact(boxHit, boxBottomDistance);
            }

            // 区域未命中时才使用有界中心射线，避免窄平台或数值边界导致瞬间失去接地。
            bool hasCenterGroundHit = TryProbeFootCenterGround(
                footCollider,
                distance,
                out RaycastHit centerHit,
                out float centerBottomDistance);
            if (!hasCenterGroundHit) return new GroundContact(false, default);

            return CreateGroundContact(centerHit, centerBottomDistance);
        }

        /// <summary>
        /// 从脚底辅助体上方的恢复范围向下执行有界中心射线，获取对应三角面的可靠法线。
        /// </summary>
        /// <param name="footCollider">浮动胶囊维护的脚底 BoxCollider。</param>
        /// <param name="groundDistance">脚底上下两侧各自允许的最大接地恢复距离，单位：米。</param>
        /// <param name="hit">找到时返回经过统一过滤的最近中心射线命中。</param>
        /// <param name="bottomDistance">找到时返回换算为脚底底面到地面的距离，单位：米。</param>
        /// <returns>是否找到层、Trigger 和自身规则均通过的中心射线地面命中。</returns>
        private bool TryProbeFootCenterGround(
            BoxCollider footCollider,
            float groundDistance,
            out RaycastHit hit,
            out float bottomDistance)
        {
            hit = default;
            bottomDistance = 0f;
            if (_physicsQuery == null || footCollider == null) return false;

            // 从脚底辅助体上方发射中心线，让已下陷的脚底仍能向下命中上方地面并获得回正依据。
            Bounds footBounds = footCollider.bounds;
            Vector3 origin = new Vector3(footBounds.center.x, footBounds.max.y + groundDistance, footBounds.center.z);
            float originToFootBottom = Mathf.Max(0f, footBounds.max.y - footBounds.min.y) + groundDistance;
            float rayDistance = originToFootBottom + groundDistance;
            int hitCount = _physicsQuery.RaycastNonAlloc(
                origin,
                Vector3.down,
                rayDistance,
                _settings.GroundLayer.value,
                _queryHits);
            bool hasGroundHit = TrySelectGroundHit(_queryHits, hitCount, out hit);
            if (!hasGroundHit) return false;

            // 射线从脚底上方恢复范围开始，统一换算为脚底底面到地面的距离供悬浮模块消费。
            bottomDistance = Mathf.Max(0f, hit.distance - originToFootBottom);
            return true;
        }

        /// <summary>
        /// 用与有效 CapsuleCollider 脚底相同半径的球体 Cast 接地，并将命中距离换算为脚底间隙。
        /// </summary>
        /// <param name="distance">胶囊底部额外向下检测的距离，单位：米。</param>
        /// <returns>经过统一过滤后的地面接触结果。</returns>
        private GroundContact ProbeCapsuleGround(CapsuleCollider capsule, float distance)
        {
            if (_physicsQuery == null || capsule == null) return new GroundContact(false, default);

            Bounds bounds = capsule.bounds;
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
            if (!TrySelectGroundHit(_castHits, hitCount, out RaycastHit hit)) return new GroundContact(false, default);

            return CreateGroundContact(hit, Mathf.Max(0f, hit.distance));
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
        /// 根据当前接地结果判断脚底是否具有足以记录安全位置和启用边缘保护的稳定支撑。
        /// </summary>
        /// <param name="contact">当前物理步已完成过滤的地面接触结果。</param>
        /// <returns>中心点命中且四个周向采样中至少两个命中时返回 true。</returns>
        public bool HasStableSupport(in GroundContact contact)
        {
            // 不可站立接触和无接触都不能作为边缘保护的稳定支撑。
            if (!contact.IsGrounded || _movementCollider == null || _settings == null || _shapeModule == null) return false;

            // 从当前有效胶囊边界构建中心和四个周向采样点，不依赖移动方向。
            Bounds bounds = _shapeModule.Bounds;
            float supportRadius = Mathf.Max(0.001f, Mathf.Min(bounds.extents.x, bounds.extents.z));
            float offset = supportRadius * StableSupportOffsetRatio;
            float distance = bounds.size.y + GroundCheckDistance;
            Vector3 origin = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            if (!TryGetWalkableGround(origin, Vector3.down, distance, out _)) return false;

            // 中心稳定后要求至少两个周向点具备支撑，避免胶囊边缘单点命中覆盖安全回退位置。
            int peripheralSupportCount = 0;
            if (TryGetWalkableGround(origin + Vector3.right * offset, Vector3.down, distance, out _))
                peripheralSupportCount++;
            if (TryGetWalkableGround(origin + Vector3.left * offset, Vector3.down, distance, out _))
                peripheralSupportCount++;
            if (TryGetWalkableGround(origin + Vector3.forward * offset, Vector3.down, distance, out _))
                peripheralSupportCount++;
            if (TryGetWalkableGround(origin + Vector3.back * offset, Vector3.down, distance, out _))
                peripheralSupportCount++;

            return peripheralSupportCount >= RequiredPeripheralSupportCount;
        }

        /// <summary>
        /// 将本模块产生的地面接触转换为统一的运动状态快照。
        /// </summary>
        /// <param name="contact">当前物理步已完成过滤的地面接触结果。</param>
        /// <param name="mode">由运动策略选择的当前运动模式。</param>
        /// <param name="velocity">当前物理步开始时的刚体速度。</param>
        /// <param name="isJumping">是否处于跳跃起跳后的短暂地面豁免阶段。</param>
        /// <returns>包含接地、稳定支撑和地面几何信息的状态快照。</returns>
        public UnitMovementState CreateMovementState(
            in GroundContact contact,
            MovementMode mode,
            Vector3 velocity,
            bool isJumping)
        {
            // 稳定支撑只在普通可行走接地时查询，避免无接触时产生额外 Physics 查询。
            bool isGrounded = contact.IsGrounded;
            bool isStableGrounded = HasStableSupport(contact);

            // 地面几何数据只由接地探测模块解释，调用方不需要重复判断命中有效性。
            return new UnitMovementState(
                isGrounded,
                isStableGrounded,
                contact.HasContact ? contact.Hit.normal : Vector3.up,
                contact.HasContact ? contact.Hit.point : Vector3.zero,
                contact.HasContact ? contact.Distance : float.PositiveInfinity,
                velocity,
                mode,
                isJumping);
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
        /// 将已选中的有效地面命中转换为包含坡度角和可行走状态的接触结果。
        /// </summary>
        /// <param name="hit">通过层、Trigger 和自身过滤的最近地面命中。</param>
        /// <param name="distance">有效碰撞体底部至该地面的已换算距离，单位：米。</param>
        /// <returns>保留坡度角、可行走状态和命中信息的地面接触结果。</returns>
        private GroundContact CreateGroundContact(RaycastHit hit, float distance)
        {
            // 只计算一次坡度角，供可行走判定和陡坡速度修正共同消费。
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            bool isWalkable = slopeAngle <= _settings.SlopeLimit;
            return new GroundContact(true, isWalkable, hit, distance, slopeAngle);
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
        /// 从命中缓冲区挑选最近的有效地面命中，不因坡度超过站立限制而丢弃该斜面。
        /// </summary>
        /// <param name="hits">待过滤的命中缓冲区。</param>
        /// <param name="hitCount">缓冲区内有效命中数量。</param>
        /// <param name="selectedHit">找到时返回最近的有效地面命中。</param>
        /// <returns>是否找到层、Trigger 和自身规则均通过的地面命中。</returns>
        private bool TrySelectGroundHit(RaycastHit[] hits, int hitCount, out RaycastHit selectedHit)
        {
            selectedHit = default;
            float nearestDistance = float.PositiveInfinity;

            // 保留最近的真实接触面，让运行时能够对不可站立的陡坡施加下坡修正。
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = hits[index];
                if (!IsValidGroundCollider(candidate.collider)) continue;
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
            if (collider == null || collider.isTrigger || _settings == null) return false;
            if (((1 << collider.gameObject.layer) & _settings.GroundLayer.value) == 0) return false;
            if (_ownerRoot != null && collider.transform.IsChildOf(_ownerRoot)) return false;

            return true;
        }
    }

    /// <summary>在超过站立限制的斜坡上按坡度施加下坡速度修正。</summary>
    internal sealed class SteepSlopeSlideModule
    {
        // 提供坡度限制、曲线下滑因数和速度上限的地面配置。
        private readonly GroundSettings _settings;
        // 当前是否已连续处于同一段不可站立斜坡；首次进入时只阻止上坡。
        private bool _isOnSteepSlope;

        /// <summary>
        /// 创建陡坡下滑模块并缓存地面配置。
        /// </summary>
        /// <param name="settings">地面、坡度和下滑参数配置；为 null 时不产生修正。</param>
        internal SteepSlopeSlideModule(GroundSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// 首次进入不可站立斜面时阻止上坡，后续物理步补充最小下坡目标速度。
        /// </summary>
        /// <param name="velocity">完成重力计算后、尚未提交给刚体的速度。</param>
        /// <param name="contact">本物理步最近的地面接触结果。</param>
        /// <returns>首次进入时仅清除上坡分量；已在斜坡上时会补足目标下坡速度。</returns>
        internal Vector3 Apply(Vector3 velocity, in GroundContact contact)
        {
            if (_settings == null) return velocity;
            if (!contact.HasContact || contact.IsWalkable)
            {
                // 离开陡坡或回到可行走面后，下一次命中陡坡需再次走首次阻止分支。
                _isOnSteepSlope = false;
                return velocity;
            }

            // 将重力方向投影到斜面上，得到严格沿坡面向下的修正方向。
            Vector3 downhillDirection = Vector3.ProjectOnPlane(Vector3.down, contact.Hit.normal);
            if (downhillDirection.sqrMagnitude <= 0.000001f)
                return velocity;

            downhillDirection.Normalize();
            float uphillSpeed = Vector3.Dot(velocity, -downhillDirection);
            float uphillCorrectionSpeed = Mathf.Max(0f, uphillSpeed);

            // 首次进入陡坡只阻止上坡推进，避免当前帧被完整目标下坡速度突然反推。
            bool justEnteredSteepSlope = !_isOnSteepSlope;
            _isOnSteepSlope = true;
            if (justEnteredSteepSlope)
            {
                return velocity + downhillDirection * uphillCorrectionSpeed;
            }

            // 已在斜坡上时恢复正常下坡目标速度，使持续停留时仍能自然滑落。
            float normalizedSlopeDifference = CalculateNormalizedSlopeDifference(contact.SlopeAngle);
            float slideRatio = _settings.EvaluateSteepSlopeSlideRatio(normalizedSlopeDifference);
            float downhillSpeed = Vector3.Dot(velocity, downhillDirection);
            float targetDownhillSpeed = Mathf.Clamp(
                _settings.SteepSlopeSlideFactor * slideRatio,
                0f,
                _settings.SteepSlopeSlideSpeedLimit);

            // 先完整抵消上坡速度，再把下坡分量补足到由逆坡比例决定的目标速度。
            float targetSpeedCorrection = Mathf.Max(0f, targetDownhillSpeed - downhillSpeed);
            float downhillCorrectionSpeed = Mathf.Max(uphillCorrectionSpeed, targetSpeedCorrection);
            Vector3 downhillVelocityAddition = downhillDirection * downhillCorrectionSpeed;
            return velocity + downhillVelocityAddition;
        }

        /// <summary>
        /// 将超出可行走限制的坡度差转换为动画曲线的归一化输入。
        /// </summary>
        /// <param name="slopeAngle">当前接触面相对世界向上的坡度角，单位：度。</param>
        /// <returns>位于 0 到 1 的归一化超限坡度差；未超过限制时返回 0。</returns>
        private float CalculateNormalizedSlopeDifference(float slopeAngle)
        {
            // 只负责将实际角度归一化，曲线如何塑形由 GroundSettings 的 Inspector 配置决定。
            float maximumSlopeDifference = Mathf.Max(0.0001f, 90f - _settings.SlopeLimit);
            return Mathf.Clamp01((slopeAngle - _settings.SlopeLimit) / maximumSlopeDifference);
        }

    }
}
