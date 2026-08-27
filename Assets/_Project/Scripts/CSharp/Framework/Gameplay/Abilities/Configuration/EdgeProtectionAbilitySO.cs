using UnityEngine;
using Framework.Gameplay.Abilities.Movement;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存边缘保护能力的可复用静态配置。</summary>
    [CreateAssetMenu(fileName = "EdgeProtectionAbility", menuName = "Framework/Gameplay/Abilities/Edge Protection")]
    public sealed class EdgeProtectionAbilitySO : AbilityDefinitionSO
    {
        // 边缘预测和速度约束配置。
        [Header("边缘保护")]
        [Tooltip("预测脚底支撑、短缝确认和边缘速度约束参数")]
        [SerializeField] private EdgeProtectionModule _edgeProtection = new EdgeProtectionModule();
        // 无浮动胶囊时使用的接地配置。
        [Tooltip("未挂载浮动胶囊能力时使用的接地层和探测参数")]
        [SerializeField] private GroundSettings _ground = new GroundSettings();

        /// <summary>创建边缘保护配置和回退接地配置的运行时副本。</summary>
        /// <param name="edgeProtection">返回独立的边缘保护配置。</param>
        /// <param name="ground">返回独立的回退接地配置。</param>
        public void CreateRuntimeCopies(
            out EdgeProtectionModule edgeProtection,
            out GroundSettings ground)
        {
            edgeProtection = _edgeProtection != null
                ? _edgeProtection.CreateRuntimeCopy()
                : new EdgeProtectionModule();
            ground = _ground != null ? _ground.CreateRuntimeCopy() : new GroundSettings();
        }

        /// <summary>创建边缘保护能力运行时。</summary>
        /// <returns>使用当前边缘保护配置的能力运行时。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new EdgeProtectionAbility(this);
        }
    }
}
