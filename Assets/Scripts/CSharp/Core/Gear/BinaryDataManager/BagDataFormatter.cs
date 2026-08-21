using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace Core.Gear
{
    /// <summary>
    /// BagData 的 MessagePack Formatter。
    /// </summary>
    internal sealed class BagDataFormatter : IMessagePackFormatter<BagData>
    {
        public void Serialize(ref MessagePackWriter writer, BagData value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }

            writer.WriteArrayHeader(2);
            writer.Write(value.currency);
            options.Resolver.GetFormatterWithVerify<Dictionary<string, int>>()
                .Serialize(ref writer, value.stackableItems, options);
        }

        public BagData Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;

            int count = reader.ReadArrayHeader();
            BagData value = new BagData();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0:
                        value.currency = reader.ReadInt32();
                        break;
                    case 1:
                        value.stackableItems = options.Resolver.GetFormatterWithVerify<Dictionary<string, int>>()
                            .Deserialize(ref reader, options);
                        break;
                    default: reader.Skip(); break;
                }
            }

            value.stackableItems ??= new Dictionary<string, int>();
            return value;
        }
    }
}
