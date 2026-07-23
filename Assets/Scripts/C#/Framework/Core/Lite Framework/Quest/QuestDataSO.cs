using System;
using UnityEngine;

namespace CoreFramework
{
    /// <summary>
    /// 任务数据配置（ScriptableObject），定义任务的阶段、条件、奖励和对话。
    /// </summary>
    [CreateAssetMenu(menuName = "Framework/CoreFramework/Quest/Quest Data")]
    public class QuestDataSO : ScriptableObject
    {
        public string questID;
        public string displayName;
        public QuestType questType;

        [TextArea]
        public string description;

        public string[] prerequisiteQuestIDs = Array.Empty<string>();
        public QuestStage[] stages = Array.Empty<QuestStage>();
        public QuestReward reward = new QuestReward();
        public DialogueDataSO acceptDialogue;
        public DialogueDataSO completeDialogue;
    }
}
