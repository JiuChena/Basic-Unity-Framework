using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 定义接地检测、坡面限制和悬浮弹簧参数。
    /// </summary>
    [Serializable]
    public sealed class GroundSettings
    {
        // 常规悬浮高度的框架级上限，避免将角色支撑到不符合接地语义的高度。
        private const float MaximumHoverHeight = 0.5f;

        [Tooltip("参与地面检测和支撑判断的物理层")]
        [SerializeField] private LayerMask _groundLayer = ~0;
        [Tooltip("允许作为可行走地面的最大坡度，单位：度")]
        [Range(0f, 89f)] [SerializeField] private float _slopeLimit = 45f;
        // 坡度超过可行走限制多少度后才开始确认陡坡，避免边缘三角面瞬时触发约束。
        [Tooltip("坡度高于最大可行走坡度该角度后，才开始确认不可行走陡坡，单位：度")]
        [Range(0f, 15f)] [SerializeField] private float _steepSlopeEnterAngleMargin = 1.5f;
        // 已锁定陡坡后，坡度低于可行走限制多少度才允许退出，形成进入与退出滞回。
        [Tooltip("已锁定陡坡后，坡度低于最大可行走坡度该角度才允许退出约束，单位：度")]
        [Range(0f, 15f)] [SerializeField] private float _steepSlopeExitAngleMargin = 1.5f;
        // 连续命中同类坡面后才切换陡坡状态的确认时间，防止单帧接触波动。
        [Tooltip("连续检测到满足进入或退出条件的坡面后才切换陡坡约束状态的确认时长，单位：秒")]
        [Min(0f)] [SerializeField] private float _steepSlopeContactConfirmTime = 0.06f;
        // 已锁定陡坡时允许短暂丢失接触的时间，避免悬浮和边缘接触使输入约束逐帧反复开关。
        [Tooltip("已锁定陡坡后允许短暂丢失地面接触的时长，单位：秒；期间保持陡坡输入约束和下滑方向")]
        [Min(0f)] [SerializeField] private float _steepSlopeLostContactGraceTime = 0.08f;
        // 坡度超过可站立限制时，根据实际坡度差和曲线倍率计算沿坡速度增量的下滑因数。
        [Tooltip("陡坡下滑速度叠加因数，单位：米/秒²/度；实际超限坡度差、下滑曲线倍率和该值共同决定下坡速度增量，设为 0 时不叠加下坡速度")]
        [FormerlySerializedAs("_steepSlopeSlideAcceleration")]
        [FormerlySerializedAs("_steepSlopeSlideSpeed")]
        [Min(0f)] [SerializeField] private float _steepSlopeSlideFactor = 20f;
        // 将归一化超限坡度差映射为归一化下滑强度的可编辑曲线。
        [Tooltip("横轴为 0 到 1 的归一化超限坡度差，纵轴为 0 到 1 的下滑强度；默认曲线使小坡度差更快起效、高坡度差平缓增长")]
        [SerializeField] private AnimationCurve _steepSlopeSlideCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 2f, 2f),
            new Keyframe(1f, 1f, 0.1f, 0.1f));
        // 陡坡模块可叠加到的沿坡下滑速度上限，避免高坡度造成下滑超速。
        [Tooltip("陡坡模块可叠加到的最大沿坡下滑速度，单位：米/秒；达到该值后不再叠加下坡速度")]
        [Min(0f)] [SerializeField] private float _steepSlopeSlideSpeedLimit = 20f;
        [Tooltip("有效支撑形状底部额外保持的悬浮距离，单位：米，最大为 0.5 米")]
        [Range(0f, MaximumHoverHeight)] [SerializeField] private float _hoverHeight = 0.05f;
        [Tooltip("悬浮距离之外额外用于接地检测的长度，单位：米")]
        [Min(0f)] [SerializeField] private float _probeDistance = 0.3f;
        [Tooltip("悬浮弹簧回正强度")]
        [Min(0f)] [SerializeField] private float _springStrength = 90f;
        [Tooltip("悬浮弹簧沿地面法线的阻尼")]
        [Min(0f)] [SerializeField] private float _springDamping = 14f;

        /// <summary>获取地面层掩码。</summary>
        public LayerMask GroundLayer => _groundLayer;
        /// <summary>获取最大可行走坡度。</summary>
        public float SlopeLimit => _slopeLimit;
        /// <summary>获取进入陡坡约束所需的额外坡度。</summary>
        public float SteepSlopeEnterAngleMargin => _steepSlopeEnterAngleMargin;
        /// <summary>获取退出陡坡约束所需的坡度回落余量。</summary>
        public float SteepSlopeExitAngleMargin => _steepSlopeExitAngleMargin;
        /// <summary>获取陡坡状态切换所需的连续接触确认时长。</summary>
        public float SteepSlopeContactConfirmTime => _steepSlopeContactConfirmTime;
        /// <summary>获取锁定陡坡时允许短暂丢失接触的宽限时长。</summary>
        public float SteepSlopeLostContactGraceTime => _steepSlopeLostContactGraceTime;
        /// <summary>获取陡坡下滑速度叠加因数。</summary>
        public float SteepSlopeSlideFactor => _steepSlopeSlideFactor;
        /// <summary>获取陡坡模块可叠加到的最大沿坡下滑速度。</summary>
        public float SteepSlopeSlideSpeedLimit => _steepSlopeSlideSpeedLimit;

        /// <summary>
        /// 将归一化超限坡度差转换为归一化下滑强度。
        /// </summary>
        /// <param name="normalizedSlopeDifference">相对可行走坡度上限的归一化超限坡度差，范围为 0 到 1。</param>
        /// <returns>经过动画曲线采样并钳制到 0 到 1 的下滑强度。</returns>
        public float EvaluateSteepSlopeSlideRatio(float normalizedSlopeDifference)
        {
            // 旧 Profile 或异常序列化未提供曲线时保持线性映射，保证接地模块始终得到有效比例。
            float normalizedDifference = Mathf.Clamp01(normalizedSlopeDifference);
            if (_steepSlopeSlideCurve == null || _steepSlopeSlideCurve.length == 0) return normalizedDifference;

            return Mathf.Clamp01(_steepSlopeSlideCurve.Evaluate(normalizedDifference));
        }
        /// <summary>获取期望悬浮高度。</summary>
        public float HoverHeight => Mathf.Clamp(_hoverHeight, 0f, MaximumHoverHeight);
        /// <summary>获取额外地面探测长度。</summary>
        public float ProbeDistance => _probeDistance;
        /// <summary>获取悬浮弹簧强度。</summary>
        public float SpringStrength => _springStrength;
        /// <summary>获取悬浮弹簧阻尼。</summary>
        public float SpringDamping => _springDamping;
    }
}
