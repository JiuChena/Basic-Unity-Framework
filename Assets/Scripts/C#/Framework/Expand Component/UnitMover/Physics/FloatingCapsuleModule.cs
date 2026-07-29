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
    }
}
