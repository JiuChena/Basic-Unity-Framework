using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 定义自动跨越低台阶的辅助参数。
    /// </summary>
    [Serializable]
    public sealed class StepSettings
    {
        [Tooltip("允许自动辅助跨越的最大台阶高度，单位：米")]
        [Min(0f)] [SerializeField] private float _maxHeight = 0.3f;
        [Tooltip("台阶前方探测时额外保留的距离，单位：米")]
        [Min(0f)] [SerializeField] private float _probePadding = 0.08f;
        [Tooltip("台阶辅助允许附加的最大向上速度，单位：米/秒")]
        [Min(0f)] [SerializeField] private float _maxUpSpeed = 4f;

        /// <summary>获取最大自动台阶高度。</summary>
        public float MaxHeight => _maxHeight;
        /// <summary>获取台阶前探测留边。</summary>
        public float ProbePadding => _probePadding;
        /// <summary>获取最大台阶向上速度。</summary>
        public float MaxUpSpeed => _maxUpSpeed;
    }
}
