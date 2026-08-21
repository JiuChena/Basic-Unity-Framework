using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace Core.Gear
{
    /// <summary>
    /// GenericDataContainer 的 MessagePack Formatter。
    /// </summary>
    internal sealed class GenericDataContainerFormatter : IMessagePackFormatter<GenericDataContainer>
    {
        public void Serialize(ref MessagePackWriter writer, GenericDataContainer value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }

            writer.WriteArrayHeader(1);
            options.Resolver.GetFormatterWithVerify<List<GenericDataValue>>()
                .Serialize(ref writer, value.serializedData, options);
        }

        public GenericDataContainer Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;

            int count = reader.ReadArrayHeader();
            GenericDataContainer value = new GenericDataContainer();

            for (int i = 0; i < count; i++)
            {
                if (i == 0)
                {
                    value.serializedData = options.Resolver.GetFormatterWithVerify<List<GenericDataValue>>()
                        .Deserialize(ref reader, options);
                }
                else reader.Skip();
            }

            // 还原 data 列表
            value.serializedData ??= new List<GenericDataValue>();
            value.data = new List<object>(value.serializedData.Count);
            for (int i = 0; i < value.serializedData.Count; i++) value.data.Add(value.serializedData[i].ToObject());
            return value;
        }
    }
}
