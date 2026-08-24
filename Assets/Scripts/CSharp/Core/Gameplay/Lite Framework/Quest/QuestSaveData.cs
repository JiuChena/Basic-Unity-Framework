using System;
using System.Collections.Generic;
using MessagePack;

namespace Core.Gear
{
    /// <summary>
    /// 任务持久化数据，包含已完成列表、进行中任务进度和日常刷新时间。
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class QuestSaveData
    {
        [Key(0)]
        public List<string> completedQuestIDs = new List<string>();

        [Key(1)]
        public Dictionary<string, QuestProgress> activeQuests = new Dictionary<string, QuestProgress>();

        [Key(2)]
        public Dictionary<string, long> dailyLastClaimTime = new Dictionary<string, long>();
    }
}
