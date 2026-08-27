using UnityEngine;
using Framework.Gameplay.Abilities.Movement;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存基础移动能力的可复用静态配置。</summary>
    [CreateAssetMenu(fileName = "MovementAbility", menuName = "Framework/Gameplay/Abilities/Movement")]
    public sealed class MovementAbilitySO : AbilityDefinitionSO
    {
        // 基础水平移动配置。
        [Header("移动")]
        [Tooltip("地面和空中的速度、加速度以及空中控制参数")]
        [SerializeField] private LocomotionSettings _locomotion = new LocomotionSettings();
        // 基础接地和坡面配置。
        [Tooltip("基础移动使用的地面层、坡度限制和接地探测参数")]
        [SerializeField] private GroundSettings _ground = new GroundSettings();
        // 基础重力配置。
        [Header("重力")]
        [Tooltip("项目重力倍率、下落倍率和最大下落速度")]
        [SerializeField] private GravityModule _gravity = new GravityModule();

        /// <summary>创建基础移动配置的运行时副本。</summary>
        /// <param name="locomotion">返回独立的水平移动配置。</param>
        /// <param name="ground">返回独立的接地配置。</param>
        /// <param name="gravity">返回独立的重力配置。</param>
        public void CreateRuntimeCopies(
            out LocomotionSettings locomotion,
            out GroundSettings ground,
            out GravityModule gravity)
        {
            locomotion = _locomotion != null ? _locomotion.CreateRuntimeCopy() : new LocomotionSettings();
            ground = _ground != null ? _ground.CreateRuntimeCopy() : new GroundSettings();
            gravity = _gravity != null ? _gravity.CreateRuntimeCopy() : new GravityModule();
        }

        /// <summary>创建基础移动能力运行时。</summary>
        /// <returns>使用当前移动配置的基础移动运行时。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new MovementAbility(this);
        }
    }
}
