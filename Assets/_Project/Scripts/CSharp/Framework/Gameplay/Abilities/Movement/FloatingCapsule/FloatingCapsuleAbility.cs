using Framework.Gameplay.Abilities.Configuration;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Movement
{
    /// <summary>提供浮动胶囊形状和悬浮修正能力运行时。</summary>
    public sealed class FloatingCapsuleAbility : AbilityRuntime
    {
        // 浮动胶囊静态配置表。
        private readonly FloatingCapsuleAbilitySO _configuration;
        // 单位独占的浮动胶囊运行时配置。
        private FloatingCapsuleModule _floatingCapsule;
        // 单位独占的接地和悬浮运行时配置。
        private GroundSettings _groundSettings;
        // 当前单位刚体。
        private Rigidbody _rigidbody;
        // 当前单位主胶囊。
        private CapsuleCollider _capsule;
        // 当前浮动形状模块。
        private ColliderShapeModule _shape;
        // 当前浮动接地探测模块。
        private GroundProbeModule _groundProbe;
        // 当前悬浮修正模块。
        private HoverModule _hover;
        // 当前单位共享的跳跃状态数据。
        private JumpContextData _jumpData;

        /// <summary>创建浮动胶囊运行时并保存配置表引用。</summary>
        /// <param name="configuration">浮动胶囊配置表；允许为 null 并使用默认配置。</param>
        public FloatingCapsuleAbility(FloatingCapsuleAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>获取浮动能力所需组件并创建悬浮链。</summary>
        /// <param name="context">当前单位能力上下文。</param>
        public override void Initialize(AbilityContext context)
        {
            base.Initialize(context);
            if (context == null || context.Owner == null) return;

            _rigidbody = context.Owner.GetComponent<Rigidbody>();
            if (_rigidbody == null && Application.isPlaying) _rigidbody = context.Owner.AddComponent<Rigidbody>();
            _capsule = context.Owner.GetComponent<CapsuleCollider>();
            if (_capsule == null && Application.isPlaying) _capsule = context.Owner.AddComponent<CapsuleCollider>();
            if (_rigidbody == null || _capsule == null) return;
            // 从配置表创建单位独占运行时配置，浮动形状快照不会写回共享资产。
            if (_configuration != null)
                _configuration.CreateRuntimeCopies(out _floatingCapsule, out _groundSettings);
            else
            {
                _floatingCapsule = new FloatingCapsuleModule();
                _groundSettings = new GroundSettings();
            }

            // 创建浮动专属形状和接地链，基础移动不接管浮动参数。
            _shape = new ColliderShapeModule(_capsule, context.Owner, _floatingCapsule);
            GroundSettings runtimeGroundSettings = _groundSettings;
            _groundProbe = new GroundProbeModule(
                _shape,
                context.Transform,
                new UnityPhysicsQuery(),
                runtimeGroundSettings);
            _hover = new HoverModule(runtimeGroundSettings, _groundProbe);
            Context.Register(AbilityContextDataType.FloatingCapsule,
                new FloatingCapsuleContextData(_shape, _groundProbe));
        }

        /// <summary>同步浮动胶囊形状。</summary>
        public override void OnAbilityEnable()
        {
            _shape?.Synchronize();
        }

        /// <summary>执行浮动胶囊接地探测和高度修正。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public override void FixedUpdateAbility(float fixedDeltaTime)
        {
            if (_rigidbody == null || _groundProbe == null || _hover == null) return;
            _shape?.Synchronize();
            GroundContact contact = _groundProbe.ProbeGround();
            bool jumping = Context != null
                           && Context.TryGet(AbilityContextDataType.Jump, out _jumpData)
                           && _jumpData.IsJumping;
            _rigidbody.velocity = _hover.Apply(_rigidbody.velocity, contact, jumping, fixedDeltaTime);
        }

        /// <summary>禁用时恢复基础胶囊并删除脚底辅助碰撞体。</summary>
        public override void OnAbilityDisable()
        {
            _shape?.RestoreAuthoringShape();
        }

        /// <summary>释放浮动胶囊运行时引用。</summary>
        public override void DisposeAbility()
        {
            OnAbilityDisable();
            _groundProbe = null;
            _hover = null;
            _shape = null;
            _capsule = null;
            _rigidbody = null;
            _jumpData = null;
            Context?.Unregister(AbilityContextDataType.FloatingCapsule);
            base.DisposeAbility();
        }

    }
}
