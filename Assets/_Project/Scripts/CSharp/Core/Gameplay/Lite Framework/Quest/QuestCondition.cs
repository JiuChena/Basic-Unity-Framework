using System;

namespace Core.Gear
{
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
}
