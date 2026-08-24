using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 基于预测脚底支撑约束目标速度和已有外向速度，并保存业务层显式检查点。
    /// </summary>
    [Serializable]
    public sealed class EdgeProtectionModule
    {
        // 前缘预测距离中保留的微小安全边距。
        private const float SkinWidth = 0.02f;
        // 局部边缘方向定位时使用的固定危险采样数量。
        private const int HazardSampleCount = 8;
        // 提供有效 Collider 边界和脚底半径的形状模块。
        [NonSerialized] private ColliderShapeModule _shapeModule;
        // 复用统一地面过滤规则的接地模块。
        [NonSerialized] private GroundProbeModule _groundProbe;
        // 是否启用预测支撑式边缘防跌落。
        [Tooltip("是否启用预测支撑式边缘防跌落")]
        [SerializeField] private bool _enabled = true;
        // 支撑采样允许向下确认较低可行走地面的最大高度。
        [Tooltip("支撑检测允许向下确认较低可行走地面的最大高度，单位：米")]
        [Min(0f)] [SerializeField] private float _maxFallHeight = 2f;
        // 允许确认后跨越的最大短缝宽度。
        [Tooltip("允许确认后跨越的最大短缝宽度，单位：米")]
        [Min(0f)] [SerializeField] private float _maxBridgeableGapWidth = 0.15f;
        // 供 Scene Gizmos 读取的固定缓冲诊断数据。
        [NonSerialized] private readonly EdgeProtectionDebugState _debugState = new EdgeProtectionDebugState();
        // 当前是否存在业务层显式记录的检查点。
        private bool _hasCheckpoint;
        // 当前业务层显式记录的检查点位置和旋转。
        private CheckpointSnapshot _checkpoint;

        /// <summary>
        /// 初始化边缘保护模块。
        /// </summary>
        /// <param name="shapeModule">提供当前有效 Collider 尺寸的模块。</param>
        /// <param name="groundProbe">提供统一地面判断的模块。</param>
        /// <param name="settings">边缘保护配置。</param>
        public void Initialize(
            ColliderShapeModule shapeModule,
            GroundProbeModule groundProbe)
        {
            _shapeModule = shapeModule;
            _groundProbe = groundProbe;
        }

        /// <summary>获取最近一次边缘保护调试数据。</summary>
        public EdgeProtectionDebugState DebugState => _debugState;

        /// <summary>获取边缘防跌落是否启用。</summary>
        public bool IsEnabled => _enabled;

        /// <summary>
        /// 清空检查点和 Gizmo 诊断等全部运行时状态，保留 Inspector 配置。
        /// </summary>
        public void ResetRuntimeState()
        {
            _hasCheckpoint = false;
            _checkpoint = default;
            _debugState.ClearRayData();
            _debugState.EdgeOutNormal = Vector3.zero;
            _debugState.ConstrainedVelocity = Vector3.zero;
            _debugState.SupportStatus = SupportStatus.Unsupported;
        }

        /// <summary>
        /// 由业务层显式记录可恢复的检查点位置。
        /// </summary>
        /// <param name="position">需要恢复的刚体世界位置。</param>
        /// <param name="rotation">需要恢复的刚体世界旋转。</param>
        public void SetCheckpoint(Vector3 position, Quaternion rotation)
        {
            _checkpoint = new CheckpointSnapshot(position, rotation);
            _hasCheckpoint = true;
        }

        /// <summary>
        /// 获取当前显式记录的检查点。
        /// </summary>
        /// <param name="checkpoint">存在检查点时返回对应的位置快照。</param>
        /// <returns>是否存在可恢复的检查点。</returns>
        public bool TryGetCheckpoint(out CheckpointSnapshot checkpoint)
        {
            checkpoint = _checkpoint;
            return _hasCheckpoint;
        }

        /// <summary>
        /// 预测目标速度前缘的三点支撑并在必要时删除通向悬崖外侧的速度分量。
        /// </summary>
        /// <param name="state">当前运动状态。</param>
        /// <param name="candidateVelocity">尚未约束的候选水平速度。</param>
        /// <param name="currentVelocity">当前刚体水平速度。</param>
        /// <param name="fixedDeltaTime">本物理步时长，单位：秒。</param>
        /// <param name="constrainedCandidate">返回经过边缘保护的候选水平速度。</param>
        /// <param name="constrainedCurrent">返回已移除外向惯性的当前水平速度。</param>
        public void ConstrainVelocity(
            in UnitMovementState state,
            Vector3 candidateVelocity,
            Vector3 currentVelocity,
            float fixedDeltaTime,
            out Vector3 constrainedCandidate,
            out Vector3 constrainedCurrent)
        {
            constrainedCandidate = candidateVelocity;
            constrainedCurrent = currentVelocity;
            _debugState.ClearRayData();
            _debugState.EdgeOutNormal = Vector3.zero;
            _debugState.ConstrainedVelocity = candidateVelocity;

            if (!_enabled || !state.IsStableGrounded) return;

            // 候选速度不能代表外力或反向输入时，使用当前实际水平速度继续预测外向运动。
            Vector3 predictionVelocity = SelectPredictionVelocity(candidateVelocity, currentVelocity);
            if (predictionVelocity.sqrMagnitude <= 0.000001f) return;

            Vector3 direction = predictionVelocity.normalized;
            SupportStatus supportStatus = EvaluatePredictedSupport(direction, candidateVelocity, currentVelocity, fixedDeltaTime);
            _debugState.SupportStatus = supportStatus;
            if (supportStatus == SupportStatus.Stable) return;
            if (TryEvaluateBridgeableGap(direction, candidateVelocity, currentVelocity, fixedDeltaTime)) return;

            Vector3 edgeOutNormal = TryResolveEdgeOutNormal();
            if (edgeOutNormal.sqrMagnitude <= 0.000001f)
            {
                constrainedCandidate = Vector3.zero;
                _debugState.ConstrainedVelocity = constrainedCandidate;
                return;
            }

            constrainedCandidate = RemoveOutwardComponent(candidateVelocity, edgeOutNormal);
            constrainedCurrent = RemoveOutwardComponent(currentVelocity, edgeOutNormal);
            if (HasStableSupportForVelocity(constrainedCandidate, constrainedCurrent, fixedDeltaTime))
            {
                _debugState.EdgeOutNormal = edgeOutNormal;
                _debugState.ConstrainedVelocity = constrainedCandidate;
                return;
            }

            Vector3 tangent = Vector3.Cross(Vector3.up, edgeOutNormal).normalized;
            Vector3 tangentCandidate = SelectSupportedTangent(
                tangent,
                candidateVelocity,
                constrainedCurrent,
                fixedDeltaTime);
            constrainedCandidate = tangentCandidate;
            _debugState.EdgeOutNormal = edgeOutNormal;
            _debugState.ConstrainedVelocity = constrainedCandidate;
        }

        /// <summary>
        /// 评估当前候选方向预测前缘的左、中、右三点支撑状态。
        /// </summary>
        /// <param name="direction">归一化后的候选水平移动方向。</param>
        /// <param name="candidateVelocity">候选水平速度。</param>
        /// <param name="currentVelocity">当前水平速度。</param>
        /// <param name="fixedDeltaTime">本物理步时长，单位：秒。</param>
        /// <returns>三点截面的稳定、非稳定或无支撑结果。</returns>
        private SupportStatus EvaluatePredictedSupport(
            Vector3 direction,
            Vector3 candidateVelocity,
            Vector3 currentVelocity,
            float fixedDeltaTime)
        {
            if (_shapeModule == null || _groundProbe == null) return SupportStatus.Unsupported;

            Bounds bounds = _shapeModule.Bounds;
            float horizontalSpeed = Mathf.Max(candidateVelocity.magnitude, currentVelocity.magnitude);
            float predictionDistance = horizontalSpeed * fixedDeltaTime + _shapeModule.GetHorizontalExtent(direction) + SkinWidth;
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            float lateralOffset = Mathf.Min(_shapeModule.GetHorizontalExtent(right) * 0.7f, _shapeModule.GetFootSupportRadius());
            Vector3 frontCenter = bounds.center + direction * predictionDistance;
            float rayDistance = bounds.size.y + _groundProbe.HoverHeight + _maxFallHeight + SkinWidth;
            Vector3 rayOrigin = new Vector3(frontCenter.x, bounds.max.y + SkinWidth, frontCenter.z);
            _debugState.SetSupportRayDistance(rayDistance);

            bool middleSupported = TrySampleSupport(rayOrigin, rayDistance, 1);
            bool leftSupported = TrySampleSupport(rayOrigin - right * lateralOffset, rayDistance, 0);
            bool rightSupported = TrySampleSupport(rayOrigin + right * lateralOffset, rayDistance, 2);

            if (middleSupported && (leftSupported || rightSupported)) return SupportStatus.Stable;
            if (middleSupported || leftSupported || rightSupported) return SupportStatus.Unstable;
            return SupportStatus.Unsupported;
        }

        /// <summary>
        /// 在有限短缝窗口内查找重新稳定的支撑截面并用脚底球体再次确认。
        /// </summary>
        /// <param name="direction">原始候选移动方向。</param>
        /// <param name="candidateVelocity">候选水平速度。</param>
        /// <param name="currentVelocity">当前水平速度。</param>
        /// <param name="fixedDeltaTime">本物理步时长，单位：秒。</param>
        /// <returns>是否确认到可安全跨越的短缝出口。</returns>
        private bool TryEvaluateBridgeableGap(
            Vector3 direction,
            Vector3 candidateVelocity,
            Vector3 currentVelocity,
            float fixedDeltaTime)
        {
            if (_maxBridgeableGapWidth <= 0f) return false;
            if (_shapeModule == null || _groundProbe == null) return false;

            float step = Mathf.Max(0.02f, _maxBridgeableGapWidth * 0.5f);
            Vector3 originalCenter = _shapeModule.Bounds.center;

            // 仅在紧邻失去支撑后向前扫描有限距离，不进行全方向持续检测。
            for (float offset = step; offset <= _maxBridgeableGapWidth; offset += step)
            {
                if (!HasStableSupportAt(originalCenter + direction * offset, direction, candidateVelocity, currentVelocity, fixedDeltaTime))
                    continue;

                Bounds bounds = _shapeModule.Bounds;
                Vector3 sphereOrigin = new Vector3(
                    originalCenter.x + direction.x * offset,
                    bounds.min.y + _shapeModule.GetFootSupportRadius() + SkinWidth,
                    originalCenter.z + direction.z * offset);
                float sphereDistance = _shapeModule.GetFootSupportRadius() + _groundProbe.HoverHeight + SkinWidth;
                if (_groundProbe.HasWalkableSphereSupport(
                        sphereOrigin,
                        _shapeModule.GetFootSupportRadius(),
                        sphereDistance))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 判断指定候选速度是否仍可通过预测支撑检测。
        /// </summary>
        /// <param name="candidateVelocity">待验证的候选水平速度。</param>
        /// <param name="currentVelocity">当前经过约束的水平速度。</param>
        /// <param name="fixedDeltaTime">本物理步时长，单位：秒。</param>
        /// <returns>候选速度是否拥有稳定支撑。</returns>
        private bool HasStableSupportForVelocity(Vector3 candidateVelocity, Vector3 currentVelocity, float fixedDeltaTime)
        {
            Vector3 predictionVelocity = SelectPredictionVelocity(candidateVelocity, currentVelocity);
            if (predictionVelocity.sqrMagnitude <= 0.000001f) return true;
            return EvaluatePredictedSupport(predictionVelocity.normalized, candidateVelocity, currentVelocity, fixedDeltaTime)
                == SupportStatus.Stable;
        }

        /// <summary>
        /// 选择本物理步最能代表可能越界位移的水平速度。
        /// </summary>
        /// <param name="candidateVelocity">策略计算出的候选水平速度。</param>
        /// <param name="currentVelocity">刚体当前实际水平速度。</param>
        /// <returns>速度较大的一方；两者均近零时返回零。</returns>
        private static Vector3 SelectPredictionVelocity(Vector3 candidateVelocity, Vector3 currentVelocity)
        {
            return currentVelocity.sqrMagnitude > candidateVelocity.sqrMagnitude
                ? currentVelocity
                : candidateVelocity;
        }

        /// <summary>
        /// 以指定中心点评估短缝出口处的三点支撑状态。
        /// </summary>
        /// <param name="center">预测出口的 Collider 中心。</param>
        /// <param name="direction">原始候选移动方向。</param>
        /// <param name="candidateVelocity">候选水平速度。</param>
        /// <param name="currentVelocity">当前水平速度。</param>
        /// <param name="fixedDeltaTime">本物理步时长，单位：秒。</param>
        /// <returns>出口截面是否满足稳定支撑规则。</returns>
        private bool HasStableSupportAt(
            Vector3 center,
            Vector3 direction,
            Vector3 candidateVelocity,
            Vector3 currentVelocity,
            float fixedDeltaTime)
        {
            Bounds bounds = _shapeModule.Bounds;
            float horizontalSpeed = Mathf.Max(candidateVelocity.magnitude, currentVelocity.magnitude);
            float predictionDistance = horizontalSpeed * fixedDeltaTime + _shapeModule.GetHorizontalExtent(direction) + SkinWidth;
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            float lateralOffset = Mathf.Min(_shapeModule.GetHorizontalExtent(right) * 0.7f, _shapeModule.GetFootSupportRadius());
            Vector3 frontCenter = center + direction * predictionDistance;
            float rayDistance = bounds.size.y + _groundProbe.HoverHeight + _maxFallHeight + SkinWidth;
            Vector3 rayOrigin = new Vector3(frontCenter.x, bounds.max.y + SkinWidth, frontCenter.z);

            bool middleSupported = _groundProbe.TryGetWalkableGround(rayOrigin, Vector3.down, rayDistance, out _);
            bool leftSupported = _groundProbe.TryGetWalkableGround(rayOrigin - right * lateralOffset, Vector3.down, rayDistance, out _);
            bool rightSupported = _groundProbe.TryGetWalkableGround(rayOrigin + right * lateralOffset, Vector3.down, rayDistance, out _);
            return middleSupported && (leftSupported || rightSupported);
        }

        /// <summary>
        /// 执行一次预测支撑采样并保存结果供 Gizmos 显示。
        /// </summary>
        /// <param name="origin">向下采样射线的起点。</param>
        /// <param name="distance">采样射线最大长度。</param>
        /// <param name="index">诊断缓冲区索引。</param>
        /// <returns>该采样点是否存在可行走支撑。</returns>
        private bool TrySampleSupport(Vector3 origin, float distance, int index)
        {
            bool supported = _groundProbe.TryGetWalkableGround(origin, Vector3.down, distance, out _);
            _debugState.SupportPoints[index] = origin;
            _debugState.SupportResults[index] = supported;
            return supported;
        }

        /// <summary>
        /// 仅在确认无稳定支撑后局部扫描脚底周围，估算指向无支撑区域的边缘外法线。
        /// </summary>
        /// <returns>有足够危险样本时返回归一化外法线，否则返回零向量。</returns>
        private Vector3 TryResolveEdgeOutNormal()
        {
            if (_shapeModule == null || _groundProbe == null) return Vector3.zero;

            Bounds bounds = _shapeModule.Bounds;
            float radialDistance = _shapeModule.GetFootSupportRadius();
            float rayDistance = bounds.size.y + _groundProbe.HoverHeight + _maxFallHeight + SkinWidth;
            Vector3 rayBase = new Vector3(bounds.center.x, bounds.max.y + SkinWidth, bounds.center.z);
            _debugState.SetHazardRayDistance(rayDistance);
            Vector3 hazardSum = Vector3.zero;
            int hazardCount = 0;

            // 危险采样仅在前缘支撑已失败时执行，避免常态全周扫描开销。
            for (int index = 0; index < HazardSampleCount; index++)
            {
                float angle = index * 360f / HazardSampleCount;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 origin = rayBase + direction * radialDistance;
                bool supported = _groundProbe.TryGetWalkableGround(origin, Vector3.down, rayDistance, out _);

                _debugState.HazardPoints[index] = origin;
                _debugState.HazardResults[index] = !supported;
                if (supported) continue;

                hazardSum += direction;
                hazardCount++;
            }

            if (hazardCount < 2 || hazardSum.sqrMagnitude <= 0.000001f) return Vector3.zero;
            return Vector3.ProjectOnPlane(hazardSum, Vector3.up).normalized;
        }

        /// <summary>
        /// 从速度中移除沿悬崖外法线的正向分量，保留返回平台或沿边运动的分量。
        /// </summary>
        /// <param name="velocity">需要约束的水平速度。</param>
        /// <param name="edgeOutNormal">指向无支撑区域的归一化方向。</param>
        /// <returns>移除外向速度分量后的结果。</returns>
        private static Vector3 RemoveOutwardComponent(Vector3 velocity, Vector3 edgeOutNormal)
        {
            float outwardSpeed = Vector3.Dot(velocity, edgeOutNormal);
            return outwardSpeed > 0f ? velocity - edgeOutNormal * outwardSpeed : velocity;
        }

        /// <summary>
        /// 在两个沿边候选速度中选择与原始输入最相符且通过支撑验证的方向。
        /// </summary>
        /// <param name="tangent">归一化后的边缘切线方向。</param>
        /// <param name="originalCandidateVelocity">未经边缘约束的原始候选速度，用于保留输入方向偏好。</param>
        /// <param name="currentVelocity">当前经过外向分量剔除的速度。</param>
        /// <param name="fixedDeltaTime">本物理步时长，单位：秒。</param>
        /// <returns>通过验证的沿边候选速度；均失败时返回零向量。</returns>
        private Vector3 SelectSupportedTangent(
            Vector3 tangent,
            Vector3 originalCandidateVelocity,
            Vector3 currentVelocity,
            float fixedDeltaTime)
        {
            float speed = originalCandidateVelocity.magnitude;
            Vector3 positiveCandidate = tangent * speed;
            Vector3 negativeCandidate = -tangent * speed;
            bool positiveSupported = HasStableSupportForVelocity(positiveCandidate, currentVelocity, fixedDeltaTime);
            bool negativeSupported = HasStableSupportForVelocity(negativeCandidate, currentVelocity, fixedDeltaTime);

            if (positiveSupported && !negativeSupported) return positiveCandidate;
            if (!positiveSupported && negativeSupported) return negativeCandidate;
            if (!positiveSupported) return Vector3.zero;

            return Vector3.Dot(positiveCandidate, originalCandidateVelocity)
                >= Vector3.Dot(negativeCandidate, originalCandidateVelocity)
                ? positiveCandidate
                : negativeCandidate;
        }
    }
}
