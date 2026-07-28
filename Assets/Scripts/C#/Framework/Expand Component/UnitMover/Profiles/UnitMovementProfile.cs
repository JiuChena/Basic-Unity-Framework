using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 聚合各个独立运动能力配置的可序列化容器，不承载具体物理逻辑。
    /// </summary>
    [Serializable]
    public sealed class UnitMovementProfile
    {
        // 地面和空中速度规则模块。
        [Tooltip("地面与空中水平速度的模块化配置")]
        [SerializeField] private LocomotionSettings _locomotion = new LocomotionSettings();
        // 普通跳跃规则模块。
        [Tooltip("普通跳跃手感的模块化配置")]
        [SerializeField] private JumpSettings _jump = new JumpSettings();
        // 重力规则模块。
        [Tooltip("重力和最大下落速度的模块化配置")]
        [SerializeField] private GravitySettings _gravity = new GravitySettings();
        // 接地、坡面和悬浮规则模块。
        [Tooltip("接地、坡面和悬浮弹簧的模块化配置")]
        [SerializeField] private GroundSettings _ground = new GroundSettings();
        // 浮动胶囊形状规则模块。
        [Tooltip("顶部对齐浮动胶囊的模块化配置")]
        [SerializeField] private FloatingCapsuleSettings _floatingCapsule = new FloatingCapsuleSettings();
        // 台阶辅助规则模块。
        [Tooltip("自动跨越低台阶的模块化配置")]
        [SerializeField] private StepSettings _step = new StepSettings();
        // 边缘防跌落和安全回退规则模块。
        [Tooltip("预测支撑与异常跌落回退的模块化配置")]
        [SerializeField] private EdgeProtectionSettings _edgeProtection = new EdgeProtectionSettings();

        /// <summary>
        /// 补齐旧 Prefab、旧场景或反序列化异常后缺失的独立配置模块。
        /// </summary>
        internal void EnsureModules()
        {
            // 每个配置组独立补齐，避免旧序列化数据仅缺少其中一组时导致运行时空引用。
            if (_locomotion == null) _locomotion = new LocomotionSettings();
            if (_jump == null) _jump = new JumpSettings();
            if (_gravity == null) _gravity = new GravitySettings();
            if (_ground == null) _ground = new GroundSettings();
            if (_floatingCapsule == null) _floatingCapsule = new FloatingCapsuleSettings();
            if (_step == null) _step = new StepSettings();
            if (_edgeProtection == null) _edgeProtection = new EdgeProtectionSettings();
        }

        /// <summary>获取水平移动速度与加速度配置。</summary>
        public LocomotionSettings Locomotion => _locomotion;

        /// <summary>获取普通跳跃手感配置。</summary>
        public JumpSettings Jump => _jump;

        /// <summary>获取重力行为配置。</summary>
        public GravitySettings Gravity => _gravity;

        /// <summary>获取接地、坡面与悬浮配置。</summary>
        public GroundSettings Ground => _ground;

        /// <summary>获取浮动胶囊形状配置。</summary>
        public FloatingCapsuleSettings FloatingCapsule => _floatingCapsule;

        /// <summary>获取台阶辅助配置。</summary>
        public StepSettings Step => _step;

        /// <summary>获取边缘防跌落配置。</summary>
        public EdgeProtectionSettings EdgeProtection => _edgeProtection;
    }
}
