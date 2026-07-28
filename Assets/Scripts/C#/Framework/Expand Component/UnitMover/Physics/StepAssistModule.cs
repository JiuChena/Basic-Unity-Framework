using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 通过前缘障碍、上方净空和落点支撑验证，为低台阶提供有限向上速度建议。
    /// </summary>
    public sealed class StepAssistModule
    {
        // 提供有效 Collider 边界和前缘尺寸的形状模块。
        private readonly ColliderShapeModule _shapeModule;
        // 提供统一过滤规则的地面查询模块。
        private readonly GroundProbeModule _groundProbe;
        // 自动台阶高度和速度配置。
        private readonly StepSettings _settings;

        /// <summary>
        /// 初始化台阶辅助模块。
        /// </summary>
        /// <param name="shapeModule">有效碰撞体形状模块。</param>
        /// <param name="groundProbe">统一地面查询模块。</param>
        /// <param name="settings">台阶辅助配置。</param>
        public StepAssistModule(
            ColliderShapeModule shapeModule,
            GroundProbeModule groundProbe,
            StepSettings settings)
        {
            _shapeModule = shapeModule;
            _groundProbe = groundProbe;
            _settings = settings;
        }

        /// <summary>
        /// 在稳定接地且前进时计算跨越合法低台阶所需的最小向上速度。
        /// </summary>
        /// <param name="state">当前接地状态。</param>
        /// <param name="worldDirection">本物理步的候选世界移动方向。</param>
        /// <param name="currentVelocity">刚体当前速度。</param>
        /// <param name="fixedDeltaTime">本物理步时长，单位：秒。</param>
        /// <returns>需要附加的向上速度；零表示不应执行台阶辅助。</returns>
        public float CalculateUpwardSpeed(
            in UnitMovementState state,
            Vector3 worldDirection,
            Vector3 currentVelocity,
            float fixedDeltaTime)
        {
            if (_settings == null || _settings.MaxHeight <= 0f) return 0f;
            if (!state.IsStableGrounded || _shapeModule == null || _groundProbe == null) return 0f;

            Vector3 moveDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            if (moveDirection.sqrMagnitude <= 0.000001f) return 0f;
            moveDirection.Normalize();

            Bounds bounds = _shapeModule.Bounds;
            float forwardDistance = _shapeModule.GetHorizontalExtent(moveDirection) + _settings.ProbePadding;
            Vector3 lowerOrigin = new Vector3(bounds.center.x, bounds.min.y + 0.02f, bounds.center.z);
            if (!_groundProbe.TryGetSolid(lowerOrigin, moveDirection, forwardDistance, out _)) return 0f;

            // 上方仍有实体时说明不是可跨越的低台阶。
            Vector3 upperOrigin = lowerOrigin + Vector3.up * _settings.MaxHeight;
            if (_groundProbe.TryGetSolid(upperOrigin, moveDirection, forwardDistance, out _)) return 0f;

            Vector3 landingOrigin = upperOrigin + moveDirection * forwardDistance;
            float landingDistance = _settings.MaxHeight + _groundProbe.GroundCheckDistance;
            if (!_groundProbe.TryGetWalkableGround(landingOrigin, Vector3.down, landingDistance, out RaycastHit landing)) return 0f;

            float requiredHeight = landing.point.y - bounds.min.y;
            if (requiredHeight <= 0f || requiredHeight > _settings.MaxHeight) return 0f;

            float currentUpSpeed = Vector3.Dot(currentVelocity, Vector3.up);
            float targetUpSpeed = Mathf.Min(_settings.MaxUpSpeed, requiredHeight / Mathf.Max(fixedDeltaTime, 0.0001f));
            return Mathf.Max(0f, targetUpSpeed - currentUpSpeed);
        }
    }
}
