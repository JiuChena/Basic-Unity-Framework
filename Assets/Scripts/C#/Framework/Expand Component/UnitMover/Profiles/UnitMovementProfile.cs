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
        // 接地、坡面和悬浮规则模块。
        [Tooltip("接地、坡面和悬浮弹簧的模块化配置")]
        [SerializeField] private GroundSettings _ground = new GroundSettings();

        /// <summary>
        /// 补齐旧 Prefab、旧场景或反序列化异常后缺失的独立配置模块。
        /// </summary>
        internal void EnsureModules()
        {
            // 每个配置组独立补齐，避免旧序列化数据仅缺少其中一组时导致运行时空引用。
            if (_locomotion == null) _locomotion = new LocomotionSettings();
            if (_ground == null) _ground = new GroundSettings();
        }

        /// <summary>获取水平移动速度与加速度配置。</summary>
        public LocomotionSettings Locomotion => _locomotion;

        /// <summary>获取接地、坡面与悬浮配置。</summary>
        public GroundSettings Ground => _ground;

    }
}
