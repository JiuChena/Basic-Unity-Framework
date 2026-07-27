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
        private DataSourceHandler<TBlackboard> _initializedDataSource;

        /// <summary>供具体 Provider 使用的类型化黑板。</summary>
        public abstract TBlackboard Blackboard { get; }

        /// <summary>供 UnitMover 等通用消费者使用的基类黑板。</summary>
        Blackboard IDataProvider.Blackboard => Blackboard;

        /// <summary>当前 Provider 使用的数据源处理器。</summary>
        protected abstract DataSourceHandler<TBlackboard> DataSource { get; }

        protected virtual void Awake()
        {
            if (Blackboard == null)
                throw new System.InvalidOperationException($"{GetType().Name} must provide a Blackboard instance.");

            _initializedDataSource = DataSource;
            if (_initializedDataSource == null)
                throw new System.InvalidOperationException($"{GetType().Name} must provide a DataSourceHandler instance.");

            _initializedDataSource.Initialize(gameObject);
        }

        /// <summary>每帧由数据源处理器采集并写入专用 Blackboard。</summary>
        public virtual void Tick()
        {
            if (_initializedDataSource == null)
                throw new System.InvalidOperationException($"{GetType().Name} must be initialized before Tick is called.");

            _initializedDataSource.Process(Blackboard);
        }

        protected virtual void OnDestroy()
        {
            _initializedDataSource?.Dispose();
            _initializedDataSource = null;
        }
    }
}
