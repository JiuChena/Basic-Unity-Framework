using Framework.ExpandComponent.UnitMover;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 配置并执行预测支撑式边缘保护能力。
    /// </summary>
    [CreateAssetMenu(fileName = "EdgeProtectionAbility", menuName = "Framework/Gameplay/Abilities/Movement/Edge Protection")]
    public sealed class EdgeProtectionAbilitySO : AbilityDefinitionSO
    {
        // 边缘保护静态参数；运行时会复制为单位独占实例。
        [Header("边缘保护")]
        [Tooltip("边缘支撑预测、短缝确认和地面层过滤参数")]
        [SerializeField] private EdgeProtectionModule _configuration = new EdgeProtectionModule();
        // 边缘保护依赖的地面检测配置。
        [Tooltip("边缘保护使用的坡度、地面层和探测距离配置")]
        [SerializeField] private GroundSettings _groundSettings = new GroundSettings();

        /// <summary>根据配置创建边缘保护能力运行时。</summary>
        /// <param name="context">能力所属单位上下文。</param>
        /// <returns>单位独占边缘保护运行时。</returns>
        public override AbilityRuntime CreateRuntime(AbilityContext context)
        {
            return new EdgeProtectionAbilityRuntime(
                _configuration != null ? _configuration.CreateRuntimeCopy() : new EdgeProtectionModule(),
                _groundSettings != null ? _groundSettings.CreateRuntimeCopy() : new GroundSettings());
        }
    }

    /// <summary>
    /// 在固定帧中移除指向无支撑区域的速度分量。
    /// </summary>
    public sealed class EdgeProtectionAbilityRuntime : AbilityRuntime
    {
        // 当前单位独占的边缘保护模块。
        private readonly EdgeProtectionModule _edgeProtection;
        // 当前单位独占的接地配置。
        private readonly GroundSettings _groundSettings;
        // 由浮动胶囊能力注册的共享形状模块。
        private ColliderShapeModule _shapeModule;
        // 当前单位共享的接地查询模块。
        private GroundProbeModule _groundProbe;

        /// <summary>创建边缘保护能力运行时。</summary>
        /// <param name="edgeProtection">单位独占边缘保护模块。</param>
        /// <param name="groundSettings">单位独占地面配置。</param>
        public EdgeProtectionAbilityRuntime(EdgeProtectionModule edgeProtection, GroundSettings groundSettings)
        {
            _edgeProtection = edgeProtection;
            _groundSettings = groundSettings;
        }

        /// <summary>读取浮动胶囊提供的形状服务并创建接地查询服务。</summary>
        public override void Start()
        {
            if (Context == null || Context.MovementCollider == null) return;
            _shapeModule = Context.GetService<ColliderShapeModule>();
            if (_shapeModule == null)
            {
                FloatingCapsuleModule disabledFloating = new FloatingCapsuleModule();
                _shapeModule = new ColliderShapeModule(Context.MovementCollider, Context.Owner, disabledFloating);
                _shapeModule.Synchronize();
                Context.RegisterService(_shapeModule);
            }

            _groundProbe = new GroundProbeModule(
                _shapeModule,
                Context.Transform,
                Context.PhysicsQuery,
                _groundSettings);
            _edgeProtection?.Initialize(_shapeModule, _groundProbe);
            Context.RegisterService(_groundProbe);
        }

        /// <summary>执行边缘支撑预测并约束当前固定帧速度。</summary>
        /// <param name="fixedDeltaTime">当前固定帧时长，单位：秒。</param>
        public override void FixedUpdate(float fixedDeltaTime)
        {
            if (_edgeProtection == null || Context == null || _groundProbe == null) return;

            Vector3 candidate = Vector3.ProjectOnPlane(Context.Velocity, Vector3.up);
            _edgeProtection.ConstrainVelocity(
                Context.MovementState,
                candidate,
                candidate,
                fixedDeltaTime,
                out Vector3 constrainedCandidate,
                out _);
            Context.Velocity = new Vector3(constrainedCandidate.x, Context.Velocity.y, constrainedCandidate.z);
        }

        /// <summary>清理边缘保护服务和运行时检查点。</summary>
        public override void Dispose()
        {
            _edgeProtection?.ResetRuntimeState();
            if (Context != null)
            {
                if (ReferenceEquals(Context.GetService<GroundProbeModule>(), _groundProbe))
                    Context.RegisterService<GroundProbeModule>(null);
            }
            _groundProbe = null;
            _shapeModule = null;
            base.Dispose();
        }
    }
}
