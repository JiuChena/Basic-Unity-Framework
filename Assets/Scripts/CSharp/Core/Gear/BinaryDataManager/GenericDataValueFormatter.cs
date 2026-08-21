using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace Core.Gear
{
    /// <summary>
    /// GenericDataValue 的 MessagePack Formatter。
    /// </summary>
    internal sealed class GenericDataValueFormatter : IMessagePackFormatter<GenericDataValue>
    {
        public void Serialize(ref MessagePackWriter writer, GenericDataValue value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }

            writer.WriteArrayHeader(7);
            writer.Write((int)value.type);
            writer.Write(value.intValue);
            writer.Write(value.floatValue);
            writer.Write(value.boolValue);
            writer.Write(value.stringValue);
            writer.Write(value.longValue);
            writer.Write(value.doubleValue);
        }

        public GenericDataValue Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;

            int count = reader.ReadArrayHeader();
            GenericDataValue value = new GenericDataValue();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0: value.type = (GenericDataValue.ValueType)reader.ReadInt32(); break;
                    case 1: value.intValue = reader.ReadInt32(); break;
                    case 2: value.floatValue = reader.ReadSingle(); break;
                    case 3: value.boolValue = reader.ReadBoolean(); break;
                    case 4: value.stringValue = reader.ReadString(); break;
                    case 5: value.longValue = reader.ReadInt64(); break;
                    case 6: value.doubleValue = reader.ReadDouble(); break;
                    default: reader.Skip(); break;
                }
            }

            return value;
        }
    }
}
