using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CoreFramework
{
    /// <summary>
    /// 标记 MonoBehaviour/Object 字段必须在 Inspector 中实现指定接口。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class RequireInterfaceAttribute : PropertyAttribute
    {
        public readonly Type InterfaceType;
        public RequireInterfaceAttribute(Type interfaceType) => InterfaceType = interfaceType;
    }


    /// <summary>
    /// 使用 Rigidbody 执行单位地面、跳跃、台阶和空中移动的通用组件。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Rigidbody))]
    public class UnitMover : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("参与移动物理检测的 CapsuleCollider 或 BoxCollider 组件")]
        [FormerlySerializedAs("capsuleCollider")]
        public Collider movementCollider;

        [Tooltip("用于相机相对移动的参考 Transform，留空时在 Awake 缓存主相机")]
        public Transform cameraTransform;

        [Tooltip("提供 Blackboard 的输入组件，必须实现 IInputProvider；留空时自动查找同物体组件")]
        [RequireInterface(typeof(IInputProvider))]
        public MonoBehaviour inputProviderSource;

        [Header("地面移动")]
        [Tooltip("基础移动速度，单位：米/秒")]
        [Min(0f)]
        public float moveSpeed = 5f;

        [Tooltip("冲刺时应用到基础速度的倍率")]
        [Min(0f)]
        public float sprintMultiplier = 1.6f;

        [Tooltip("地面移动时达到目标速度的最大加速度，单位：米/秒²")]
        [Min(0f)]
        public float groundAcceleration = 45f;

        [Tooltip("地面无输入时降低水平速度的最大减速度，单位：米/秒²")]
        [Min(0f)]
        public float groundDeceleration = 55f;

        [Tooltip("碰撞体底部与可站立地面的目标悬浮距离，单位：米")]
        [Min(0f)]
        public float hoverHeight = 0.05f;

        [Tooltip("在目标悬浮距离之外额外向下探测的距离，单位：米")]
        [Min(0f)]
        public float groundProbeDistance = 0.3f;

        [Tooltip("可判定为地面的最大坡度，单位：度")]
        [Range(0f, 89f)]
        public float slopeLimit = 45f;

        [Tooltip("浮动弹簧的高度误差加速度系数")]
        [Min(0f)]
        public float springStrength = 90f;

        [Tooltip("浮动弹簧沿地面法线速度的阻尼系数")]
        [Min(0f)]
        public float springDamping = 14f;

        [Tooltip("允许自动跨越的最大台阶高度，0 禁用台阶辅助，单位：米")]
        [Min(0f)]
        public float stepHeight = 0.3f;

        [Tooltip("参与地面、台阶和悬崖检测的物理层")]
        public LayerMask groundLayer = ~0;

        [Header("浮动胶囊体")]
        [Tooltip("启用后缩短实际 CapsuleCollider 的底部，顶部始终与基础胶囊体对齐")]
        public bool enableFloatingCapsule;

        [Tooltip("从基础胶囊体底部移除的碰撞高度，单位：米")]
        [Min(0f)]
        public float floatingBottomClearance = 0.4f;

        [Header("空中行为")]
        [Tooltip("跳跃时沿当前地面法线施加的初始速度，单位：米/秒")]
        [Min(0f)]
        public float jumpSpeed = 8f;

        [Tooltip("应用到 Physics.gravity 的重力倍率")]
        [Min(0f)]
        public float gravityMultiplier = 1f;

        [Tooltip("空中向目标速度逼近的最大加速度，单位：米/秒²")]
        [Min(0f)]
        public float airAcceleration = 15f;

        [Tooltip("空中控制强度，0 表示无控制，1 表示完整控制")]
        [Range(0f, 1f)]
        public float airControl = 0.45f;

        [Tooltip("空中水平速度上限，单位：米/秒")]
        [Min(0f)]
        public float airSpeedLimit = 6f;

        [Tooltip("是否阻止单位主动走向超过最大落差的悬崖")]
        public bool ledgeCheckEnabled = true;

        [Tooltip("前方地面缺失达到此落差时视为悬崖，单位：米")]
        [Min(0f)]
        public float maxFallHeight = 2f;

        [Header("编辑器预览")]
        [Tooltip("在编辑模式和运行模式的 Scene 视图中绘制浮动胶囊体尺寸预览")]
        public bool showHoverPreview = true;

        [Serializable]
        private struct CapsuleShapeSnapshot
        {
            public bool isInitialized;
            public Vector3 center;
            public float radius;
            public float height;
            public int direction;
        }

        // ── 内部常量（从 public 精简为私有）──
        private const float StepProbePadding = 0.08f;
        private const float MaxStepUpSpeed = 4f;
        private const float JumpGroundIgnoreDuration = 0.1f;

        // Rigidbody 运动执行器，仅在初始化阶段缓存。
        [SerializeField] private Rigidbody _rigidbody;
        // 运行时解析出的输入提供者接口。
        private IInputProvider _inputProvider;
        // 当前可执行的纯 C# 移动策略。
        private MovementStrategy _strategy;
        // 策略类的程序集限定名，由编辑器保存。
        [SerializeField] private string _strategyTypeName;
        // 编辑器保存的策略公开字段参数。
        [SerializeField] private List<StrategyParam> _strategyParams = new List<StrategyParam>();
        // Collider.Cast 使用的预分配命中缓冲区。
        private readonly RaycastHit[] _castHits = new RaycastHit[8];
        // 射线检测使用的预分配命中缓冲区。
        private readonly RaycastHit[] _rayHits = new RaycastHit[8];
        // 当前物理步由策略或外部提交的移动方向。
        private Vector3 _desiredDirection;
        // 当前物理步由策略或外部提交的速度倍率。
        private float _speedMultiplier = 1f;
        // 当前物理步是否有跳跃请求。
        private bool _jumpRequested;
        // 当前是否站在可行走地面上。
        private bool _isGrounded;
        // 当前可站立地面的法线。
        private Vector3 _groundNormal = Vector3.up;
        // 当前可站立地面的命中点。
        private Vector3 _groundPoint;
        // 当前可站立地面的碰撞距离。
        private float _groundDistance;
        // 跳跃后重新允许地面吸附的时间点。
        private float _groundIgnoreUntil;
        // 是否已经报告输入 Provider 缺失问题。
        private bool _reportedMissingProvider;
        // 是否已经报告碰撞体配置错误。
        private bool _reportedColliderError;
        // 首次同步时记录的原始胶囊体形状，关闭浮动时据此恢复。
        [SerializeField, HideInInspector] private CapsuleShapeSnapshot _baseCapsuleShape;
        // UnitMover 最近一次写入 Collider 的形状，用于识别用户随后在 Inspector 中的修改。
        [SerializeField, HideInInspector] private CapsuleShapeSnapshot _lastAppliedCapsuleShape;
        // 快照对应的 Collider，切换组件时不能复用旧形状。
        [SerializeField, HideInInspector] private CapsuleCollider _snapshotCapsuleCollider;

        /// <summary>
        /// 当前是否站在可行走地面上。
        /// </summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>
        /// 当前可站立地面的法线。
        /// </summary>
        public Vector3 GroundNormal => _groundNormal;

        /// <summary>
        /// 当前 Rigidbody 的真实速度。
        /// </summary>
        public Vector3 CurrentVelocity => _rigidbody != null ? _rigidbody.velocity : Vector3.zero;

        /// <summary>
        /// 供策略获取的已缓存相机参考。
        /// </summary>
        public Transform CameraTransform => cameraTransform;

        /// <summary>
        /// 编辑器访问的策略类名。
        /// </summary>
        public string StrategyTypeName
        {
            get => _strategyTypeName;
            set
            {
                if (_strategyTypeName == value) return;

                _strategyTypeName = value;
                if (Application.isPlaying) CreateStrategy();
            }
        }

        /// <summary>
        /// 缓存组件、配置刚体并创建当前策略。
        /// </summary>
        private void Awake()
        {
            ResolveComponents(Application.isPlaying);
            SynchronizeFloatingCapsule();
            if (!Application.isPlaying) return;

            ConfigureRigidbody();
            ResolveInputProvider();
            CreateStrategy();
        }

        /// <summary>
        /// 为新添加组件填充合理的默认引用和策略。
        /// </summary>
        private void Reset()
        {
            ResolveComponents(false);
            SynchronizeFloatingCapsule();
            EnsureDefaultStrategyType();
        }

        /// <summary>
        /// 在编辑器中校验碰撞体和策略配置。
        /// </summary>
        private void OnValidate()
        {
            ResolveComponents(false);
            SynchronizeFloatingCapsule();
            EnsureDefaultStrategyType();
        }

        /// <summary>
        /// 在固定物理步中执行策略、地面探测和刚体施力。
        /// </summary>
        private void FixedUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            if (!HasSupportedCollider()) return;

            SynchronizeFloatingCapsule();

            UpdateGroundState();
            ExecuteStrategy();
            ApplyJumpRequest();
            ApplyStepAssist();
            ApplyVerticalForces();
            ApplyHorizontalForces();
            ClearStepCommands();
        }

        /// <summary>
        /// 提交本物理步期望的世界空间移动方向与速度倍率。
        /// </summary>
        /// <param name="worldDirection">世界空间的移动方向。</param>
        /// <param name="speedMultiplier">应用到基础速度的非负倍率。</param>
        public virtual void Move(Vector3 worldDirection, float speedMultiplier = 1f)
        {
            _desiredDirection = worldDirection.sqrMagnitude > 0.0001f ? worldDirection.normalized : Vector3.zero;
            _speedMultiplier = Mathf.Max(0f, speedMultiplier);
        }

        /// <summary>
        /// 请求下一物理步执行跳跃；仅在可站立地面上生效。
        /// </summary>
        public virtual void Jump()
        {
            _jumpRequested = true;
        }

        /// <summary>
        /// 在固定物理步中朝指定世界方向旋转刚体。
        /// </summary>
        /// <param name="worldDirection">期望朝向的世界空间方向。</param>
        /// <param name="degreesPerSecond">旋转速度，单位：度/秒。</param>
        public virtual void RotateTowards(Vector3 worldDirection, float degreesPerSecond)
        {
            if (_rigidbody == null || worldDirection.sqrMagnitude <= 0.0001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            Quaternion nextRotation = Quaternion.RotateTowards(
                _rigidbody.rotation,
                targetRotation,
                Mathf.Max(0f, degreesPerSecond) * Time.fixedDeltaTime);
            _rigidbody.MoveRotation(nextRotation);
        }

        /// <summary>
        /// 运行时切换为指定策略，并可迁移同名同类型公开字段。
        /// </summary>
        /// <typeparam name="T">具有无参构造的移动策略类型。</typeparam>
        /// <param name="keepState">是否迁移旧策略的兼容公开字段。</param>
        public virtual void SwitchStrategy<T>(bool keepState = true) where T : MovementStrategy, new()
        {
            MovementStrategy nextStrategy = new T();
            if (keepState && _strategy != null) MigrateStrategyFields(_strategy, nextStrategy);

            _strategy = nextStrategy;
            _strategyTypeName = typeof(T).AssemblyQualifiedName;
            ApplyStrategyParams(_strategy);
        }

        /// <summary>
        /// 编辑器读取指定策略参数。
        /// </summary>
        /// <param name="fieldName">策略公开字段名称。</param>
        /// <param name="targetType">目标字段类型。</param>
        /// <returns>已保存参数值，不存在时为 null。</returns>
        public object GetStrategyParam(string fieldName, Type targetType)
        {
            if (_strategyParams == null) return null;

            StrategyParam param = _strategyParams.Find(item => item.name == fieldName);
            return param?.GetValue(targetType);
        }

        /// <summary>
        /// 编辑器读取指定策略参数。
        /// </summary>
        /// <param name="fieldName">策略公开字段名称。</param>
        /// <returns>已保存参数值，不存在时为 null。</returns>
        public object GetStrategyParam(string fieldName)
        {
            return GetStrategyParam(fieldName, null);
        }

        /// <summary>
        /// 编辑器保存指定策略公开字段的值。
        /// </summary>
        /// <param name="fieldName">策略公开字段名称。</param>
        /// <param name="value">要保存的基础类型或枚举值。</param>
        public void SetStrategyParam(string fieldName, object value)
        {
            if (string.IsNullOrEmpty(fieldName) || value == null) return;
            if (_strategyParams == null) _strategyParams = new List<StrategyParam>();

            StrategyParam param = _strategyParams.Find(item => item.name == fieldName);
            if (param == null)
            {
                param = new StrategyParam { name = fieldName };
                _strategyParams.Add(param);
            }

            param.SetValue(value);
        }

        /// <summary>
        /// 缓存 Rigidbody、Collider 与可选的相机引用。
        /// </summary>
        /// <param name="resolveCamera">是否在运行时缓存主相机。</param>
        private void ResolveComponents(bool resolveCamera)
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (movementCollider == null)
            {
                movementCollider = GetComponent<CapsuleCollider>();
                if (movementCollider == null) movementCollider = GetComponent<BoxCollider>();
            }
            if (resolveCamera && cameraTransform == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null) cameraTransform = mainCamera.transform;
            }
        }

        /// <summary>
        /// 同步基础胶囊体与实际参与物理的胶囊体。浮动时仅从底部移除碰撞高度，顶部保持不动。
        /// </summary>
        private void SynchronizeFloatingCapsule()
        {
            if (!(movementCollider is CapsuleCollider capsule))
            {
                _baseCapsuleShape = default;
                _lastAppliedCapsuleShape = default;
                _snapshotCapsuleCollider = null;
                return;
            }

            if (_snapshotCapsuleCollider != capsule)
            {
                _baseCapsuleShape = default;
                _lastAppliedCapsuleShape = default;
                _snapshotCapsuleCollider = capsule;
            }

            CapsuleShapeSnapshot currentShape = CaptureCapsuleShape(capsule);
            if (!_baseCapsuleShape.isInitialized)
            {
                _baseCapsuleShape = currentShape;
            }
            else if (!_lastAppliedCapsuleShape.isInitialized
                || !CapsuleShapesMatch(currentShape, _lastAppliedCapsuleShape))
            {
                // Inspector 对 Collider 的直接修改视为新的设计尺寸，而非再次累减。
                _baseCapsuleShape = enableFloatingCapsule
                    ? ConvertEffectiveShapeToBase(currentShape)
                    : currentShape;
            }

            CapsuleShapeSnapshot desiredShape = enableFloatingCapsule
                ? CreateEffectiveCapsuleShape(_baseCapsuleShape)
                : _baseCapsuleShape;

            if (!CapsuleShapesMatch(currentShape, desiredShape))
                ApplyCapsuleShape(capsule, desiredShape);

            _lastAppliedCapsuleShape = desiredShape;
        }

        /// <summary>
        /// 从基础形状生成顶部锚定的有效碰撞胶囊体。
        /// </summary>
        private CapsuleShapeSnapshot CreateEffectiveCapsuleShape(CapsuleShapeSnapshot baseShape)
        {
            float clearance = GetClampedFloatingClearance(baseShape);
            Vector3 localAxis = GetCapsuleLocalAxis(baseShape.direction);
            CapsuleShapeSnapshot effectiveShape = baseShape;
            effectiveShape.isInitialized = true;
            effectiveShape.height = baseShape.height - clearance;
            effectiveShape.center = baseShape.center + localAxis * (clearance * 0.5f);
            return effectiveShape;
        }

        /// <summary>
        /// 将用户在浮动状态下直接编辑的有效形状还原成其对应的基础形状。
        /// </summary>
        private CapsuleShapeSnapshot ConvertEffectiveShapeToBase(CapsuleShapeSnapshot effectiveShape)
        {
            float requestedClearance = Mathf.Max(0f, floatingBottomClearance);
            float maximumClearance = Mathf.Max(
                0f,
                effectiveShape.height + requestedClearance - effectiveShape.radius * 2f);
            float clearance = Mathf.Min(requestedClearance, maximumClearance);
            Vector3 localAxis = GetCapsuleLocalAxis(effectiveShape.direction);
            effectiveShape.isInitialized = true;
            effectiveShape.height += clearance;
            effectiveShape.center -= localAxis * (clearance * 0.5f);
            return effectiveShape;
        }

        /// <summary>
        /// 返回不使胶囊体高度小于直径的底部空腔高度。
        /// </summary>
        private float GetClampedFloatingClearance(CapsuleShapeSnapshot baseShape)
        {
            float maximumClearance = Mathf.Max(0f, baseShape.height - baseShape.radius * 2f);
            return Mathf.Clamp(floatingBottomClearance, 0f, maximumClearance);
        }

        /// <summary>
        /// 读取 CapsuleCollider 的可序列化局部形状。
        /// </summary>
        private static CapsuleShapeSnapshot CaptureCapsuleShape(CapsuleCollider capsule)
        {
            return new CapsuleShapeSnapshot
            {
                isInitialized = true,
                center = capsule.center,
                radius = capsule.radius,
                height = capsule.height,
                direction = capsule.direction
            };
        }

        /// <summary>
        /// 将局部形状写回实际 CapsuleCollider。
        /// </summary>
        private static void ApplyCapsuleShape(CapsuleCollider capsule, CapsuleShapeSnapshot shape)
        {
            capsule.center = shape.center;
            capsule.radius = shape.radius;
            capsule.height = shape.height;
            capsule.direction = shape.direction;
        }

        /// <summary>
        /// 比较两份局部形状，避免浮动模式因重复同步而累计缩短高度。
        /// </summary>
        private static bool CapsuleShapesMatch(CapsuleShapeSnapshot left, CapsuleShapeSnapshot right)
        {
            const float tolerance = 0.0001f;
            return left.isInitialized == right.isInitialized
                && left.direction == right.direction
                && (left.center - right.center).sqrMagnitude <= tolerance * tolerance
                && Mathf.Abs(left.radius - right.radius) <= tolerance
                && Mathf.Abs(left.height - right.height) <= tolerance;
        }

        /// <summary>
        /// 获取 CapsuleCollider.direction 对应的局部正轴。
        /// </summary>
        private static Vector3 GetCapsuleLocalAxis(int direction)
        {
            return direction switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                _ => Vector3.forward
            };
        }

        /// <summary>
        /// 配置由 UnitMover 管理的 Rigidbody 运动模式。
        /// </summary>
        private void ConfigureRigidbody()
        {
            if (_rigidbody == null) return;

            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = false;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        /// <summary>
        /// 从显式引用或同物体组件解析输入提供者接口。
        /// </summary>
        private void ResolveInputProvider()
        {
            _inputProvider = inputProviderSource as IInputProvider;
            if (_inputProvider != null) return;

            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (component is IInputProvider provider)
                {
                    _inputProvider = provider;
                    inputProviderSource = component;
                    return;
                }
            }
        }

        /// <summary>
        /// 确保未配置策略时使用默认移动策略。
        /// </summary>
        private void EnsureDefaultStrategyType()
        {
            if (string.IsNullOrEmpty(_strategyTypeName))
                _strategyTypeName = typeof(DefaultMovementStrategy).AssemblyQualifiedName;
        }

        /// <summary>
        /// 使用保存的类型名创建策略实例并应用编辑器参数。
        /// </summary>
        private void CreateStrategy()
        {
            EnsureDefaultStrategyType();
            Type strategyType = Type.GetType(_strategyTypeName);
            if (!IsConcreteStrategy(strategyType))
            {
                strategyType = typeof(DefaultMovementStrategy);
                _strategyTypeName = strategyType.AssemblyQualifiedName;
            }

            _strategy = Activator.CreateInstance(strategyType) as MovementStrategy;
            ApplyStrategyParams(_strategy);
        }

        /// <summary>
        /// 在物理步执行当前策略；仅输入策略要求存在 Provider。
        /// </summary>
        private void ExecuteStrategy()
        {
            if (_strategy == null) return;

            Blackboard board = _inputProvider?.Board;
            if (board == null && _strategy.RequiresInputProvider && !_reportedMissingProvider)
            {
                Debug.LogError($"{name} 的 UnitMover 策略需要 IInputProvider，但未找到有效输入组件。", this);
                _reportedMissingProvider = true;
            }

            _strategy.Execute(board, this);
        }

        /// <summary>
        /// 使用 Collider.Cast 更新可站立地面状态。
        /// </summary>
        protected virtual void UpdateGroundState()
        {
            _isGrounded = false;
            _groundNormal = Vector3.up;
            _groundDistance = float.PositiveInfinity;

            if (Time.time < _groundIgnoreUntil) return;
            if (!TryGetGroundCast(hoverHeight + groundProbeDistance, out RaycastHit hit)) return;
            if (!IsWalkable(hit.normal)) return;

            _isGrounded = true;
            _groundNormal = hit.normal;
            _groundPoint = hit.point;
            _groundDistance = hit.distance;
        }

        /// <summary>
        /// 处理当前物理步的跳跃请求。
        /// </summary>
        protected virtual void ApplyJumpRequest()
        {
            if (!_jumpRequested || !_isGrounded) return;

            float normalVelocity = Vector3.Dot(_rigidbody.velocity, _groundNormal);
            float velocityChange = Mathf.Max(0f, jumpSpeed - normalVelocity);
            if (velocityChange > 0f)
                _rigidbody.AddForce(_groundNormal * velocityChange, ForceMode.VelocityChange);

            _isGrounded = false;
            _groundIgnoreUntil = Time.time + JumpGroundIgnoreDuration;
        }

        /// <summary>
        /// 沿地面法线施加浮动弹簧，离地时施加自定义重力。
        /// </summary>
        protected virtual void ApplyVerticalForces()
        {
            if (_isGrounded)
            {
                float heightError = hoverHeight - _groundDistance;
                float normalVelocity = Vector3.Dot(_rigidbody.velocity, _groundNormal);
                float acceleration = heightError * springStrength - normalVelocity * springDamping;
                _rigidbody.AddForce(_groundNormal * acceleration, ForceMode.Acceleration);
                return;
            }

            _rigidbody.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Acceleration);
        }

        /// <summary>
        /// 将期望方向转换为受地面、空中和悬崖限制的水平加速度。
        /// </summary>
        protected virtual void ApplyHorizontalForces()
        {
            Vector3 direction = ApplyLedgeCheck(_desiredDirection);
            if (_isGrounded)
                direction = Vector3.ProjectOnPlane(direction, _groundNormal).normalized;
            else
                direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;

            float targetSpeed = moveSpeed * _speedMultiplier;
            if (!_isGrounded) targetSpeed = Mathf.Min(targetSpeed, airSpeedLimit);

            Vector3 planeNormal = _isGrounded ? _groundNormal : Vector3.up;
            Vector3 currentVelocity = Vector3.ProjectOnPlane(_rigidbody.velocity, planeNormal);
            Vector3 targetVelocity = direction * targetSpeed;
            float acceleration = GetHorizontalAcceleration(direction);
            Vector3 velocityDelta = Vector3.MoveTowards(
                currentVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime);

            _rigidbody.AddForce(velocityDelta - currentVelocity, ForceMode.VelocityChange);
        }

        /// <summary>
        /// 对可跨越的小台阶施加有限上行速度辅助。
        /// </summary>
        protected virtual void ApplyStepAssist()
        {
            if (!_isGrounded || stepHeight <= 0f || _desiredDirection.sqrMagnitude <= 0.0001f) return;

            Vector3 moveDirection = Vector3.ProjectOnPlane(_desiredDirection, Vector3.up).normalized;
            if (moveDirection.sqrMagnitude <= 0.0001f) return;

            Bounds bounds = movementCollider.bounds;
            float forwardDistance = GetHorizontalExtent(moveDirection) + StepProbePadding;
            Vector3 lowerOrigin = new Vector3(bounds.center.x, bounds.min.y + 0.02f, bounds.center.z);
            if (!TryGetGroundRay(lowerOrigin, moveDirection, forwardDistance, out RaycastHit obstacle)) return;

            Vector3 upperOrigin = lowerOrigin + Vector3.up * stepHeight;
            if (TryGetGroundRay(upperOrigin, moveDirection, forwardDistance, out _)) return;

            Vector3 landingOrigin = upperOrigin + moveDirection * forwardDistance;
            float landingDistance = stepHeight + hoverHeight + groundProbeDistance;
            if (!TryGetGroundRay(landingOrigin, Vector3.down, landingDistance, out RaycastHit landing)) return;
            if (!IsWalkable(landing.normal)) return;

            float requiredHeight = landing.point.y + hoverHeight - bounds.min.y;
            if (requiredHeight <= 0f || requiredHeight > stepHeight + hoverHeight) return;

            float upwardSpeed = Vector3.Dot(_rigidbody.velocity, Vector3.up);
            float targetSpeed = Mathf.Min(MaxStepUpSpeed, requiredHeight / Time.fixedDeltaTime);
            if (targetSpeed > upwardSpeed)
                _rigidbody.AddForce(Vector3.up * (targetSpeed - upwardSpeed), ForceMode.VelocityChange);
        }

        /// <summary>
        /// 在启用边缘保护时移除通往悬崖的移动指令。
        /// </summary>
        /// <param name="desiredDirection">当前物理步的原始移动方向。</param>
        /// <returns>允许继续执行的移动方向。</returns>
        protected virtual Vector3 ApplyLedgeCheck(Vector3 desiredDirection)
        {
            if (!ledgeCheckEnabled || !_isGrounded || desiredDirection.sqrMagnitude <= 0.0001f)
                return desiredDirection;

            Vector3 horizontalDirection = Vector3.ProjectOnPlane(desiredDirection, Vector3.up).normalized;
            if (horizontalDirection.sqrMagnitude <= 0.0001f) return desiredDirection;

            Bounds bounds = movementCollider.bounds;
            Vector3 origin = bounds.center + horizontalDirection * GetHorizontalExtent(horizontalDirection);
            origin.y = bounds.max.y + 0.02f;
            float checkDistance = bounds.size.y + maxFallHeight + hoverHeight;
            if (!TryGetGroundRay(origin, Vector3.down, checkDistance, out RaycastHit hit))
                return Vector3.zero;
            if (!IsWalkable(hit.normal)) return Vector3.zero;
            if (hit.point.y < bounds.min.y - maxFallHeight) return Vector3.zero;

            return desiredDirection;
        }

        /// <summary>
        /// 获取当前输入状态对应的水平速度变化加速度。
        /// </summary>
        /// <param name="direction">经地面限制后的移动方向。</param>
        /// <returns>用于本物理步的加速度上限。</returns>
        private float GetHorizontalAcceleration(Vector3 direction)
        {
            if (_isGrounded)
                return direction.sqrMagnitude > 0.0001f ? groundAcceleration : groundDeceleration;

            return airAcceleration * airControl;
        }

        /// <summary>
        /// 清空本物理步的命令，避免策略未提交时残留旧状态。
        /// </summary>
        private void ClearStepCommands()
        {
            _desiredDirection = Vector3.zero;
            _speedMultiplier = 1f;
            _jumpRequested = false;
        }

        /// <summary>
        /// 在实际 Collider 上执行无分配向下 Cast 并选择最近地面命中。
        /// </summary>
        /// <param name="distance">最大检测距离。</param>
        /// <param name="groundHit">最近有效地面命中。</param>
        /// <returns>找到有效地面时返回 true。</returns>
        private bool TryGetGroundCast(float distance, out RaycastHit groundHit)
        {
            groundHit = default;
            int hitCount = CastMovementCollider(Vector3.down, distance);
            float bestDistance = float.PositiveInfinity;

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _castHits[index];
                if (!IsGroundCollider(hit.collider) || hit.distance >= bestDistance) continue;

                bestDistance = hit.distance;
                groundHit = hit;
            }

            return bestDistance < float.PositiveInfinity;
        }

        /// <summary>
        /// 以实际 Capsule 或 Box 形状执行无分配 Cast。
        /// </summary>
        /// <param name="direction">世界空间检测方向。</param>
        /// <param name="distance">最大检测距离。</param>
        /// <returns>写入命中缓冲区的数量。</returns>
        private int CastMovementCollider(Vector3 direction, float distance)
        {
            if (movementCollider is CapsuleCollider capsule)
            {
                GetCapsuleWorldPoints(capsule, out Vector3 point1, out Vector3 point2, out float radius);
                return Physics.CapsuleCastNonAlloc(
                    point1,
                    point2,
                    radius,
                    direction,
                    _castHits,
                    distance,
                    groundLayer,
                    QueryTriggerInteraction.Ignore);
            }

            if (movementCollider is BoxCollider box)
            {
                Vector3 scale = box.transform.lossyScale;
                Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, new Vector3(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.y),
                    Mathf.Abs(scale.z)));
                Vector3 center = box.transform.TransformPoint(box.center);
                return Physics.BoxCastNonAlloc(
                    center,
                    halfExtents,
                    direction,
                    _castHits,
                    box.transform.rotation,
                    distance,
                    groundLayer,
                    QueryTriggerInteraction.Ignore);
            }

            return 0;
        }

        /// <summary>
        /// 将胶囊碰撞体的局部中心、轴向和尺寸转换为世界空间端点。
        /// </summary>
        /// <param name="capsule">待转换的胶囊碰撞体。</param>
        /// <param name="point1">胶囊轴线一端。</param>
        /// <param name="point2">胶囊轴线另一端。</param>
        /// <param name="radius">世界空间胶囊半径。</param>
        private static void GetCapsuleWorldPoints(
            CapsuleCollider capsule,
            out Vector3 point1,
            out Vector3 point2,
            out float radius)
        {
            GetCapsuleWorldPoints(
                capsule,
                capsule.center,
                capsule.radius,
                capsule.height,
                capsule.direction,
                out point1,
                out point2,
                out radius);
        }

        /// <summary>
        /// 将指定的胶囊局部形状转换为世界空间端点，用于绘制基础和有效形状。
        /// </summary>
        private static void GetCapsuleWorldPoints(
            CapsuleCollider capsule,
            Vector3 localCenter,
            float localRadius,
            float localHeight,
            int direction,
            out Vector3 point1,
            out Vector3 point2,
            out float radius)
        {
            Transform colliderTransform = capsule.transform;
            Vector3 scale = colliderTransform.lossyScale;
            Vector3 absoluteScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 localAxis = GetCapsuleLocalAxis(direction);
            float axisScale = direction switch
            {
                0 => absoluteScale.x,
                1 => absoluteScale.y,
                _ => absoluteScale.z
            };
            float perpendicularScale = direction switch
            {
                0 => Mathf.Max(absoluteScale.y, absoluteScale.z),
                1 => Mathf.Max(absoluteScale.x, absoluteScale.z),
                _ => Mathf.Max(absoluteScale.x, absoluteScale.y)
            };

            radius = localRadius * perpendicularScale;
            float halfLineLength = Mathf.Max(0f, localHeight * axisScale * 0.5f - radius);
            Vector3 axis = colliderTransform.TransformDirection(localAxis).normalized;
            Vector3 center = colliderTransform.TransformPoint(localCenter);
            point1 = center + axis * halfLineLength;
            point2 = center - axis * halfLineLength;
        }

        /// <summary>
        /// 执行无分配射线检测并跳过自身碰撞体。
        /// </summary>
        /// <param name="origin">射线起点。</param>
        /// <param name="direction">射线方向。</param>
        /// <param name="distance">最大检测距离。</param>
        /// <param name="groundHit">最近有效地面命中。</param>
        /// <returns>找到有效地面时返回 true。</returns>
        private bool TryGetGroundRay(Vector3 origin, Vector3 direction, float distance, out RaycastHit groundHit)
        {
            groundHit = default;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                _rayHits,
                distance,
                groundLayer,
                QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _rayHits[index];
                if (!IsGroundCollider(hit.collider) || hit.distance >= bestDistance) continue;

                bestDistance = hit.distance;
                groundHit = hit;
            }

            return bestDistance < float.PositiveInfinity;
        }

        /// <summary>
        /// 判断碰撞体是否属于允许检测的外部地面。
        /// </summary>
        /// <param name="collider">待校验碰撞体。</param>
        /// <returns>属于地面层且不在自身层级下时返回 true。</returns>
        private bool IsGroundCollider(Collider collider)
        {
            if (collider == null) return false;
            if (collider.transform == transform || collider.transform.IsChildOf(transform)) return false;

            int colliderMask = 1 << collider.gameObject.layer;
            return (groundLayer.value & colliderMask) != 0;
        }

        /// <summary>
        /// 判断命中法线是否在配置的可行走坡度范围内。
        /// </summary>
        /// <param name="normal">待检查的表面法线。</param>
        /// <returns>可作为地面时返回 true。</returns>
        private bool IsWalkable(Vector3 normal)
        {
            return Vector3.Angle(normal, Vector3.up) <= slopeLimit;
        }

        /// <summary>
        /// 获取 Collider 在给定水平方向上的包围盒投影半径。
        /// </summary>
        /// <param name="direction">已归一化的水平检测方向。</param>
        /// <returns>从中心到包围盒边缘的投影距离。</returns>
        private float GetHorizontalExtent(Vector3 direction)
        {
            Vector3 extents = movementCollider.bounds.extents;
            return Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.z) * extents.z;
        }

        /// <summary>
        /// 判断当前配置的 Collider 是否受 UnitMover 支持。
        /// </summary>
        /// <returns>配置为 CapsuleCollider 或 BoxCollider 时返回 true。</returns>
        private bool HasSupportedCollider(bool reportError = true)
        {
            bool supported = movementCollider is CapsuleCollider || movementCollider is BoxCollider;
            if (!supported && reportError && !_reportedColliderError)
            {
                Debug.LogError($"{name} 的 UnitMover 需要 CapsuleCollider 或 BoxCollider。", this);
                _reportedColliderError = true;
            }

            return supported;
        }

        /// <summary>
        /// 判断类型是否为可实例化的移动策略。
        /// </summary>
        /// <param name="strategyType">待校验类型。</param>
        /// <returns>可创建策略实例时返回 true。</returns>
        private static bool IsConcreteStrategy(Type strategyType)
        {
            return strategyType != null
                && typeof(MovementStrategy).IsAssignableFrom(strategyType)
                && !strategyType.IsAbstract;
        }

        /// <summary>
        /// 将保存的编辑器参数应用到刚创建的策略实例。
        /// </summary>
        /// <param name="strategy">要初始化的策略实例。</param>
        private void ApplyStrategyParams(MovementStrategy strategy)
        {
            if (strategy == null || _strategyParams == null) return;

            Type strategyType = strategy.GetType();
            foreach (StrategyParam param in _strategyParams)
            {
                if (param == null || string.IsNullOrEmpty(param.name)) continue;

                FieldInfo field = strategyType.GetField(param.name, BindingFlags.Instance | BindingFlags.Public);
                if (field == null || field.IsLiteral || field.IsInitOnly) continue;

                object value = param.GetValue(field.FieldType);
                if (value != null) field.SetValue(strategy, value);
            }
        }

        /// <summary>
        /// 复制两个策略间名称和类型都一致的公开实例字段。
        /// </summary>
        /// <param name="source">当前正在运行的策略。</param>
        /// <param name="target">将要启用的新策略。</param>
        private static void MigrateStrategyFields(MovementStrategy source, MovementStrategy target)
        {
            Type sourceType = source.GetType();
            Type targetType = target.GetType();
            foreach (FieldInfo sourceField in sourceType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (sourceField.IsLiteral || sourceField.IsInitOnly) continue;

                FieldInfo targetField = targetType.GetField(sourceField.Name, BindingFlags.Instance | BindingFlags.Public);
                if (targetField == null || targetField.IsLiteral || targetField.IsInitOnly) continue;
                if (targetField.FieldType != sourceField.FieldType) continue;

                targetField.SetValue(target, sourceField.GetValue(source));
            }
        }

        /// <summary>
        /// 策略公开字段的可序列化基础类型容器。
        /// </summary>
        [Serializable]
        public class StrategyParam
        {
            [Tooltip("策略公开字段名称")]
            public string name;

            [Tooltip("策略公开字段的程序集限定类型名")]
            public string typeName;

            [Tooltip("使用固定区域性格式保存的字段值")]
            public string stringValue;

            /// <summary>
            /// 将支持的基础类型或枚举写入字符串存储。
            /// </summary>
            /// <param name="value">要保存的非空字段值。</param>
            public void SetValue(object value)
            {
                if (value == null) return;

                typeName = value.GetType().AssemblyQualifiedName;
                stringValue = value is IFormattable formattable
                    ? formattable.ToString(null, CultureInfo.InvariantCulture)
                    : value.ToString();
            }

            /// <summary>
            /// 将字符串存储转换为目标字段类型。
            /// </summary>
            /// <param name="targetType">策略字段的实际类型。</param>
            /// <returns>成功解析的值；不支持或无效时返回 null。</returns>
            public object GetValue(Type targetType)
            {
                Type valueType = targetType ?? Type.GetType(typeName);
                if (valueType == null || string.IsNullOrEmpty(stringValue)) return null;

                if (valueType.IsEnum)
                {
                    try
                    {
                        return Enum.Parse(valueType, stringValue);
                    }
                    catch (ArgumentException)
                    {
                        return null;
                    }
                }

                if (valueType == typeof(bool))
                    return bool.TryParse(stringValue, out bool booleanValue) ? booleanValue : (object)null;
                if (valueType == typeof(int))
                    return int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue)
                        ? intValue : (object)null;
                if (valueType == typeof(float))
                    return float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue)
                        ? floatValue : (object)null;
                if (valueType == typeof(string)) return stringValue;

                return null;
            }
        }

        /// <summary>
        /// 在编辑模式和运行模式的 Scene 视图中绘制浮动胶囊体的基础与有效形状。
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showHoverPreview || !HasSupportedCollider(false)) return;

            DrawHoverPreview();
        }

        /// <summary>
        /// 在选中对象时额外绘制地面、台阶和悬崖检测范围。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!HasSupportedCollider(false)) return;

