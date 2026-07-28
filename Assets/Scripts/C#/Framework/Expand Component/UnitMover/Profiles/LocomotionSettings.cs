using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 定义地面和空中模式共用的水平速度规则。
    /// </summary>
    [Serializable]
    public sealed class LocomotionSettings
    {
        [Tooltip("地面最大移动速度，单位：米/秒")]
        [Min(0f)] [SerializeField] private float _groundMaxSpeed = 5f;
        [Tooltip("地面有输入时接近目标速度的加速度，单位：米/秒²")]
        [Min(0f)] [SerializeField] private float _groundAcceleration = 45f;
        [Tooltip("地面无输入或反向时的减速度，单位：米/秒²")]
        [Min(0f)] [SerializeField] private float _groundDeceleration = 55f;
        [Tooltip("空中最大水平移动速度，单位：米/秒")]
        [Min(0f)] [SerializeField] private float _airMaxSpeed = 6f;
        [Tooltip("空中接近目标速度的加速度，单位：米/秒²")]
        [Min(0f)] [SerializeField] private float _airAcceleration = 15f;
        [Tooltip("空中可改变水平速度的比例，范围：0-1")]
        [Range(0f, 1f)] [SerializeField] private float _airControl = 0.45f;

        /// <summary>获取地面最大移动速度。</summary>
        public float GroundMaxSpeed => _groundMaxSpeed;
        /// <summary>获取地面加速度。</summary>
        public float GroundAcceleration => _groundAcceleration;
        /// <summary>获取地面减速度。</summary>
        public float GroundDeceleration => _groundDeceleration;
        /// <summary>获取空中最大移动速度。</summary>
        public float AirMaxSpeed => _airMaxSpeed;
        /// <summary>获取空中加速度。</summary>
        public float AirAcceleration => _airAcceleration;
        /// <summary>获取空中控制比例。</summary>
        public float AirControl => _airControl;
    }
}
