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
        /// 创建不共享 Authoring 状态和碰撞体引用的运行时配置副本。
        /// </summary>
        /// <returns>供单个单位运行时使用的独立浮动胶囊配置。</returns>
        public FloatingCapsuleModule CreateRuntimeCopy()
        {
            return new FloatingCapsuleModule
            {
                _enabled = _enabled,
                _bottomClearance = _bottomClearance,
                _footBoxHeight = _footBoxHeight,
                _footBoxSupportWidthScale = _footBoxSupportWidthScale,
                _authoringState = new FloatingCapsuleAuthoringState()
            };
        }

        /// <summary>
        /// 确保反序列化后的模块拥有可写入基础胶囊快照的容器。
        /// </summary>
        public void EnsureAuthoringState()
        {
            if (_authoringState == null) _authoringState = new FloatingCapsuleAuthoringState();
        }

        /// <summary>
        /// 将调用方刚写入的主胶囊形状明确记录为新的浮动基础形状。
        /// </summary>
        /// <param name="capsule">已由调用方完成外部形状修改的主胶囊。</param>
        public void RecaptureBaseShape(CapsuleCollider capsule)
        {
            EnsureAuthoringState();
            _authoringState.RecaptureBaseShape(capsule);
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
}