#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif

            Bounds bounds = movementCollider.bounds;
            Vector3 bottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            Gizmos.color = _isGrounded ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(bottomCenter, Mathf.Min(bounds.extents.x, bounds.extents.z));
            Gizmos.DrawLine(bottomCenter, bottomCenter + Vector3.down * (hoverHeight + groundProbeDistance));

            if (_isGrounded)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(_groundPoint, _groundPoint + _groundNormal);
            }

            if (_desiredDirection.sqrMagnitude <= 0.0001f) return;

            Vector3 direction = Vector3.ProjectOnPlane(_desiredDirection, Vector3.up).normalized;
            if (direction.sqrMagnitude <= 0.0001f) return;

            float extent = GetHorizontalExtent(direction) + StepProbePadding;
            Vector3 lowerOrigin = new Vector3(bounds.center.x, bounds.min.y + 0.02f, bounds.center.z);
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(lowerOrigin, lowerOrigin + direction * extent);
            Gizmos.DrawLine(lowerOrigin + Vector3.up * stepHeight, lowerOrigin + Vector3.up * stepHeight + direction * extent);

            Vector3 ledgeOrigin = bounds.center + direction * GetHorizontalExtent(direction);
            ledgeOrigin.y = bounds.max.y + 0.02f;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(ledgeOrigin, ledgeOrigin + Vector3.down * (bounds.size.y + maxFallHeight + hoverHeight));
        }

        /// <summary>
        /// 绘制基础胶囊体、有效碰撞胶囊体及底部无碰撞空腔。
        /// </summary>
        private void DrawHoverPreview()
        {
            if (movementCollider is CapsuleCollider capsule
                && _baseCapsuleShape.isInitialized
                && _snapshotCapsuleCollider == capsule)
            {
                DrawCapsuleOutline(capsule, _baseCapsuleShape, new Color(0.2f, 0.9f, 1f, 0.9f));
                if (!enableFloatingCapsule) return;

                CapsuleShapeSnapshot effectiveShape = CaptureCapsuleShape(capsule);
                float clearance = GetClampedFloatingClearance(_baseCapsuleShape);
                DrawCapsuleOutline(capsule, effectiveShape, new Color(1f, 0.72f, 0.1f, 0.95f));
                DrawFloatingCapsuleGap(capsule, _baseCapsuleShape, clearance);
                return;
            }

            DrawColliderOutline(Vector3.zero, new Color(0.2f, 0.9f, 1f, 0.9f));
        }

        /// <summary>
        /// 绘制基础胶囊体底部与实际碰撞体底部之间的无碰撞空腔。
        /// </summary>
        private void DrawFloatingCapsuleGap(
            CapsuleCollider capsule,
            CapsuleShapeSnapshot baseShape,
            float clearance)
        {
            if (clearance <= 0.0001f) return;

            Vector3 localAxis = GetCapsuleLocalAxis(baseShape.direction);
            Vector3 baseBottom = baseShape.center - localAxis * (baseShape.height * 0.5f);
            Vector3 effectiveBottom = baseBottom + localAxis * clearance;
            Vector3 gapCenter = Vector3.Lerp(baseBottom, effectiveBottom, 0.5f);
            float diameter = baseShape.radius * 2f;
            Vector3 gapSize = baseShape.direction switch
            {
                0 => new Vector3(clearance, diameter, diameter),
                1 => new Vector3(diameter, clearance, diameter),
                _ => new Vector3(diameter, diameter, clearance)
            };

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = capsule.transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.72f, 0.1f, 0.12f);
            Gizmos.DrawCube(gapCenter, gapSize);
            Gizmos.color = new Color(1f, 0.72f, 0.1f, 0.85f);
            Gizmos.DrawWireCube(gapCenter, gapSize);
            Gizmos.matrix = previousMatrix;

