using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 基于统一接地结果沿地面法线计算悬浮弹簧速度修正。
    /// </summary>
    public sealed class HoverModule
    {
        // 接地探测距离和悬浮弹簧配置。
        private readonly GroundSettings _settings;
        // 统一计算有效支撑形状的目标支撑距离。
        private readonly GroundProbeModule _groundProbe;

        /// <summary>
        /// 创建悬浮弹簧模块。
        /// </summary>
        /// <param name="settings">接地和悬浮配置。</param>
        /// <param name="groundProbe">提供浮动胶囊总支撑距离的接地探测模块。</param>
        public HoverModule(GroundSettings settings, GroundProbeModule groundProbe)
        {
            _settings = settings;
            _groundProbe = groundProbe;
        }

        /// <summary>
        /// 在可行走地面上沿法线施加弹簧和阻尼修正。
        /// </summary>
        /// <param name="velocity">尚未施加悬浮修正的速度。</param>
        /// <param name="state">当前地面状态。</param>
        /// <param name="fixedDeltaTime">本物理步时长，单位：秒。</param>
        /// <returns>加入弹簧速度修正后的速度。</returns>
        public Vector3 Apply(Vector3 velocity, in UnitMovementState state, float fixedDeltaTime)
        {
            if (_settings == null || !state.IsGrounded) return velocity;

            // 浮动胶囊的支撑留空属于物理支撑目标，不能仅作为外形修改后被弹簧重新压回地面。
            float targetDistance = _groundProbe != null
                ? _groundProbe.DesiredGroundDistance
                : _settings.HoverHeight;
            float heightError = targetDistance - state.GroundDistance;
            float normalVelocity = Vector3.Dot(velocity, state.GroundNormal);
            float acceleration = heightError * _settings.SpringStrength - normalVelocity * _settings.SpringDamping;
            return velocity + state.GroundNormal * acceleration * fixedDeltaTime;
        }
    }
}
