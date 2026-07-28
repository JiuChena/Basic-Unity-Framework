using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 定义预测支撑、窄缝确认和意外跌落回退参数。
    /// </summary>
    [Serializable]
    public sealed class EdgeProtectionSettings
    {
        [Tooltip("是否启用预测支撑式边缘防跌落")]
        [SerializeField] private bool _enabled = true;
        [Tooltip("允许自然落到较低可站立地面的最大高度，单位：米")]
        [Min(0f)] [SerializeField] private float _maxFallHeight = 2f;
        [Tooltip("是否在异常跌落且无落脚点时恢复到最近安全位置")]
        [SerializeField] private bool _fallRecoveryEnabled = true;
        [Tooltip("是否只回退非主动跳跃导致的异常跌落")]
        [SerializeField] private bool _recoverUnexpectedFallsOnly = true;
        [Tooltip("允许确认后跨越的最大短缝宽度，单位：米")]
        [Min(0f)] [SerializeField] private float _maxBridgeableGapWidth = 0.15f;

        /// <summary>获取边缘保护是否启用。</summary>
        public bool Enabled => _enabled;
        /// <summary>获取可自然落下的最大高度。</summary>
        public float MaxFallHeight => _maxFallHeight;
        /// <summary>获取异常跌落回退是否启用。</summary>
        public bool FallRecoveryEnabled => _fallRecoveryEnabled;
        /// <summary>获取跳跃阶段是否豁免回退。</summary>
        public bool RecoverUnexpectedFallsOnly => _recoverUnexpectedFallsOnly;
        /// <summary>获取可跨越短缝的最大宽度。</summary>
        public float MaxBridgeableGapWidth => _maxBridgeableGapWidth;
    }
}
