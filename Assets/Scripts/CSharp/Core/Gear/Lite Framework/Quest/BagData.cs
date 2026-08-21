using System.Collections.Generic;
using MessagePack;
using UnityEngine;

namespace Core.Gear
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
}
