using System;

namespace Core.Gear
{
    /// <summary>
    /// 任务奖励，包含货币和道具列表。
    /// </summary>
    [Serializable]
    public class QuestReward
    {
        public int currency;
        public ItemReward[] items = Array.Empty<ItemReward>();
    }
}
