using System.Collections.Generic;
using MessagePack;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 背包系统，管理货币和道具的增删查。首次访问时从磁盘懒加载 BagData，
    /// 每次修改后立即保存并广播 BagUpdated 事件。
    /// </summary>
    public class BagSystem
    {
        private static BagSystem _instance;
        public static BagSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new BagSystem();
                    _instance.LoadData();
                }
                return _instance;
            }
        }

        private BagData _bagData;
        private bool _dataLoaded;

        private void LoadData()
        {
            _bagData = BinaryDataManager.Instance.Load<BagData>("Bag/", "BagData") ?? new BagData();
            _dataLoaded = true;
        }

        private void SaveData()
        {
            if (!_dataLoaded) return;
            BinaryDataManager.Instance.Save("Bag/", "BagData", _bagData);
        }

        /// <summary>
        /// 当前货币数量。
        /// </summary>
        public int Currency => _bagData.currency;

        /// <summary>
        /// 是否有足够货币。
        /// </summary>
        public bool HasEnoughCurrency(int amount) => _bagData.currency >= amount;

        /// <summary>
        /// 增加货币并立即保存。
        /// </summary>
        public void AddCurrency(int amount)
        {
            _bagData.currency += amount;
            SaveData();
            NotifyBagChanged();
        }

        /// <summary>
        /// 消费货币并立即保存，余额不足返回 false。
        /// </summary>
        public bool SpendCurrency(int amount)
        {
            if (!HasEnoughCurrency(amount)) return false;
            _bagData.currency -= amount;
            SaveData();
            NotifyBagChanged();
            return true;
        }

        /// <summary>
        /// 向背包添加道具并立即保存。
        /// </summary>
        public void AddItem(string assetID, int amount = 1)
        {
            _bagData.AddItem(assetID, amount);
            SaveData();
            NotifyBagChanged();
        }

        /// <summary>
        /// 从背包移除道具并立即保存，数量不足返回 false。
        /// </summary>
        public bool RemoveItem(string assetID, int amount = 1)
        {
            if (!_bagData.RemoveItem(assetID, amount)) return false;
            SaveData();
            NotifyBagChanged();
            return true;
        }

        /// <summary>
        /// 查询指定道具的持有数量。
        /// </summary>
        public int GetItemCount(string assetID)
        {
            _bagData.stackableItems.TryGetValue(assetID, out int count);
            return count;
        }

        /// <summary>
        /// 是否持有指定数量的道具。
        /// </summary>
        public bool HasItem(string assetID, int amount = 1) => _bagData.HasItem(assetID, amount);

        private void NotifyBagChanged()
        {
            EventCenter.Instance.SetEventTrigger(EventNames.BagUpdated);
        }
    }
}
