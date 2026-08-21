using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace Core.Gear
{
    /// <summary>
    /// QuestSaveData 的 MessagePack Formatter。
    /// </summary>
    internal sealed class QuestSaveDataFormatter : IMessagePackFormatter<QuestSaveData>
    {
        public void Serialize(ref MessagePackWriter writer, QuestSaveData value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }

            writer.WriteArrayHeader(3);
            options.Resolver.GetFormatterWithVerify<List<string>>()
                .Serialize(ref writer, value.completedQuestIDs, options);
            options.Resolver.GetFormatterWithVerify<Dictionary<string, QuestProgress>>()
                .Serialize(ref writer, value.activeQuests, options);
            options.Resolver.GetFormatterWithVerify<Dictionary<string, long>>()
                .Serialize(ref writer, value.dailyLastClaimTime, options);
        }

        public QuestSaveData Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;

            int count = reader.ReadArrayHeader();
            QuestSaveData value = new QuestSaveData();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0:
                        value.completedQuestIDs = options.Resolver.GetFormatterWithVerify<List<string>>()
                            .Deserialize(ref reader, options);
                        break;
                    case 1:
                        value.activeQuests = options.Resolver.GetFormatterWithVerify<Dictionary<string, QuestProgress>>()
                            .Deserialize(ref reader, options);
                        break;
                    case 2:
                        value.dailyLastClaimTime = options.Resolver.GetFormatterWithVerify<Dictionary<string, long>>()
                            .Deserialize(ref reader, options);
                        break;
                    default: reader.Skip(); break;
                }
            }

            value.completedQuestIDs ??= new List<string>();
            value.activeQuests ??= new Dictionary<string, QuestProgress>();
            value.dailyLastClaimTime ??= new Dictionary<string, long>();
            return value;
        }
    }
}