#if UNITY_EDITOR
            Vector3 worldBaseBottom = capsule.transform.TransformPoint(baseBottom);
            Vector3 worldEffectiveBottom = capsule.transform.TransformPoint(effectiveBottom);
            Handles.color = new Color(1f, 0.72f, 0.1f, 0.9f);
            Handles.DrawDottedLine(worldBaseBottom, worldEffectiveBottom, 4f);
            Handles.Label(
                worldEffectiveBottom + Vector3.right * 0.05f,
                $"Floating Gap: {clearance:0.###} m\nEffective Height: {baseShape.height - clearance:0.###} m",
                EditorStyles.miniBoldLabel);
#endif
        }

        /// <summary>
        /// 使用指定局部形状绘制胶囊体轮廓。
        /// </summary>
        private static void DrawCapsuleOutline(
            CapsuleCollider capsule,
            CapsuleShapeSnapshot shape,
            Color color)
        {
            GetCapsuleWorldPoints(
                capsule,
                shape.center,
                shape.radius,
                shape.height,
                shape.direction,
                out Vector3 point1,
                out Vector3 point2,
                out float radius);
            Gizmos.color = color;
            DrawWireCapsule(point1, point2, radius);
        }

        /// <summary>
        /// 根据当前支持的碰撞体类型绘制指定世界空间偏移后的轮廓。
        /// </summary>
        /// <param name="worldOffset">施加到碰撞体轮廓的世界空间偏移。</param>
        /// <param name="color">轮廓绘制颜色。</param>
        private void DrawColliderOutline(Vector3 worldOffset, Color color)
        {
            Gizmos.color = color;
            if (movementCollider is CapsuleCollider capsule)
            {
                GetCapsuleWorldPoints(capsule, out Vector3 point1, out Vector3 point2, out float radius);
                DrawWireCapsule(point1 + worldOffset, point2 + worldOffset, radius);
                return;
            }

            if (movementCollider is BoxCollider box)
            {
                Matrix4x4 previousMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(
                    box.transform.position + worldOffset,
                    box.transform.rotation,
                    box.transform.lossyScale);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = previousMatrix;
            }
        }

        /// <summary>
        /// 使用两个端点和半径绘制胶囊体线框。
        /// </summary>
        /// <param name="point1">胶囊轴线一端。</param>
        /// <param name="point2">胶囊轴线另一端。</param>
        /// <param name="radius">胶囊世界空间半径。</param>
        private static void DrawWireCapsule(Vector3 point1, Vector3 point2, float radius)
        {
            Gizmos.DrawWireSphere(point1, radius);
            Gizmos.DrawWireSphere(point2, radius);

            Vector3 axis = point1 - point2;
            if (axis.sqrMagnitude <= 0.0001f) return;

            axis.Normalize();
            Vector3 tangent = Vector3.Cross(axis, Vector3.up);
            if (tangent.sqrMagnitude <= 0.0001f)
                tangent = Vector3.Cross(axis, Vector3.right);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(axis, tangent).normalized;

            Gizmos.DrawLine(point1 + tangent * radius, point2 + tangent * radius);
            Gizmos.DrawLine(point1 - tangent * radius, point2 - tangent * radius);
            Gizmos.DrawLine(point1 + bitangent * radius, point2 + bitangent * radius);
            Gizmos.DrawLine(point1 - bitangent * radius, point2 - bitangent * radius);
        }
    }
}
