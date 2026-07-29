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
        /// 预初始化当前 Provider 的数据源处理器，减少首个 Tick 的工作量。
        /// </summary>
        protected virtual void Awake()
        {
            EnsureDataSourceInitialized();
        }

        /// <summary>
        /// 执行一次数据采集并写入专用 Blackboard；编辑器或开发构建中随后调用可选调试钩子。
        /// </summary>
        public virtual void Tick()
        {
            // Unity 生命周期遗漏或脚本重载后，首个数据消费必须仍能完成初始化。
            EnsureDataSourceInitialized();

            _initializedDataSource.Process(Blackboard);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugData(Blackboard);
#endif
        }

        /// <summary>
        /// 确保当前数据源处理器已与所属 GameObject 完成一次且仅一次初始化。
        /// </summary>
        private void EnsureDataSourceInitialized()
        {
            if (_initializedDataSource != null) return;

            // Provider 必须先提供类型化 Blackboard，才能安全写入数据。
            if (Blackboard == null)
                throw new System.InvalidOperationException($"{GetType().Name} must provide a Blackboard instance.");

            // 在处理器初始化成功后再写入缓存，避免失败状态被误当作可用。
            DataSourceHandler<TBlackboard> dataSource = DataSource;
            if (dataSource == null)
                throw new System.InvalidOperationException($"{GetType().Name} must provide a DataSourceHandler instance.");

            dataSource.Initialize(gameObject);
            _initializedDataSource = dataSource;
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
