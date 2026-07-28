using UnityEngine;

namespace Framework.ExpandComponent.DataProvider
{
    /// <summary>
    /// IDataProvider 的泛型 MonoBehaviour 生命周期实现。
    /// 子类只装配具体 Blackboard 与 DataSourceHandler，属性声明和数据处理不留在 Provider。
    /// </summary>
    public abstract class DataProviderBase<TBlackboard> : MonoBehaviour, IDataProvider
        where TBlackboard : Blackboard
    {
        // 已完成初始化并负责处理当前 Provider 数据源的纯 C# 处理器。
        private DataSourceHandler<TBlackboard> _initializedDataSource;

        /// <summary>供具体 Provider 使用的类型化黑板。</summary>
        public abstract TBlackboard Blackboard { get; }

        /// <summary>供 UnitMover 等通用消费者使用的基类黑板。</summary>
        Blackboard IDataProvider.Blackboard => Blackboard;

        /// <summary>当前 Provider 使用的数据源处理器。</summary>
        protected abstract DataSourceHandler<TBlackboard> DataSource { get; }

        /// <summary>
        /// 初始化当前 Provider 的数据源处理器。
        /// </summary>
        protected virtual void Awake()
        {
            if (Blackboard == null)
                throw new System.InvalidOperationException($"{GetType().Name} must provide a Blackboard instance.");

            _initializedDataSource = DataSource;
            if (_initializedDataSource == null)
                throw new System.InvalidOperationException($"{GetType().Name} must provide a DataSourceHandler instance.");

            _initializedDataSource.Initialize(gameObject);
        }

        /// <summary>
        /// 执行一次数据采集并写入专用 Blackboard；编辑器或开发构建中随后调用可选调试钩子。
        /// </summary>
        public virtual void Tick()
        {
            if (_initializedDataSource == null)
                throw new System.InvalidOperationException($"{GetType().Name} must be initialized before Tick is called.");

            _initializedDataSource.Process(Blackboard);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugData(Blackboard);
#endif
        }

        /// <summary>
        /// 输出当前处理后的最新黑板数据。
        /// 默认不执行任何操作，具体 Provider 可选择覆写以提供自身的调试信息。
        /// </summary>
        /// <param name="blackboard">本次 Tick 已完成更新的类型化黑板。</param>
        protected virtual void DebugData(TBlackboard blackboard) { }

        /// <summary>
        /// 释放数据源处理器持有的运行时资源。
        /// </summary>
        protected virtual void OnDestroy()
        {
            _initializedDataSource?.Dispose();
            _initializedDataSource = null;
        }
    }
}
