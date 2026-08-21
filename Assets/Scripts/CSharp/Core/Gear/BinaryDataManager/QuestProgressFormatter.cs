using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace Core.Gear
{
    /// <summary>
    /// QuestProgress 的 MessagePack Formatter。
    /// </summary>
    internal sealed class QuestProgressFormatter : IMessagePackFormatter<QuestProgress>
    {
        public void Serialize(ref MessagePackWriter writer, QuestProgress value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }

            writer.WriteArrayHeader(3);
            writer.Write(value.questID);
            writer.Write(value.currentStageIndex);
            options.Resolver.GetFormatterWithVerify<Dictionary<string, int>>()
                .Serialize(ref writer, value.conditionProgress, options);
        }

        public QuestProgress Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;

            int count = reader.ReadArrayHeader();
            QuestProgress value = new QuestProgress();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0: value.questID = reader.ReadString(); break;
                    case 1: value.currentStageIndex = reader.ReadInt32(); break;
                    case 2:
                        value.conditionProgress = options.Resolver.GetFormatterWithVerify<Dictionary<string, int>>()
                            .Deserialize(ref reader, options);
                        break;
                    default: reader.Skip(); break;
                }
            }

            value.conditionProgress ??= new Dictionary<string, int>();
            return value;
        }
    }
}
