using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 保存能力静态配置并创建单位独占运行时实例。
    /// </summary>
    public abstract class AbilityDefinitionSO : ScriptableObject
    {
        // 能力在 Inspector 和诊断面板中的显示名称。
        [Tooltip("能力在运行时诊断和 Inspector 中显示的名称")]
        [SerializeField] private string _displayName;

        /// <summary>获取能力显示名称。</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;

        /// <summary>根据静态配置创建不共享状态的能力运行时。</summary>
        /// <param name="context">能力所属单位上下文。</param>
        /// <returns>新创建的单位独占能力运行时。</returns>
        public abstract AbilityRuntime CreateRuntime(AbilityContext context);
    }
}
