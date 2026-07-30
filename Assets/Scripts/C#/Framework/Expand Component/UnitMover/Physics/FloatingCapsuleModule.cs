using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 聚合浮动胶囊参数和组件专属基础形状快照的可序列化 Authoring 模块。
    /// </summary>
    [Serializable]
    public sealed class FloatingCapsuleModule
    {
        // 是否启用顶部对齐的浮动胶囊。
        [Tooltip("是否启用顶部对齐的浮动胶囊体")]
        [SerializeField] private bool _enabled;
        // 从胶囊底部移除的碰撞高度，也是最大可通过台阶高度。
        [Tooltip("从胶囊底部移除的碰撞高度，同时是最大可通过台阶高度，单位：米；实际值受胶囊最小高度限制")]
        [Min(0f)] [SerializeField] private float _bottomClearance = 0.4f;
        // 自动生成脚底 BoxCollider 的竖直厚度。
        [Tooltip("胶囊底部自动生成的扁平脚底 BoxCollider 厚度，单位：米。Box 底面与有效胶囊底面对齐，厚度会限制在胶囊半径内")]
        [Min(0f)] [SerializeField] private float _footBoxHeight = 0.05f;
        // 接地检测相对脚底 BoxCollider 使用的水平宽度比例。
        [Tooltip("脚底 BoxCollider 用于悬浮接地检测的水平宽度比例。物理碰撞仍使用完整宽度，缩小此值可避免前缘碰到高台阶时被悬浮系统托上去")]
        [Range(0.1f, 1f)] [SerializeField] private float _footBoxSupportWidthScale = 0.7f;
        // 关闭浮动时需要恢复的组件专属基础 CapsuleCollider 形状。
        [Tooltip("保存浮动胶囊关闭时需要恢复的基础 CapsuleCollider 形状")]
        [SerializeField] private FloatingCapsuleAuthoringState _authoringState = new FloatingCapsuleAuthoringState();

        /// <summary>获取浮动胶囊是否启用。</summary>
        public bool Enabled => _enabled;

        /// <summary>获取底部无碰撞留空与最大可通过台阶高度。</summary>
        public float BottomClearance => _bottomClearance;

        /// <summary>获取脚底 BoxCollider 的竖直厚度。</summary>
        public float FootBoxHeight => _footBoxHeight;

        /// <summary>获取脚底 BoxCollider 接地检测使用的水平宽度比例。</summary>
        public float FootBoxSupportWidthScale => Mathf.Clamp(_footBoxSupportWidthScale, 0.1f, 1f);

        /// <summary>获取组件专属的基础形状快照。</summary>
        public FloatingCapsuleAuthoringState AuthoringState => _authoringState;

        /// <summary>
        /// 确保反序列化后的模块拥有可写入基础胶囊快照的容器。
        /// </summary>
        public void EnsureAuthoringState()
        {
            if (_authoringState == null) _authoringState = new FloatingCapsuleAuthoringState();
        }

        /// <summary>
        /// 根据当前 Authoring 配置生成应写入主胶囊碰撞体的有效形状。
        /// </summary>
        /// <param name="capsule">提供作者基础形状的主 CapsuleCollider；为 null 时返回默认形状。</param>
        /// <returns>基础形状或顶部对齐后的浮动形状，不会写入任何 Unity 组件。</returns>
        internal FloatingCapsuleShape GetEffectiveShape(CapsuleCollider capsule)
        {
            EnsureAuthoringState();
            return _authoringState.GetEffectiveShape(capsule, _enabled, _bottomClearance);
        }

        /// <summary>
        /// 计算当前实际胶囊相对基础胶囊底部的世界竖直留空高度。
        /// </summary>
        /// <param name="capsule">已经由 ColliderShapeModule 写入有效形状的主 CapsuleCollider。</param>
        /// <returns>实际支撑底部的世界竖直留空高度，未应用浮动形状时返回零。</returns>
        internal float GetFloatingBottomClearance(CapsuleCollider capsule)
        {
            return _authoringState != null
                ? _authoringState.GetFloatingBottomClearance(capsule)
                : 0f;
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
