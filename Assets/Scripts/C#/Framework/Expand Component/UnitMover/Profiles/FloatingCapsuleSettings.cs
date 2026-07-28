using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 定义 CapsuleCollider 顶部不动、仅向上抬升底部的浮动胶囊规则。
    /// </summary>
    [Serializable]
    public sealed class FloatingCapsuleSettings
    {
        [Tooltip("是否启用顶部对齐的浮动胶囊体")]
        [SerializeField] private bool _enabled = false;
        [Tooltip("从胶囊底部移除的碰撞高度，单位：米")]
        [Min(0f)] [SerializeField] private float _bottomClearance = 0.4f;
        [Tooltip("胶囊底部自动生成的扁平脚底 BoxCollider 厚度，单位：米。Box 底面与有效胶囊底面对齐，厚度会限制在胶囊半径内")]
        [Min(0f)] [SerializeField] private float _footBoxHeight = 0.05f;
        [Tooltip("脚底 BoxCollider 用于悬浮接地检测的水平宽度比例。物理碰撞仍使用完整宽度，缩小此值可避免前缘碰到高台阶时被悬浮系统托上去")]
        [Range(0.1f, 1f)] [SerializeField] private float _footBoxSupportWidthScale = 0.7f;

        /// <summary>获取浮动胶囊是否启用。</summary>
        public bool Enabled => _enabled;
        /// <summary>获取底部无碰撞留空高度。</summary>
        public float BottomClearance => _bottomClearance;
        /// <summary>获取脚底 BoxCollider 的竖直厚度。</summary>
        public float FootBoxHeight => _footBoxHeight;
        /// <summary>获取脚底 BoxCollider 进行接地检测时的水平宽度比例。</summary>
        public float FootBoxSupportWidthScale => Mathf.Clamp(_footBoxSupportWidthScale, 0.1f, 1f);
    }
}
