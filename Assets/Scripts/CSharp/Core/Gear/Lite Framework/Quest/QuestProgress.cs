using System;
using System.Collections.Generic;
using MessagePack;

namespace Core.Gear
{
    /// <summary>
    /// 任务进度快照，记录当前阶段和条件进度。
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class QuestProgress
    {
        [Key(0)]
        public string questID;

        [Key(1)]
        public int currentStageIndex;

        [Key(2)]
        public Dictionary<string, int> conditionProgress = new Dictionary<string, int>();
    }
}
