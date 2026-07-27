using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// IDataProvider 的统一 MonoBehaviour 生命周期实现。
    /// 子类在 RegisterAttributes 中声明输出的属性，在 Tick 中更新它们。
    /// </summary>
    public abstract class DataProviderBase : MonoBehaviour, IDataProvider
    {
        /// <summary>
        /// 当前实体持有的属性黑板。Awake 中自动创建。
        /// </summary>
        public Blackboard Blackboard { get; private set; }

        #region Lifecycle

        /// <summary>
        /// 创建 Blackboard 并调用子类的 RegisterAttributes 注册属性。
        /// </summary>
        protected virtual void Awake()
        {
            Blackboard = new Blackboard();
            RegisterAttributes(Blackboard);
        }

        /// <summary>
        /// 每帧驱动 Tick 执行数据采集。
        /// </summary>
        protected virtual void Update()
        {
            Tick();
        }

        #endregion

        #region Abstract

        /// <summary>
        /// 子类在此方法中注册此 Provider 负责输出的全部属性。
        /// </summary>
        /// <param name="board">当前实体的属性黑板</param>
        protected abstract void RegisterAttributes(Blackboard board);

        /// <summary>
        /// 每帧从数据源采集数据并写入已注册的属性。
        /// </summary>
        public abstract void Tick();

        #endregion

        #region Helpers

        /// <summary>
        /// 注册属性并返回其实例，方便子类在 RegisterAttributes 中链式缓存。
        /// </summary>
        /// <typeparam name="TAttribute">属性具体类型</typeparam>
        /// <param name="attribute">属性实例</param>
        /// <returns>返回传入的属性实例</returns>
        protected TAttribute Register<TAttribute>(TAttribute attribute)
            where TAttribute : BlackboardAttribute
        {
            return Blackboard.Register(attribute);
        }

        #endregion
    }
}
