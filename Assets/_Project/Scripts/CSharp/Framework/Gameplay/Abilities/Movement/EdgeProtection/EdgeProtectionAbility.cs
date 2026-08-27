using Framework.Gameplay.Abilities.Configuration;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Movement
{
    /// <summary>提供预测支撑式边缘保护能力运行时。</summary>
    public sealed class EdgeProtectionAbility : AbilityRuntime
    {
        // 边缘保护静态配置表。
        private readonly EdgeProtectionAbilitySO _configuration;
        // 无浮动胶囊时使用的接地运行时配置。
        private GroundSettings _groundSettings;
        // 当前单位共享的移动状态数据。
        private MovementContextData _movementData;
        // 当前单位刚体。
        private Rigidbody _rigidbody;
        // 当前边缘保护模块。
        private EdgeProtectionModule _edgeProtection;
        // 无浮动能力时创建的独立形状模块。
        private ColliderShapeModule _fallbackShape;
        // 当前边缘保护接地探测模块。
        private GroundProbeModule _groundProbe;

        /// <summary>创建边缘保护运行时并保存配置表引用。</summary>
        /// <param name="configuration">边缘保护配置表；允许为 null 并使用默认配置。</param>
        public EdgeProtectionAbility(EdgeProtectionAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>获取移动、刚体和接地模块依赖。</summary>
        /// <param name="context">当前单位能力上下文。</param>
        public override void Initialize(AbilityContext context)
        {
            base.Initialize(context);
            if (context == null || context.Owner == null) return;

            Context.TryGet(AbilityContextDataType.Movement, out _movementData);
            _rigidbody = context.Owner.GetComponent<Rigidbody>();
            if (Context.TryGet(AbilityContextDataType.FloatingCapsule, out FloatingCapsuleContextData floatingData)
                && floatingData.ShapeModule != null
                && floatingData.GroundProbe != null)
            {
                _groundProbe = floatingData.GroundProbe;
                EdgeProtectionModule configuredEdgeProtection;
                if (_configuration != null)
                    _configuration.CreateRuntimeCopies(out configuredEdgeProtection, out _groundSettings);
                else
                {
                    configuredEdgeProtection = new EdgeProtectionModule();
                    _groundSettings = new GroundSettings();
                }

                _edgeProtection = configuredEdgeProtection;
                _edgeProtection.Initialize(floatingData.ShapeModule, _groundProbe);
                return;
            }

            CapsuleCollider capsule = context.Owner.GetComponent<CapsuleCollider>();
            if (capsule == null && Application.isPlaying) capsule = context.Owner.AddComponent<CapsuleCollider>();
            if (capsule == null) return;
            EdgeProtectionModule fallbackEdgeProtection;
            if (_configuration != null)
                _configuration.CreateRuntimeCopies(out fallbackEdgeProtection, out _groundSettings);
            else
            {
                fallbackEdgeProtection = new EdgeProtectionModule();
                _groundSettings = new GroundSettings();
            }

            _fallbackShape = new ColliderShapeModule(capsule, context.Owner, new FloatingCapsuleModule());
            _groundProbe = new GroundProbeModule(
                _fallbackShape,
                context.Transform,
                new UnityPhysicsQuery(),
                _groundSettings);
            _edgeProtection = fallbackEdgeProtection;
            _edgeProtection.Initialize(_fallbackShape, _groundProbe);
        }

        /// <summary>根据基础移动状态约束当前水平速度。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public override void FixedUpdateAbility(float fixedDeltaTime)
        {
            if (_movementData == null || _rigidbody == null || _edgeProtection == null || _groundProbe == null) return;
            Vector3 candidate = Vector3.ProjectOnPlane(_rigidbody.velocity, Vector3.up);
            _edgeProtection.ConstrainVelocity(
                _movementData.CurrentState,
                candidate,
                candidate,
                fixedDeltaTime,
                out Vector3 constrained,
                out _);
            _rigidbody.velocity = new Vector3(constrained.x, _rigidbody.velocity.y, constrained.z);
        }

        /// <summary>清空边缘保护运行时检查点和诊断状态。</summary>
        public override void OnAbilityDisable()
        {
            _edgeProtection?.ResetRuntimeState();
        }

        /// <summary>释放边缘保护运行时引用。</summary>
        public override void DisposeAbility()
        {
            OnAbilityDisable();
            _movementData = null;
            _rigidbody = null;
            _edgeProtection = null;
            _fallbackShape = null;
            _groundProbe = null;
            base.DisposeAbility();
        }
    }
}
