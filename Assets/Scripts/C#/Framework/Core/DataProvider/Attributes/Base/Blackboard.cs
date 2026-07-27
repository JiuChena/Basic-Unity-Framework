using System;
using System.Collections.Generic;

namespace Framework.Core
{
    /// <summary>
    /// 实体运行时属性容器。
    /// 以属性类型为键（每种具体属性类型只能注册一个实例），
    /// 字典只存属性对象引用，不存值类型数据，保证零装箱。
    /// </summary>
    public sealed class Blackboard
    {
        // 属性注册表：Type → BlackboardAttribute 实例引用
        private readonly Dictionary<Type, BlackboardAttribute> _attributes = new Dictionary<Type, BlackboardAttribute>();

        #region Register

        /// <summary>
        /// 注册一项具体属性。重复注册同一属性类型视为 Provider 配置错误。
        /// </summary>
        /// <typeparam name="TAttribute">属性具体类型</typeparam>
        /// <param name="attribute">属性实例</param>
        /// <returns>返回传入的属性实例，方便调用方链式缓存</returns>
        /// <exception cref="ArgumentNullException">attribute 为 null</exception>
        /// <exception cref="InvalidOperationException">同类型属性已注册</exception>
        public TAttribute Register<TAttribute>(TAttribute attribute)
            where TAttribute : BlackboardAttribute
        {
            if (attribute == null) throw new ArgumentNullException(nameof(attribute));

            Type attributeType = typeof(TAttribute);

            // 重复注册检查
            if (_attributes.ContainsKey(attributeType))
                throw new InvalidOperationException($"Blackboard already contains {attributeType.Name}.");

            _attributes.Add(attributeType, attribute);
            return attribute;
        }

        #endregion

        #region Query

        /// <summary>
        /// 获取已注册的具体属性实例。未注册时返回 null。
        /// </summary>
        /// <typeparam name="TAttribute">属性具体类型</typeparam>
        /// <returns>属性实例，未注册时返回 null</returns>
        public TAttribute Get<TAttribute>() where TAttribute : BlackboardAttribute
        {
            return _attributes.TryGetValue(typeof(TAttribute), out BlackboardAttribute attribute)
                ? attribute as TAttribute
                : null;
        }

        /// <summary>
        /// 尝试获取已注册的具体属性实例。
        /// </summary>
        /// <typeparam name="TAttribute">属性具体类型</typeparam>
        /// <param name="attribute">成功时返回属性实例，否则为 null</param>
        /// <returns>属性已注册且类型匹配时返回 true</returns>
        public bool TryGet<TAttribute>(out TAttribute attribute)
            where TAttribute : BlackboardAttribute
        {
            attribute = Get<TAttribute>();
            return attribute != null;
        }

        /// <summary>
        /// 检查指定类型的属性是否已注册。
        /// </summary>
        public bool Has<TAttribute>() where TAttribute : BlackboardAttribute
        {
            return _attributes.ContainsKey(typeof(TAttribute));
        }

        #endregion

        #region Remove / Clear

        /// <summary>
        /// 移除指定类型的已注册属性。
        /// </summary>
        /// <returns>属性存在并已移除时返回 true</returns>
        public bool Remove<TAttribute>() where TAttribute : BlackboardAttribute
        {
            return _attributes.Remove(typeof(TAttribute));
        }

        /// <summary>
        /// 清空所有已注册属性。
        /// </summary>
        public void Clear()
        {
            _attributes.Clear();
        }

        #endregion
    }
}
