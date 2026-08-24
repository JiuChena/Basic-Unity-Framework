using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 保存浮动胶囊的基础形状快照并计算有效胶囊形状。
    /// </summary>
    [Serializable]
    public sealed class FloatingCapsuleAuthoringState
    {
        // 是否已从关联 CapsuleCollider 记录基础形状。
        [Tooltip("是否已从关联 CapsuleCollider 记录基础形状")]
        [HideInInspector] [SerializeField] private bool _captured;
        // 浮动胶囊关闭时应恢复的局部中心。
        [Tooltip("浮动胶囊关闭时应恢复的局部中心")]
        [HideInInspector] [SerializeField] private Vector3 _baseCenter;
        // 浮动胶囊关闭时应恢复的高度。
        [Tooltip("浮动胶囊关闭时应恢复的高度，单位：米")]
        [HideInInspector] [SerializeField] private float _baseHeight;
        // 浮动胶囊关闭时应恢复的半径。
        [Tooltip("浮动胶囊关闭时应恢复的半径，单位：米")]
        [HideInInspector] [SerializeField] private float _baseRadius;
        // 浮动胶囊关闭时应恢复的轴向索引。
        [Tooltip("浮动胶囊关闭时应恢复的轴向索引")]
        [HideInInspector] [SerializeField] private int _baseDirection;
        // 上一次形状计算是否处于浮动胶囊状态。
        [Tooltip("上一次形状计算是否处于浮动胶囊状态")]
        [HideInInspector] [SerializeField] private bool _floatingShapeApplied;
        // 由 ColliderShapeModule 创建和维护的脚底 BoxCollider 引用。
        [Tooltip("由 ColliderShapeModule 创建和维护的脚底 BoxCollider")]
        [HideInInspector] [SerializeField] private BoxCollider _footCollider;

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

        /// <summary>获取当前是否正在使用浮动形状。</summary>
        public bool FloatingShapeApplied => _floatingShapeApplied;

        /// <summary>获取由 ColliderShapeModule 自动维护的脚底 BoxCollider。</summary>
        public BoxCollider FootCollider => _footCollider;

        /// <summary>
        /// 将调用方刚写入的 CapsuleCollider 形状明确记录为新的浮动基础形状。
        /// </summary>
        /// <param name="capsule">已由调用方完成外部形状修改的主胶囊。</param>
        public void RecaptureBaseShape(CapsuleCollider capsule)
        {
            if (capsule == null) return;

            // 显式调用表示当前形状就是作者期望的基础形状，不自动推断其他写入的意图。
            CaptureBaseShape(capsule);
            _floatingShapeApplied = false;
        }

        /// <summary>
        /// 记录由 ColliderShapeModule 创建或销毁的脚底 BoxCollider。
        /// </summary>
        /// <param name="footCollider">当前由形状模块管理的脚底碰撞体；销毁时传入 null。</param>
        public void SetFootCollider(BoxCollider footCollider)
        {
            _footCollider = footCollider;
        }

        /// <summary>
        /// 根据当前开关和留空高度计算有效胶囊形状，并维护作者基础形状快照。
        /// </summary>
        /// <param name="capsule">提供当前作者形状的 CapsuleCollider；为 null 时返回默认形状。</param>
        /// <param name="floatingEnabled">是否需要计算顶部对齐的浮动形状。</param>
        /// <param name="clearance">从基础胶囊底部移除的局部高度，单位：米。</param>
        /// <returns>待由 ColliderShapeModule 写入 Unity 组件的有效形状。</returns>
        internal FloatingCapsuleShape GetEffectiveShape(
            CapsuleCollider capsule,
            bool floatingEnabled,
            float clearance)
        {
            if (capsule == null) return default;

            // 关闭期间持续接收作者编辑；从开启切换到关闭时先返回缓存的基础形状。
            if (!floatingEnabled)
            {
                if (_floatingShapeApplied)
                {
                    _floatingShapeApplied = false;
                    return CreateBaseShape();
                }

                CaptureBaseShape(capsule);
                return CreateBaseShape();
            }

            // 首次开启和重新开启时均以当前作者形状作为基础快照。
            if (!_captured || !_floatingShapeApplied) CaptureBaseShape(capsule);

            // 有效高度不得小于直径，顶部对齐只让底部向上收缩。
            float maximumClearance = Mathf.Max(0f, _baseHeight - _baseRadius * 2f);
            float clampedClearance = Mathf.Clamp(clearance, 0f, maximumClearance);
            Vector3 localAxis = FloatingCapsuleModule.GetCapsuleLocalAxis(_baseDirection);
            _floatingShapeApplied = true;
            return new FloatingCapsuleShape(
                _baseCenter + localAxis * (clampedClearance * 0.5f),
                _baseHeight - clampedClearance,
                _baseRadius,
                _baseDirection);
        }

        /// <summary>
        /// 计算当前实际胶囊相对缓存基础胶囊底部的世界竖直留空高度。
        /// </summary>
        /// <param name="capsule">已由 ColliderShapeModule 写入当前有效形状的 CapsuleCollider。</param>
        /// <returns>实际支撑底部相对基础胶囊底部的世界竖直留空高度，未启用浮动时返回零。</returns>
        public float GetFloatingBottomClearance(CapsuleCollider capsule)
        {
            if (capsule == null || !_captured || !_floatingShapeApplied) return 0f;

            // 使用实际写入后的胶囊形状计算留空，确保缩放时的世界距离正确。
            Vector3 localAxis = FloatingCapsuleModule.GetCapsuleLocalAxis(_baseDirection);
            Vector3 baseBottom = _baseCenter - localAxis * (_baseHeight * 0.5f);
            Vector3 effectiveBottom = capsule.center - localAxis * (capsule.height * 0.5f);
            Vector3 worldOffset = capsule.transform.TransformVector(effectiveBottom - baseBottom);
            return Mathf.Max(0f, Vector3.Dot(worldOffset, Vector3.up));
        }

        /// <summary>
        /// 获取已捕获的基础胶囊形状，并将当前 Authoring 状态标记为未应用浮动形状。
        /// </summary>
        /// <param name="shape">成功时返回应恢复到主胶囊的基础局部形状。</param>
        /// <returns>存在有效基础形状快照时返回 true。</returns>
        internal bool TryRestoreBaseShape(out FloatingCapsuleShape shape)
        {
            shape = default;
            _floatingShapeApplied = false;
            if (!_captured) return false;

            // 形状写入仍由 ColliderShapeModule 负责，AuthoringState 只提供可恢复的纯数据。
            shape = CreateBaseShape();
            return true;
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
        /// 根据已缓存的作者基础数据创建基础胶囊形状。
        /// </summary>
        /// <returns>可直接由 ColliderShapeModule 写入的基础形状。</returns>
        private FloatingCapsuleShape CreateBaseShape()
        {
            return new FloatingCapsuleShape(
                _baseCenter,
                _baseHeight,
                _baseRadius,
                _baseDirection);
        }
    }
}
