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
}
