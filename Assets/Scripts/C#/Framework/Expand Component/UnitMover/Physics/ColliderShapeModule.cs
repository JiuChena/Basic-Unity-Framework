using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 保存单个 UnitMover 对 CapsuleCollider 的基础形状快照，供浮动胶囊恢复使用。
    /// </summary>
    [Serializable]
    public sealed class FloatingCapsuleAuthoringState
    {
        [Tooltip("是否已从关联 CapsuleCollider 记录基础形状")]
        [HideInInspector] [SerializeField] private bool _captured;
        [Tooltip("浮动胶囊关闭时应恢复的局部中心")]
        [HideInInspector] [SerializeField] private Vector3 _baseCenter;
        [Tooltip("浮动胶囊关闭时应恢复的高度，单位：米")]
        [HideInInspector] [SerializeField] private float _baseHeight;
        [Tooltip("浮动胶囊关闭时应恢复的半径，单位：米")]
        [HideInInspector] [SerializeField] private float _baseRadius;
        [Tooltip("浮动胶囊关闭时应恢复的轴向索引")]
        [HideInInspector] [SerializeField] private int _baseDirection;
        [Tooltip("上一次同步是否已将浮动形状写入 CapsuleCollider")]
        [HideInInspector] [SerializeField] private bool _floatingShapeApplied;

        /// <summary>获取是否已记录基础 CapsuleCollider 形状。</summary>
        public bool Captured => _captured;

        /// <summary>获取基础胶囊局部中心，仅在启用浮动时可用于绘制间隙。</summary>
        public Vector3 BaseCenter => _baseCenter;
        /// <summary>获取基础胶囊高度，单位：米。</summary>
        public float BaseHeight => _baseHeight;
        /// <summary>获取基础胶囊半径，单位：米。</summary>
        public float BaseRadius => _baseRadius;
        /// <summary>获取基础胶囊轴向索引。</summary>
        public int BaseDirection => _baseDirection;
        /// <summary>浮动形状是否已写回 CapsuleCollider。</summary>
        public bool FloatingShapeApplied => _floatingShapeApplied;

        /// <summary>
        /// 根据浮动开关同步实际胶囊，并在功能关闭期间持续把作者编辑的形状记录为新的基础形状。
        /// </summary>
        /// <param name="capsule">需要同步形状的 CapsuleCollider。</param>
        /// <param name="floatingEnabled">当前是否启用浮动胶囊。</param>
        /// <param name="clearance">从基础胶囊底部移除的高度，单位：米。</param>
        public void Synchronize(CapsuleCollider capsule, bool floatingEnabled, float clearance)
        {
            if (capsule == null) return;

            if (!floatingEnabled)
            {
                // 从开启切换到关闭时先恢复基础形状；持续关闭时则接收作者的最新编辑。
                if (_floatingShapeApplied)
                    RestoreBaseShape(capsule);
                else
                    CaptureBaseShape(capsule);

                _floatingShapeApplied = false;
                return;
            }

            // 首次启用必须从当前实际 Collider 记录基础形状，避免把旧快照误用于新作者尺寸。
            if (!_captured || !_floatingShapeApplied) CaptureBaseShape(capsule);

            // 高度不能小于两个半径，避免生成无效胶囊。
            float maximumClearance = Mathf.Max(0f, _baseHeight - _baseRadius * 2f);
            float clampedClearance = Mathf.Clamp(clearance, 0f, maximumClearance);
            Vector3 localAxis = ColliderShapeModule.GetCapsuleLocalAxis(_baseDirection);
            capsule.center = _baseCenter + localAxis * (clampedClearance * 0.5f);
            capsule.height = _baseHeight - clampedClearance;
            capsule.radius = _baseRadius;
            capsule.direction = _baseDirection;
            _floatingShapeApplied = true;
        }

        /// <summary>
        /// 将当前未浮动的 CapsuleCollider 形状记录为可恢复的作者基础形状。
        /// </summary>
        /// <param name="capsule">当前由作者调整后的 CapsuleCollider。</param>
        private void CaptureBaseShape(CapsuleCollider capsule)
        {
            _baseCenter = capsule.center;
            _baseHeight = capsule.height;
            _baseRadius = capsule.radius;
            _baseDirection = capsule.direction;
            _captured = true;
        }

        /// <summary>
        /// 将最近一次记录的基础形状恢复到目标 CapsuleCollider。
        /// </summary>
        /// <param name="capsule">需要恢复形状的 CapsuleCollider。</param>
        private void RestoreBaseShape(CapsuleCollider capsule)
        {
            if (!_captured) return;

            capsule.center = _baseCenter;
            capsule.height = _baseHeight;
            capsule.radius = _baseRadius;
            capsule.direction = _baseDirection;
        }
    }

    /// <summary>
    /// 统一同步浮动胶囊形状，并向所有物理模块提供实际参与检测的 Collider 边界数据。
    /// </summary>
    public sealed class ColliderShapeModule
    {
        // 参与移动和物理检测的实际碰撞体。
        private readonly Collider _movementCollider;
        // 浮动胶囊的可序列化开关与留空高度。
        private readonly FloatingCapsuleSettings _settings;
        // 组件级基础胶囊快照。
        private readonly FloatingCapsuleAuthoringState _authoringState;

        /// <summary>
        /// 初始化与单个 Collider 绑定的形状模块。
        /// </summary>
        /// <param name="movementCollider">参与移动检测的 CapsuleCollider 或 BoxCollider。</param>
        /// <param name="settings">浮动胶囊配置。</param>
        /// <param name="authoringState">需要跨编辑器重载保存的基础胶囊快照。</param>
        public ColliderShapeModule(
            Collider movementCollider,
            FloatingCapsuleSettings settings,
            FloatingCapsuleAuthoringState authoringState)
        {
            _movementCollider = movementCollider;
            _settings = settings;
            _authoringState = authoringState;
        }

        /// <summary>获取实际参与物理查询的碰撞体。</summary>
        public Collider MovementCollider => _movementCollider;

        /// <summary>获取当前绑定的浮动胶囊配置。</summary>
        public FloatingCapsuleSettings FloatingCapsuleSettings => _settings;

        /// <summary>获取与浮动胶囊关联的基础形状快照。</summary>
        public FloatingCapsuleAuthoringState AuthoringState => _authoringState;

        /// <summary>获取当前有效碰撞体的世界边界。</summary>
        public Bounds Bounds => _movementCollider != null ? _movementCollider.bounds : new Bounds();

        /// <summary>
        /// 获取浮动胶囊相对基础胶囊底部实际抬升的世界竖直距离。
        /// </summary>
        /// <returns>当前有效胶囊底部相对基础胶囊底部的竖直留空高度，单位：米。</returns>
        public float GetFloatingBottomClearance()
        {
            if (!(_movementCollider is CapsuleCollider capsule)) return 0f;
            if (_authoringState == null || !_authoringState.Captured
                || !_authoringState.FloatingShapeApplied) return 0f;

            // 以基础和有效胶囊的真实底部差值为准，保证缩放后的支撑距离仍然正确。
            Vector3 localAxis = GetCapsuleLocalAxis(_authoringState.BaseDirection);
            Vector3 baseBottom = _authoringState.BaseCenter
                - localAxis * (_authoringState.BaseHeight * 0.5f);
            Vector3 effectiveBottom = capsule.center - localAxis * (capsule.height * 0.5f);
            Vector3 worldOffset = capsule.transform.TransformVector(effectiveBottom - baseBottom);
            return Mathf.Max(0f, Vector3.Dot(worldOffset, Vector3.up));
        }

        /// <summary>
        /// 同步基础或浮动后的胶囊形状；BoxCollider 保持原始形状并继续受支持。
        /// </summary>
        public void Synchronize()
        {
            if (!(_movementCollider is CapsuleCollider capsule)) return;
            if (_authoringState == null) return;

            _authoringState.Synchronize(
                capsule,
                _settings != null && _settings.Enabled,
                _settings != null ? _settings.BottomClearance : 0f);
        }

        /// <summary>
        /// 计算给定世界水平移动方向上的 Collider 前缘半径或半宽。
        /// </summary>
        /// <param name="worldDirection">需要评估的世界空间方向。</param>
        /// <returns>从 Collider 中心到该方向边缘的世界距离。</returns>
        public float GetHorizontalExtent(Vector3 worldDirection)
        {
            if (_movementCollider == null) return 0f;

            Vector3 direction = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            if (direction.sqrMagnitude <= 0.000001f) return 0f;

            direction.Normalize();
            Vector3 extents = _movementCollider.bounds.extents;
            return Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.z) * extents.z;
        }

        /// <summary>
        /// 获取用于脚底宽体确认的保守球半径。
        /// </summary>
        /// <returns>不大于 Collider 水平最小半宽的球半径。</returns>
        public float GetFootSupportRadius()
        {
            if (_movementCollider == null) return 0f;

            Bounds bounds = _movementCollider.bounds;
            return Mathf.Max(0.001f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.9f);
        }

        /// <summary>
        /// 获取 CapsuleCollider.direction 对应的局部单位轴。
        /// </summary>
        /// <param name="direction">CapsuleCollider 的轴向索引。</param>
        /// <returns>对应的局部坐标轴。</returns>
        public static Vector3 GetCapsuleLocalAxis(int direction)
        {
            switch (direction)
            {
                case 0:
                    return Vector3.right;
                case 1:
                    return Vector3.up;
                default:
                    return Vector3.forward;
            }
        }
    }
}
