using System.Collections.Generic;
using MessagePack;
using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// 通用异构数据容器，以 object 列表存储多种基本类型数据，用于兼容旧版存储格式。
    /// </summary>
    [MessagePackObject]
    public class GenericDataContainer
    {
        // 序列化用的数据值列表
        [Key(0)]
        public List<GenericDataValue> serializedData = new List<GenericDataValue>();

        // 运行时可读写的 object 数据列表
        public List<object> data = new List<object>();

        /// <summary>
        /// 从文件加载数据并还原为 object 列表。
        /// </summary>
        /// <param name="dataPath">数据子目录路径</param>
        /// <param name="fileName">文件名</param>
        public void LoadData(string dataPath, string fileName)
        {
            GenericDataContainer storage = BinaryDataManager.Instance.Load<GenericDataContainer>(dataPath, fileName);
            if (storage == null)
            {
                serializedData = new List<GenericDataValue>();
                data = new List<object>();
                return;
            }

            serializedData = storage.serializedData ?? new List<GenericDataValue>();
            data = new List<object>(serializedData.Count);
            for (int i = 0; i < serializedData.Count; i++) data.Add(serializedData[i].ToObject());
        }

        /// <summary>
        /// 将当前 object 列表转换为可序列化格式并保存到文件。
        /// </summary>
        /// <param name="dataPath">数据子目录路径</param>
        /// <param name="fileName">文件名</param>
        public void SaveData(string dataPath, string fileName)
        {
            serializedData.Clear();
            for (int i = 0; i < data.Count; i++) serializedData.Add(GenericDataValue.FromObject(data[i]));

            BinaryDataManager.Instance.Save(dataPath, fileName, this);
        }

        /// <summary>
        /// 清空并重新填充数据。
        /// </summary>
        public void PushData(params object[] items)
        {
            data.Clear();
            if (items == null) return;

            for (int i = 0; i < items.Length; i++) data.Add(items[i]);
        }

        /// <summary>
        /// 获取指定索引的数据，超出范围返回 default。
        /// </summary>
        public object GetDataAt(int index)
        {
            return data.Count > index ? data[index] : default;
        }
    }

    /// <summary>
    /// 通用数据值的可序列化表示，支持 Int/Float/Bool/String/Long/Double 六种基本类型。
    /// </summary>
    [MessagePackObject]
    public class GenericDataValue
    {
        // 值类型枚举
        public enum ValueType
        {
            Null = 0,
            Int = 1,
            Float = 2,
            Bool = 3,
            String = 4,
            Long = 5,
            Double = 6,
        }

        // 当前存储的值类型
        [Key(0)]
        public ValueType type;

        // int 值
        [Key(1)]
        public int intValue;

        // float 值
        [Key(2)]
        public float floatValue;

        // bool 值
        [Key(3)]
        public bool boolValue;

        // string 值
        [Key(4)]
        public string stringValue;

        // long 值
        [Key(5)]
        public long longValue;

        // double 值
        [Key(6)]
        public double doubleValue;

        /// <summary>
        /// 从 object 创建 GenericDataValue，不支持的類型退化为字符串存储。
        /// </summary>
        public static GenericDataValue FromObject(object value)
        {
            if (value == null) return new GenericDataValue { type = ValueType.Null };
            if (value is int intValue) return new GenericDataValue { type = ValueType.Int, intValue = intValue };
            if (value is float floatValue) return new GenericDataValue { type = ValueType.Float, floatValue = floatValue };
            if (value is bool boolValue) return new GenericDataValue { type = ValueType.Bool, boolValue = boolValue };
            if (value is string stringValue) return new GenericDataValue { type = ValueType.String, stringValue = stringValue };
            if (value is long longValue) return new GenericDataValue { type = ValueType.Long, longValue = longValue };
            if (value is double doubleValue) return new GenericDataValue { type = ValueType.Double, doubleValue = doubleValue };

            Debug.LogWarning($"GenericDataContainer 不支持持久化类型 {value.GetType().FullName}，将退化为字符串存储。");
            return new GenericDataValue { type = ValueType.String, stringValue = value.ToString() };
        }

        /// <summary>
        /// 将序列化值还原为原始类型的 object。
        /// </summary>
        public object ToObject()
        {
            switch (type)
            {
                case ValueType.Int: return intValue;
                case ValueType.Float: return floatValue;
                case ValueType.Bool: return boolValue;
                case ValueType.String: return stringValue;
                case ValueType.Long: return longValue;
                case ValueType.Double: return doubleValue;
                default: return null;
            }
        }
    }
}
