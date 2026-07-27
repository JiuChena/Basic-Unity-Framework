using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace Framework.Core
{
    /// <summary>
    /// AudioData 的 MessagePack Formatter。
    /// </summary>
    internal sealed class AudioDataFormatter : IMessagePackFormatter<global::AudioData>
    {
        public void Serialize(ref MessagePackWriter writer, global::AudioData value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }

            writer.WriteArrayHeader(4);
            writer.Write(value.musicEnabled);
            writer.Write(value.musicVolume);
            writer.Write(value.soundEnabled);
            writer.Write(value.soundVolume);
        }

        public global::AudioData Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;

            int count = reader.ReadArrayHeader();
            global::AudioData value = new global::AudioData();

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0: value.musicEnabled = reader.ReadBoolean(); break;
                    case 1: value.musicVolume = reader.ReadSingle(); break;
                    case 2: value.soundEnabled = reader.ReadBoolean(); break;
                    case 3: value.soundVolume = reader.ReadSingle(); break;
                    default: reader.Skip(); break;
                }
            }

            return value;
        }
    }

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
