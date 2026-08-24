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
        /// <summary>
        /// 将 QuestSaveData 序列化为 MessagePack 数组。
        /// </summary>
        /// <param name="writer">MessagePack 写入器。</param>
        /// <param name="value">要序列化的 QuestSaveData；为 null 时写入 Nil。</param>
        /// <param name="options">序列化选项。</param>
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

        /// <summary>
        /// 从 MessagePack 数组反序列化 QuestSaveData。
        /// </summary>
        /// <param name="reader">MessagePack 读取器。</param>
        /// <param name="options">反序列化选项。</param>
        /// <returns>还原出的 QuestSaveData；读取到 Nil 时返回 null。</returns>
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
