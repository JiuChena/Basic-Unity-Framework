using System;
using UnityEngine;

namespace Framework.ExpandComponent.DataProvider
{
    /// <summary>
    /// 纯 C# 数据源处理器基类。处理器自行声明配置、获取所需组件，并将采集结果写入黑板。
    /// </summary>
    [Serializable]
    public abstract class DataSourceHandler : IDisposable
    {
        protected GameObject Owner { get; private set; }
        private bool _isInitialized;
        private bool _isDisposed;

        public void Initialize(GameObject owner)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().Name);
            if (_isInitialized)
                throw new InvalidOperationException($"{GetType().Name} has already been initialized.");

            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            OnInitialize();
            _isInitialized = true;
        }

        protected virtual void OnInitialize() { }

        protected virtual void OnDispose() { }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            if (_isInitialized) OnDispose();
            Owner = null;
        }

        protected void EnsureInitialized()
        {
            if (!_isInitialized || _isDisposed)
                throw new InvalidOperationException($"{GetType().Name} must be initialized and not disposed before processing data.");
        }

        public abstract void Process(Blackboard blackboard);
    }

    /// <summary>为特定 Blackboard 提供类型安全处理入口的数据源处理器。</summary>
    [Serializable]
    public abstract class DataSourceHandler<TBlackboard> : DataSourceHandler
        where TBlackboard : Blackboard
    {
        public sealed override void Process(Blackboard blackboard)
        {
            EnsureInitialized();
            if (blackboard is not TBlackboard typedBlackboard)
                throw new ArgumentException($"Expected {typeof(TBlackboard).Name} but received {blackboard?.GetType().Name ?? "null"}.", nameof(blackboard));

            ProcessData(typedBlackboard);
        }

        protected abstract void ProcessData(TBlackboard blackboard);
    }
}
