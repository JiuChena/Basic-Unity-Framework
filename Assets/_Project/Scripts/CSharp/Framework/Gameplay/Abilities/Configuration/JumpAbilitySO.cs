using UnityEngine;
using Framework.Gameplay.Abilities.Movement;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存跳跃能力的可复用静态配置。</summary>
    [CreateAssetMenu(fileName = "JumpAbility", menuName = "Framework/Gameplay/Abilities/Jump")]
    public sealed class JumpAbilitySO : AbilityDefinitionSO
    {
        // 跳跃参数配置。
        [Header("跳跃")]
        [Tooltip("跳跃初速度、土狼时间、输入缓冲和提前松开参数")]
        [SerializeField] private JumpModule _jump = new JumpModule();

        /// <summary>创建跳跃配置的运行时副本。</summary>
        /// <returns>供单个单位使用的独立跳跃配置。</returns>
        public JumpModule CreateRuntimeCopy()
        {
            return _jump != null ? _jump.CreateRuntimeCopy() : new JumpModule();
        }

        /// <summary>创建跳跃能力运行时。</summary>
        /// <returns>使用当前跳跃配置的能力运行时。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new JumpAbility(this);
        }
    }
}
