using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 在 UnitMover 接管 Rigidbody.useGravity 后统一施加重力并限制最大下落速度。
    /// </summary>
    [Serializable]
    public sealed class GravityModule
    {
        // 基础重力倍率。
        [Tooltip("基础重力倍率，乘以项目 Physics.gravity")]
        [Min(0f)] [SerializeField] private float _multiplier = 1f;
        // 下落阶段使用的额外重力倍率。
        [Tooltip("下落阶段额外使用的重力倍率")]
        [Min(1f)] [SerializeField] private float _fallMultiplier = 1.5f;
        // 沿重力方向允许的最大下落速度。
        [Tooltip("最大下落速度绝对值，单位：米/秒")]
        [Min(0f)] [SerializeField] private float _maxFallSpeed = 25f;
        // UnitMover 初始化时记录的项目重力基准，运行中不修改全局重力。
        [NonSerialized] private Vector3 _baseGravity;

        /// <summary>
        /// 初始化重力模块并记录当前运行时应使用的项目重力基准。
        /// </summary>
        /// <param name="settings">重力配置。</param>
        /// <param name="baseGravity">运行时接管开始时的项目重力向量。</param>
        public void Initialize(Vector3 baseGravity)
        {
            _baseGravity = baseGravity;
        }

        /// <summary>
        /// 清空本组件保存的运行时重力基准，保留 Inspector 配置供下一次运行时重新初始化。
        /// </summary>
        public void ResetRuntimeState()
        {
            _baseGravity = Vector3.zero;
        }

        /// <summary>
        /// 在非稳定接地时施加重力，并限制沿重力方向的最大下落速度。
        /// </summary>
        /// <param name="velocity">尚未施加本步重力的速度。</param>
        /// <param name="isGrounded">当前是否稳定接地。</param>
        /// <param name="fixedDeltaTime">本物理步时长，单位：秒。</param>
        /// <returns>施加重力和下落速度限制后的速度。</returns>
        public Vector3 Apply(Vector3 velocity, bool isGrounded, float fixedDeltaTime)
        {
            if (isGrounded) return velocity;
            if (_baseGravity.sqrMagnitude <= 0.000001f) return velocity;

            Vector3 gravityDirection = _baseGravity.normalized;
            float downwardSpeed = Vector3.Dot(velocity, gravityDirection);
            float multiplier = downwardSpeed > 0f ? _fallMultiplier : _multiplier;
            Vector3 result = velocity + _baseGravity * multiplier * fixedDeltaTime;
            float resultDownwardSpeed = Vector3.Dot(result, gravityDirection);

            if (resultDownwardSpeed <= _maxFallSpeed) return result;
            return result - gravityDirection * (resultDownwardSpeed - _maxFallSpeed);
        }

        /// <summary>创建不共享运行时重力基准的配置副本。</summary>
        /// <returns>供单个移动能力运行时使用的独立重力配置。</returns>
        public GravityModule CreateRuntimeCopy()
        {
            return new GravityModule
            {
                _multiplier = _multiplier,
                _fallMultiplier = _fallMultiplier,
                _maxFallSpeed = _maxFallSpeed
            };
        }
    }
}
