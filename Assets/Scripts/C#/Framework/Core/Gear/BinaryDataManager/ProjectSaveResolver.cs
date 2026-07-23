using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace CoreFramework
{
    /// <summary>
    /// 项目级 Formatter 解析器，集中注册所有自定义 MessagePack Formatter。
    /// </summary>
    public sealed class ProjectSaveResolver : IFormatterResolver
    {
        public static readonly IFormatterResolver Instance = new ProjectSaveResolver();

        // 类型 → Formatter 实例映射表
        private static readonly Dictionary<Type, object> FormatterMap = new Dictionary<Type, object>();

        static ProjectSaveResolver()
        {
            // 按字母顺序注册所有自定义 Formatter
            Register(new AudioDataFormatter());
            Register(new BagDataFormatter());
            Register(new GenericDataContainerFormatter());
            Register(new GenericDataValueFormatter());
            Register(new QuestProgressFormatter());
            Register(new QuestSaveDataFormatter());
        }

        private ProjectSaveResolver() { }

        /// <summary>
        /// 注册自定义 Formatter。
        /// </summary>
        public static void Register<T>(IMessagePackFormatter<T> formatter)
        {
            FormatterMap[typeof(T)] = formatter;
        }

        /// <summary>
        /// 按类型查找 Formatter，未注册时返回 null。
        /// </summary>
        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterMap.TryGetValue(typeof(T), out object formatter) ? (IMessagePackFormatter<T>)formatter : null;
        }
    }
}
