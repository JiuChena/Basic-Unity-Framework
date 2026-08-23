using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace Core.Gear
{
    /// <summary>
    /// AudioData 的 MessagePack Formatter。
    /// </summary>
    internal sealed class AudioDataFormatter : IMessagePackFormatter<global::AudioData>
    {
        /// <summary>
        /// 将 AudioData 序列化为 MessagePack 数组。
        /// </summary>
        /// <param name="writer">MessagePack 写入器。</param>
        /// <param name="value">要序列化的 AudioData；为 null 时写入 Nil。</param>
        /// <param name="options">序列化选项。</param>
        public void Serialize(ref MessagePackWriter writer, global::AudioData value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }

            writer.WriteArrayHeader(4);
            writer.Write(value.musicEnabled);
            writer.Write(value.musicVolume);
            writer.Write(value.soundEnabled);
            writer.Write(value.soundVolume);
        }

        /// <summary>
        /// 从 MessagePack 数组反序列化 AudioData。
        /// </summary>
        /// <param name="reader">MessagePack 读取器。</param>
        /// <param name="options">反序列化选项。</param>
        /// <returns>还原出的 AudioData；读取到 Nil 时返回 null。</returns>
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
}
