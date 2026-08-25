using System;
using System.Collections.Generic;
using Framework.ExpandComponent.DataProvider;
using Framework.ExpandComponent.UnitMover;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 提供能力访问单位组件、黑板和共享运动帧状态的运行时上下文。
    /// </summary>
    public sealed class AbilityContext
    {
        // 能力所属单位对象。
        public GameObject Owner { get; }
        // 能力所属单位 Transform。
        public Transform Transform { get; }
        // 能力所属单位刚体。
        public Rigidbody Rigidbody { get; }
        // 能力所属单位主胶囊。
        public CapsuleCollider MovementCollider { get; }
        // 统一写入刚体的物理适配器。
        public IUnitBody Body { get; }
        // 统一无分配物理查询适配器。
        public IPhysicsQuery PhysicsQuery { get; }
        // 单位独占的运行时数据黑板。
        public Blackboard Blackboard { get; private set; }
        // 当前固定帧可由移动能力提交并由其他能力继续叠加的速度。
        public Vector3 Velocity { get; set; }
        // 当前固定帧供跳跃和边缘保护读取的运动状态。
        public UnitMovementState MovementState { get; private set; }
        // 当前固定帧供能力解释的移动命令。
        public UnitMovementCommand MovementCommand { get; private set; }

        // 能力间共享服务：服务类型 → 单位级运行时实例。
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>
        /// 创建单位能力上下文并缓存 Unity 适配器依赖。
        /// </summary>
        /// <param name="owner">能力所属单位对象。</param>
        /// <param name="rigidbody">单位刚体。</param>
        /// <param name="movementCollider">单位主胶囊。</param>
        /// <param name="body">统一刚体写入适配器。</param>
        /// <param name="physicsQuery">无分配物理查询适配器。</param>
        /// <param name="blackboard">单位独占黑板；允许为 null。</param>
        public AbilityContext(
            GameObject owner,
            Rigidbody rigidbody,
            CapsuleCollider movementCollider,
            IUnitBody body,
            IPhysicsQuery physicsQuery,
            Blackboard blackboard)
        {
            Owner = owner;
            Transform = owner != null ? owner.transform : null;
            Rigidbody = rigidbody;
            MovementCollider = movementCollider;
            Body = body;
            PhysicsQuery = physicsQuery;
            Blackboard = blackboard;
            Velocity = body != null && body.IsValid ? body.Velocity : Vector3.zero;
            MovementCommand = UnitMovementCommand.CreateDefault();
        }

        /// <summary>更新当前固定帧供能力消费的运动状态和命令。</summary>
        /// <param name="state">当前接地、坡面和运动模式状态。</param>
        /// <param name="command">当前帧移动输入或 AI 命令。</param>
        public void SetMovementFrame(in UnitMovementState state, in UnitMovementCommand command)
        {
            MovementState = state;
            MovementCommand = command;
        }

        /// <summary>绑定能力系统创建的单位独占黑板。</summary>
        /// <param name="blackboard">输入监听能力创建的单位黑板。</param>
        public void SetBlackboard(Blackboard blackboard)
        {
            Blackboard = blackboard;
        }

        /// <summary>注册单位级共享运行时服务。</summary>
        /// <typeparam name="TService">服务类型。</typeparam>
        /// <param name="service">服务实例；传入 null 会移除该类型服务。</param>
        public void RegisterService<TService>(TService service) where TService : class
        {
            Type serviceType = typeof(TService);
            if (service == null)
            {
                _services.Remove(serviceType);
                return;
            }

            _services[serviceType] = service;
        }

        /// <summary>读取单位级共享运行时服务。</summary>
        /// <typeparam name="TService">服务类型。</typeparam>
        /// <returns>已注册服务；不存在时返回 null。</returns>
        public TService GetService<TService>() where TService : class
        {
            return _services.TryGetValue(typeof(TService), out object service)
                ? service as TService
                : null;
        }

        /// <summary>提交当前固定帧合并后的速度到刚体。</summary>
        public void CommitVelocity()
        {
            Body?.Commit(Velocity);
        }

        /// <summary>清理上下文注册的所有单位级服务。</summary>
        public void ClearServices()
        {
            _services.Clear();
        }
    }
}
