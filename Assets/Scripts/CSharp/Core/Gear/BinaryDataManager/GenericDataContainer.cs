using System.Collections.Generic;
using MessagePack;
using UnityEngine;

namespace Core.Gear
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
}
