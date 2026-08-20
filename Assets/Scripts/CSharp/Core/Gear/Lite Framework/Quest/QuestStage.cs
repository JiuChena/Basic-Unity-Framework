using System;

namespace Core.Gear
{
    /// <summary>
    /// 任务阶段，包含描述和多个条件（全部满足才推进）。
    /// </summary>
    [Serializable]
    public class QuestStage
    {
        public string stageID;
        public string description;
        public QuestCondition[] conditions = Array.Empty<QuestCondition>();
    }

    /// <summary>
    /// 任务条件，定义类型、目标ID 和需求数量。
    /// </summary>
    [Serializable]
    public class QuestCondition
    {
        public QuestConditionType type;
        public string targetID;
        public int requiredCount = 1;
        public string displayText;
    }

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
