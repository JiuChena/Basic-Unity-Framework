using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 处理浮动胶囊在有效地面接触上的沿法线支撑和回拉。
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
        /// 在任何有效地面命中上沿法线施加浮动胶囊的弹簧和阻尼修正。
        /// </summary>
        /// <param name="velocity">尚未施加悬浮修正的速度。</param>
        /// <param name="contact">当前物理步的真实地面接触结果；陡坡同样参与悬浮支撑。</param>
        /// <param name="isJumping">本物理步是否刚触发起跳；起跳时不施加支撑回正。</param>
        /// <param name="fixedDeltaTime">本物理步时长，单位：秒。</param>
        /// <returns>加入浮动胶囊支撑修正后的速度；不满足支撑条件时返回原速度。</returns>
        public Vector3 Apply(
            Vector3 velocity,
            in GroundContact contact,
            bool isJumping,
            float fixedDeltaTime)
        {
            // 接触、起跳和物理步有效性均属于浮动胶囊自身的支撑规则。
            if (_settings == null || !contact.HasContact || isJumping || fixedDeltaTime <= 0f) return velocity;

            // 浮动胶囊的支撑留空属于物理支撑目标，不能仅作为外形修改后被弹簧重新压回地面。
            float targetDistance = _groundProbe != null
                ? _groundProbe.DesiredGroundDistance
                : _settings.HoverHeight;
            float heightError = targetDistance - contact.Distance;
            if (Mathf.Abs(heightError) <= 0.0001f) return velocity;

            // 脚底低于目标距离时向上托起；高于目标距离时沿相反方向拉回。
            Vector3 correctionDirection = heightError > 0f
                ? contact.Hit.normal
                : -contact.Hit.normal;
            float springAcceleration = Mathf.Abs(heightError) * _settings.SpringStrength;

            // 阻尼始终在本次校正方向上计算，抵消沿该方向的已有速度以避免越过目标高度。
            float velocityAlongCorrection = Vector3.Dot(velocity, correctionDirection);
            float dampingAcceleration = -velocityAlongCorrection * _settings.SpringDamping;
            float correctionAcceleration = springAcceleration + dampingAcceleration;
            return velocity + correctionDirection * correctionAcceleration * fixedDeltaTime;
        }
    }
}
