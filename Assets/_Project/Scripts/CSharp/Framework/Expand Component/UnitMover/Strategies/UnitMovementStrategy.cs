using System;
using Framework.ExpandComponent.DataProvider;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 定义 UnitMover 可缓存、切换和执行的纯 C# 移动策略。
    /// </summary>
    [Serializable]
    public abstract class UnitMovementStrategy
    {
        // 策略本次启用周期绑定的刚体组件。
        [NonSerialized] protected Rigidbody Rigidbody;
        // 策略本次启用周期绑定的唯一主胶囊碰撞体。
        [NonSerialized] protected CapsuleCollider MovementCollider;
        // 策略本次启用周期绑定的实体 Transform。
        [NonSerialized] protected Transform OwnerTransform;
        // 策略提交最终速度的刚体适配器。
        [NonSerialized] protected IUnitBody Body;
        // 策略及其物理模块复用的无分配查询适配器。
        [NonSerialized] protected IPhysicsQuery PhysicsQuery;
        // 策略自行解释的实体数据提供器。
        [NonSerialized] protected IDataProvider DataProvider;
        // 用于将输入转换为世界空间方向的可选参考 Transform。
        [NonSerialized] protected Transform MovementReference;
        // UnitMover 注入的唯一生命周期容器，策略只向其中注册当前模块需要的无参包装方法。
        [NonSerialized] protected UnitMoverLifecycleContainer Lifecycle;
        // 编辑模式 Authoring 回调当前使用的主胶囊碰撞体。
        [NonSerialized] protected CapsuleCollider AuthoringMovementCollider;
        // 编辑模式 Authoring 回调当前使用的实体根 Transform。
        [NonSerialized] protected Transform AuthoringOwnerTransform;

        /// <summary>获取 Inspector 和运行时诊断使用的策略名称。</summary>
        public virtual string DisplayName => GetType().Name;

        /// <summary>获取最近一次完成固定步的只读状态。</summary>
        public virtual UnitMovementState State => default;

        /// <summary>获取最近一次策略解释得到的通用移动命令。</summary>
        public virtual UnitMovementCommand LastCommand => UnitMovementCommand.CreateDefault();

        /// <summary>获取最近一次尚未完成全部修正的候选速度。</summary>
        public virtual Vector3 LastCandidateVelocity => Vector3.zero;

        /// <summary>获取最近一次提交给刚体的最终速度。</summary>
        public virtual Vector3 LastCommittedVelocity => Vector3.zero;

        /// <summary>获取边缘保护调试状态；策略未使用该模块时返回 null。</summary>
        public virtual EdgeProtectionDebugState EdgeDebugState => null;

        /// <summary>获取用于编辑器预览的形状模块；策略未使用胶囊形状时返回 null。</summary>
        public virtual ColliderShapeModule ShapeModule => null;

        /// <summary>获取用于编辑器预览的浮动胶囊配置；策略未使用时返回 null。</summary>
        public virtual FloatingCapsuleModule FloatingCapsuleModule => null;

        /// <summary>获取用于编辑器预览的接地配置；策略未使用时返回 null。</summary>
        public virtual GroundSettings GroundSettings => null;

        /// <summary>
        /// 由 UnitMover 在策略首次加入当前启用周期缓存时显式注入运行依赖。
        /// </summary>
        /// <param name="rigidbody">同对象的刚体组件。</param>
        /// <param name="movementCollider">同对象的唯一主胶囊碰撞体。</param>
        /// <param name="ownerTransform">实体根 Transform。</param>
        /// <param name="body">统一写入刚体速度的适配器。</param>
        /// <param name="physicsQuery">无分配物理查询适配器。</param>
        /// <param name="dataProvider">同对象的实体数据提供器；允许为 null。</param>
        /// <param name="movementReference">可选移动参考 Transform；允许为 null。</param>
        internal void Initialize(
            Rigidbody rigidbody,
            CapsuleCollider movementCollider,
            Transform ownerTransform,
            IUnitBody body,
            IPhysicsQuery physicsQuery,
            IDataProvider dataProvider,
            Transform movementReference)
        {
            // 缓存由 UnitMover 一次性解析的稳定引用，固定步中不再执行组件或场景查找。
            Rigidbody = rigidbody;
            MovementCollider = movementCollider;
            OwnerTransform = ownerTransform;
            Body = body;
            PhysicsQuery = physicsQuery;
            DataProvider = dataProvider;
            MovementReference = movementReference;
            OnInitialized();
        }

        /// <summary>
        /// 刷新当前启用周期内可变的外部数据引用，不重新创建策略模块或清除策略状态。
        /// </summary>
        /// <param name="dataProvider">同对象的实体数据提供器；允许为 null。</param>
        /// <param name="movementReference">可选移动参考 Transform；允许为 null。</param>
        internal void RefreshRuntimeDependencies(IDataProvider dataProvider, Transform movementReference)
        {
            // 策略只接收 UnitMover 已解析完成的依赖，仍由自身决定如何解释 Provider 的黑板数据。
            DataProvider = dataProvider;
            MovementReference = movementReference;
        }

        /// <summary>
        /// 绑定 UnitMover 提供的生命周期容器，并由策略重新注入当前模块需要的无参回调。
        /// </summary>
        /// <param name="lifecycle">UnitMover 当前持有且已清空的生命周期容器。</param>
        internal void BindLifecycle(UnitMoverLifecycleContainer lifecycle)
        {
            Lifecycle = lifecycle;
            OnRegisterLifecycle();
        }

        /// <summary>
        /// 更新 Authoring 生命周期回调读取的 Unity 组件引用。
        /// </summary>
        /// <param name="movementCollider">当前指定的主胶囊碰撞体。</param>
        /// <param name="ownerTransform">实体根 Transform。</param>
        internal void SetAuthoringDependencies(CapsuleCollider movementCollider, Transform ownerTransform)
        {
            AuthoringMovementCollider = movementCollider;
            AuthoringOwnerTransform = ownerTransform;
        }

        /// <summary>
        /// 清除 UnitMover 注入的运行时和 Authoring 依赖引用。
        /// </summary>
        protected void ClearInjectedDependencies()
        {
            Rigidbody = null;
            MovementCollider = null;
            OwnerTransform = null;
            Body = null;
            PhysicsQuery = null;
            DataProvider = null;
            MovementReference = null;
            Lifecycle = null;
            AuthoringMovementCollider = null;
            AuthoringOwnerTransform = null;
        }

        /// <summary>
        /// 将当前策略所管理的实体状态记录为显式恢复检查点；未支持时不执行操作。
        /// </summary>
        public virtual void SetCheckpoint()
        {
        }

        /// <summary>
        /// 恢复当前策略维护的最近检查点；未支持或不存在检查点时返回 false。
        /// </summary>
        /// <returns>策略支持检查点且已完成恢复时返回 true。</returns>
        public virtual bool RestoreCheckpoint()
        {
            return false;
        }

        /// <summary>
        /// 在策略首次获得运行依赖后创建其运行时模块并缓存所需数据契约。
        /// </summary>
        protected abstract void OnInitialized();

        /// <summary>
        /// 在 UnitMover 每次绑定容器时按顺序注入当前策略需要的无参生命周期包装方法。
        /// </summary>
        protected abstract void OnRegisterLifecycle();

        /// <summary>
        /// 清空当前策略实例持有的全部运行时状态，保留 Inspector 配置。
        /// </summary>
        public abstract void ClearState();
    }
}
