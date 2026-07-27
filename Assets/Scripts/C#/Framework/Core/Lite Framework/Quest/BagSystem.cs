using System.Collections.Generic;
using MessagePack;
using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// 背包持久化数据，包含货币数量和堆叠道具字典（assetID → 数量）。
    /// </summary>
    [System.Serializable]
    [MessagePackObject]
    public class BagData
    {
        [Key(0)]
        public int currency;

        [Key(1)]
        public Dictionary<string, int> stackableItems = new Dictionary<string, int>();

        public bool HasItem(string assetID, int amount = 1)
        {
            return stackableItems.TryGetValue(assetID, out int count) && count >= amount;
        }

        public void AddItem(string assetID, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(assetID) || amount <= 0) return;
            stackableItems.TryGetValue(assetID, out int currentCount);
            stackableItems[assetID] = currentCount + amount;
        }

        public bool RemoveItem(string assetID, int amount = 1)
        {
            if (!HasItem(assetID, amount)) return false;
            stackableItems[assetID] -= amount;
            if (stackableItems[assetID] <= 0) stackableItems.Remove(assetID);
            return true;
        }
    }

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

    /// <summary>
    /// 区域触发器，玩家进入时广播 AreaEntered 事件（用于任务"到达某地"类条件）。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AreaTrigger : MonoBehaviour
    {
        [SerializeField] private string areaID;
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private LayerMask playerLayer = ~0;

        private bool triggered;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && triggered) return;
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
            triggered = true;
            EventCenter.Instance.SetEventTrigger(EventNames.AreaEntered, areaID);
        }
    }
}
