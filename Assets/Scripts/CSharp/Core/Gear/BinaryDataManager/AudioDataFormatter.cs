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
}
