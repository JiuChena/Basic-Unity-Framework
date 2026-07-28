using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 在 UnitMover 接管 Rigidbody.useGravity 后统一施加重力并限制最大下落速度。
    /// </summary>
    public sealed class GravityModule
    {
        // 基础重力、下落倍率和下落速度限制配置。
        private readonly GravitySettings _settings;
        // UnitMover 初始化时记录的项目重力基准，运行中不修改全局重力。
        private readonly Vector3 _baseGravity;

        /// <summary>
        /// 初始化重力模块并记录项目当前重力基准。
        /// </summary>
        /// <param name="settings">重力配置。</param>
        /// <param name="baseGravity">运行时接管开始时的项目重力向量。</param>
        public GravityModule(GravitySettings settings, Vector3 baseGravity)
        {
            _settings = settings;
            _baseGravity = baseGravity;
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
            if (isGrounded || _settings == null) return velocity;
            if (_baseGravity.sqrMagnitude <= 0.000001f) return velocity;

            Vector3 gravityDirection = _baseGravity.normalized;
            float downwardSpeed = Vector3.Dot(velocity, gravityDirection);
            float multiplier = downwardSpeed > 0f ? _settings.FallMultiplier : _settings.Multiplier;
            Vector3 result = velocity + _baseGravity * multiplier * fixedDeltaTime;
            float resultDownwardSpeed = Vector3.Dot(result, gravityDirection);

            if (resultDownwardSpeed <= _settings.MaxFallSpeed) return result;
            return result - gravityDirection * (resultDownwardSpeed - _settings.MaxFallSpeed);
        }
    }
}
