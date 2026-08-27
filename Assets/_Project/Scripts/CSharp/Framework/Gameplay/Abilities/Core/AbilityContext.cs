using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>提供能力访问单位和共享运行时数据的上下文。</summary>
    public sealed class AbilityContext
    {
        // 上下文数据表：语义枚举键 → 实现 IAbilityContextData 的运行时数据。
        private readonly Dictionary<AbilityContextDataType, IAbilityContextData> _data
            = new Dictionary<AbilityContextDataType, IAbilityContextData>();
        // 能力所属单位对象。
        public GameObject Owner { get; }
        // 能力所属单位 Transform。
        public Transform Transform { get; }

        /// <summary>创建单位能力上下文。</summary>
        /// <param name="owner">能力所属单位对象。</param>
        public AbilityContext(GameObject owner)
        {
            Owner = owner;
            Transform = owner != null ? owner.transform : null;
        }

        /// <summary>注册一个能力共享数据对象。</summary>
        /// <param name="type">数据在上下文中的语义键。</param>
        /// <param name="data">实现上下文数据接口的数据对象；不允许为 null。</param>
        public void Register<T>(AbilityContextDataType type, T data)
            where T : class, IAbilityContextData
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (_data.TryGetValue(type, out IAbilityContextData existing))
            {
                if (ReferenceEquals(existing, data)) return;
                throw new InvalidOperationException($"Ability context data key {type} is already registered.");
            }

            _data.Add(type, data);
        }

        /// <summary>注销指定语义键对应的数据对象。</summary>
        /// <param name="type">需要注销的数据语义键。</param>
        /// <returns>成功移除数据时返回 true。</returns>
        public bool Unregister(AbilityContextDataType type)
        {
            return _data.Remove(type);
        }

        /// <summary>仅当指定实例仍是当前注册对象时注销数据。</summary>
        /// <param name="type">需要注销的数据语义键。</param>
        /// <param name="expectedData">期望被注销的数据实例。</param>
        /// <returns>实例匹配并成功移除时返回 true。</returns>
        public bool Unregister<T>(AbilityContextDataType type, T expectedData)
            where T : class, IAbilityContextData
        {
            if (!_data.TryGetValue(type, out IAbilityContextData current)) return false;
            if (!ReferenceEquals(current, expectedData)) return false;
            return _data.Remove(type);
        }

        /// <summary>安全获取指定语义键下的强类型数据。</summary>
        /// <param name="type">需要访问的数据语义键。</param>
        /// <param name="data">找到且类型匹配时返回数据实例。</param>
        /// <returns>找到并成功转换时返回 true。</returns>
        public bool TryGet<T>(AbilityContextDataType type, out T data)
            where T : class, IAbilityContextData
        {
            if (_data.TryGetValue(type, out IAbilityContextData value) && value is T typed)
            {
                data = typed;
                return true;
            }

            data = null;
            return false;
        }

        /// <summary>强制获取指定语义键下的强类型数据。</summary>
        /// <param name="type">需要访问的数据语义键。</param>
        /// <returns>找到且类型匹配的数据实例。</returns>
        /// <exception cref="InvalidOperationException">数据不存在或注册类型不匹配时抛出。</exception>
        public T Get<T>(AbilityContextDataType type)
            where T : class, IAbilityContextData
        {
            if (TryGet(type, out T data)) return data;
            if (!_data.TryGetValue(type, out IAbilityContextData value))
                throw new InvalidOperationException($"Ability context data key {type} is not registered.");
            throw new InvalidOperationException(
                $"Ability context data key {type} contains {value.GetType().Name}, not {typeof(T).Name}.");
        }

        /// <summary>重置指定语义键对应的数据对象。</summary>
        /// <param name="type">需要重置的数据语义键。</param>
        /// <returns>找到数据并执行重置时返回 true。</returns>
        public bool ResetData(AbilityContextDataType type)
        {
            if (!_data.TryGetValue(type, out IAbilityContextData data)) return false;
            data.Reset();
            return true;
        }

        /// <summary>重置上下文中全部已注册数据的运行时状态。</summary>
        public void ResetAll()
        {
            foreach (IAbilityContextData data in _data.Values) data.Reset();
        }
    }
}
