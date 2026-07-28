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

        /// <summary>获取浮动胶囊是否启用。</summary>
        public bool Enabled => _enabled;
        /// <summary>获取底部无碰撞留空高度。</summary>
        public float BottomClearance => _bottomClearance;
    }
}
