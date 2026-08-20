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
        /// <param name="isJumping">是否处于主动跳跃且尚未重新落地的阶段；该阶段不施加支撑回正。</param>
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

            // 有符号高度差直接决定修正方向和幅度：脚底过低时上托，脚底过高时沿地面法线向下回拉。
            Vector3 groundNormal = contact.Hit.normal;
            float springAcceleration = heightError * _settings.SpringStrength;

            // 阻尼始终抵消沿地面法线的既有速度，避免弹簧在目标距离两侧来回过冲。
            float normalVelocity = Vector3.Dot(velocity, groundNormal);
            float dampingAcceleration = -normalVelocity * _settings.SpringDamping;
            float correctionAcceleration = springAcceleration + dampingAcceleration;
            return velocity + groundNormal * correctionAcceleration * fixedDeltaTime;
        }
    }
}
