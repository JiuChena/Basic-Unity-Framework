using Framework.ExpandComponent.UnitMover;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 配置并执行带土狼时间、输入缓冲和跳跃截断的跳跃能力。
    /// </summary>
    [CreateAssetMenu(fileName = "JumpAbility", menuName = "Framework/Gameplay/Abilities/Movement/Jump")]
    public sealed class JumpAbilitySO : AbilityDefinitionSO
    {
        // 跳跃静态参数；运行时会复制为单位独占实例。
        [Header("跳跃")]
        [Tooltip("跳跃速度、土狼时间、输入缓冲和提前松开跳跃键的参数")]
        [SerializeField] private JumpModule _configuration = new JumpModule();

        /// <summary>根据配置创建跳跃能力运行时。</summary>
        /// <param name="context">能力所属单位上下文。</param>
        /// <returns>单位独占跳跃运行时。</returns>
        public override AbilityRuntime CreateRuntime(AbilityContext context)
        {
            return new JumpAbilityRuntime(_configuration != null
                ? _configuration.CreateRuntimeCopy()
                : new JumpModule());
        }
    }

    /// <summary>
    /// 在固定帧中消费移动命令并叠加跳跃速度变化。
    /// </summary>
    public sealed class JumpAbilityRuntime : AbilityRuntime
    {
        // 当前单位独占的跳跃模块。
        private readonly JumpModule _jump;

        /// <summary>创建跳跃能力运行时。</summary>
        /// <param name="jump">单位独占跳跃配置和状态模块。</param>
        public JumpAbilityRuntime(JumpModule jump)
        {
            _jump = jump;
        }

        /// <summary>清空跳跃瞬态状态，准备新的启用周期。</summary>
        public override void OnEnable()
        {
            _jump?.ResetRuntimeState();
        }

        /// <summary>执行跳跃边界、起跳和跳跃截断逻辑。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public override void FixedUpdate(float fixedDeltaTime)
        {
            if (_jump == null || Context == null) return;

            _jump.Update(
                Context.MovementState,
                Context.MovementCommand,
                fixedDeltaTime,
                out bool startJump,
                out bool cutJump);

            if (startJump)
                Context.Velocity = new Vector3(Context.Velocity.x, _jump.InitialSpeed, Context.Velocity.z);
            else if (cutJump && Context.Velocity.y > 0f)
                Context.Velocity = new Vector3(Context.Velocity.x, Context.Velocity.y * _jump.CutMultiplier, Context.Velocity.z);
        }

        /// <summary>释放跳跃瞬态状态。</summary>
        public override void Dispose()
        {
            _jump?.ResetRuntimeState();
            base.Dispose();
        }
    }
}
