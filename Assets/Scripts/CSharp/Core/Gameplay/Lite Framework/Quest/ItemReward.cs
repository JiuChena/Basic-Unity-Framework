using System;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 单个道具奖励项。
    /// </summary>
    [Serializable]
    public class ItemReward
    {
        public ItemInfo item;
        public int count = 1;
    }
}
