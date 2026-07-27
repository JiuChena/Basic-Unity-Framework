using System;
using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Unity;
using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// MessagePack 运行时封装，统一项目序列化选项并安全处理反序列化异常。
    /// </summary>
    internal sealed class MessagePackRuntime
    {
        // 项目统一序列化选项：自定义 Resolver + Unity Resolver
        private readonly MessagePackSerializerOptions _options;

        public MessagePackRuntime()
        {
            _options = MessagePackSerializerOptions.Standard.WithResolver(
                CompositeResolver.Create(ProjectSaveResolver.Instance, UnityResolver.InstanceWithStandardResolver));
        }

        /// <summary>
        /// 序列化对象为 MessagePack 字节数组。
        /// </summary>
        public byte[] Serialize<T>(T data)
        {
            return MessagePackSerializer.Serialize(data, _options);
        }

        /// <summary>
        /// 反序列化字节数组。格式不兼容或旧版 typeless 数据时返回 default。
        /// </summary>
        public T Deserialize<T>(byte[] bytes)
        {
            try
            {
                return MessagePackSerializer.Deserialize<T>(bytes, _options);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"MessagePack 反序列化失败，将使用默认数据。"
                    + $"\n  目标类型: {typeof(T).FullName}"
                    + $"\n  内部异常: {ex.InnerException?.Message ?? ex.Message}");
                return default;
            }
        }
    }
}
