using System;
using System.Collections.Generic;

namespace CoreFramework
{
    /// <summary>
    /// 可存入 Blackboard 的数据对象标记接口。
    /// </summary>
    public interface IBlackboardData
    {
    }

    /// <summary>
    /// 按数据类型共享运行时状态的容器。
    /// </summary>
    public sealed class Blackboard
    {
        // 数据槽映射：数据类型 -> 唯一实例。
        private readonly Dictionary<Type, IBlackboardData> _slots = new Dictionary<Type, IBlackboardData>();

        /// <summary>
        /// 注册或替换指定类型的数据槽。
        /// </summary>
        /// <typeparam name="T">数据槽类型。</typeparam>
        /// <param name="data">要注册的数据实例，不能为空。</param>
        public void Set<T>(T data) where T : class, IBlackboardData
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _slots[typeof(T)] = data;
        }

        /// <summary>
        /// 获取指定数据槽，不存在时创建默认实例。
        /// </summary>
        /// <typeparam name="T">具备无参构造的数据槽类型。</typeparam>
        /// <returns>已有或新建的数据实例。</returns>
        public T GetOrCreate<T>() where T : class, IBlackboardData, new()
        {
            if (TryGet(out T data)) return data;

            data = new T();
            Set(data);
            return data;
        }

        /// <summary>
        /// 尝试获取已注册的数据槽，不会创建新实例。
        /// </summary>
        /// <typeparam name="T">数据槽类型。</typeparam>
        /// <param name="data">成功时返回数据实例，否则为 null。</param>
        /// <returns>存在对应类型数据槽时返回 true。</returns>
        public bool TryGet<T>(out T data) where T : class, IBlackboardData
        {
            if (_slots.TryGetValue(typeof(T), out IBlackboardData slot))
            {
                data = slot as T;
                return data != null;
            }

            data = null;
            return false;
        }

        /// <summary>
        /// 移除指定类型的数据槽。
        /// </summary>
        /// <typeparam name="T">数据槽类型。</typeparam>
        /// <returns>存在并已移除数据槽时返回 true。</returns>
        public bool Remove<T>() where T : class, IBlackboardData
        {
            return _slots.Remove(typeof(T));
        }

        /// <summary>
        /// 清除所有数据槽引用。
        /// </summary>
        public void Clear()
        {
            _slots.Clear();
        }
    }
}
