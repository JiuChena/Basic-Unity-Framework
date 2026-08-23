using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace Core.Gear
{
    /// <summary>
    /// 项目级 Formatter 解析器，集中注册所有自定义 MessagePack Formatter。
    /// </summary>
    public sealed class ProjectSaveResolver : IFormatterResolver
    {
        // 静态实例，供 MessagePackRuntime 直接引用
        public static readonly IFormatterResolver Instance = new ProjectSaveResolver();

        // 类型 → Formatter 实例映射表
        private static readonly Dictionary<Type, object> FormatterMap = new Dictionary<Type, object>();

        static ProjectSaveResolver()
        {
            // 注册各业务模块的自定义 Formatter（Formatter 类随业务数据所在模块存放，注册集中在此）
            Register(new AudioDataFormatter());
            Register(new BagDataFormatter());
            Register(new QuestProgressFormatter());
            Register(new QuestSaveDataFormatter());
        }
        
        private ProjectSaveResolver() { }
        
        /// <summary>
        /// 注册自定义 Formatter。
        /// </summary>
        /// <typeparam name="T">Formatter 对应的数据类型。</typeparam>
        /// <param name="formatter">要注册的 Formatter 实例。</param>
        public static void Register<T>(IMessagePackFormatter<T> formatter)
        {
            FormatterMap[typeof(T)] = formatter;
        }
        
        /// <summary>
        /// 按类型查找 Formatter。
        /// </summary>
        /// <typeparam name="T">需要查找的 Formatter 数据类型。</typeparam>
        /// <returns>匹配的 Formatter 实例；未注册时返回 null。</returns>
        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterMap.TryGetValue(typeof(T), out object formatter) ? (IMessagePackFormatter<T>)formatter : null;
        }
    }
}
