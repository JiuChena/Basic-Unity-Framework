using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoreFramework
{
    /// <summary>
    /// 任务管理器，负责任务的接取、阶段推进和完成。首次访问时懒加载 QuestSaveData。
    /// </summary>
    public class QuestManager
    {
        private static QuestManager _instance;

        public static QuestManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new QuestManager();
                    _instance.LoadData();
                }

                return _instance;
            }
        }

        private QuestSaveData _saveData;
        private bool _dataLoaded;

        private void LoadData()
        {
            _saveData = BinaryDataManager.Instance.Load<QuestSaveData>("Quest/", "QuestSaveData") ?? new QuestSaveData();
            _dataLoaded = true;
        }

        private void SaveData()
        {
            if (!_dataLoaded)
                return;

            BinaryDataManager.Instance.Save("Quest/", "QuestSaveData", _saveData);
        }

        /// <summary>
        /// 任务是否已完成。
        /// </summary>
        public bool IsCompleted(string questID) => _saveData.completedQuestIDs.Contains(questID);

        /// <summary>
        /// 任务是否进行中。
        /// </summary>
        public bool IsActive(string questID) => _saveData.activeQuests.ContainsKey(questID);

        /// <summary>
        /// 检查任务是否可接取（未完成、未激活、前置任务已完成、日常任务当天未完成过）。
        /// </summary>
        public bool CanAccept(QuestDataSO quest)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.questID))
                return false;
            if (IsCompleted(quest.questID) || IsActive(quest.questID))
                return false;

            if (quest.prerequisiteQuestIDs != null)
            {
                for (int i = 0; i < quest.prerequisiteQuestIDs.Length; i++)
                {
                    if (!IsCompleted(quest.prerequisiteQuestIDs[i]))
                        return false;
                }
            }

            return quest.questType != QuestType.Daily || IsDailyAvailable(quest.questID);
        }

        /// <summary>
        /// 接取任务，记录进度并保存。
        /// </summary>
        public void AcceptQuest(QuestDataSO quest)
        {
            if (!CanAccept(quest))
                return;

            _saveData.activeQuests[quest.questID] = new QuestProgress
            {
                questID = quest.questID,
                currentStageIndex = 0,
            };
            SaveData();
            EventCenter.Instance.SetEventTrigger(EventNames.QuestAccepted, quest.questID);
        }

        /// <summary>
        /// 检查当前阶段是否所有条件已满足，自动推进或完成。
        /// </summary>
        public void CheckStageCompletion(string questID, QuestDataSO questData)
        {
            if (questData == null || !_saveData.activeQuests.TryGetValue(questID, out QuestProgress progress))
                return;

            if (questData.stages == null || questData.stages.Length == 0)
            {
                CompleteQuest(questID, questData);
                return;
            }

            if (progress.currentStageIndex < 0 || progress.currentStageIndex >= questData.stages.Length)
                return;

            QuestStage stage = questData.stages[progress.currentStageIndex];
            QuestCondition[] conditions = stage?.conditions ?? Array.Empty<QuestCondition>();

            for (int i = 0; i < conditions.Length; i++)
            {
                string key = ConditionKey(questID, progress.currentStageIndex, i);
                progress.conditionProgress.TryGetValue(key, out int currentCount);
                if (currentCount < Mathf.Max(conditions[i].requiredCount, 1))
                    return;
            }

            progress.currentStageIndex++;
            SaveData();

            if (progress.currentStageIndex >= questData.stages.Length)
                CompleteQuest(questID, questData);
            else
                EventCenter.Instance.SetEventTrigger(EventNames.QuestStageAdvanced, questID);
        }

        /// <summary>
        /// 获取所有进行中任务的进度快照。
        /// </summary>
        public List<QuestProgress> GetActiveProgressesSnapshot()
        {
            return new List<QuestProgress>(_saveData.activeQuests.Values);
        }

        /// <summary>
        /// 获取指定任务的进度。
        /// </summary>
        public QuestProgress GetProgress(string questID)
        {
            _saveData.activeQuests.TryGetValue(questID, out QuestProgress progress);
            return progress;
        }

        /// <summary>
        /// 生成条件进度的唯一 Key。
        /// </summary>
        public static string ConditionKey(string questID, int stageIdx, int conditionIdx)
        {
            return $"{questID}_{stageIdx}_{conditionIdx}";
        }

        private void CompleteQuest(string questID, QuestDataSO questData)
        {
            _saveData.activeQuests.Remove(questID);
            if (!_saveData.completedQuestIDs.Contains(questID))
                _saveData.completedQuestIDs.Add(questID);

            if (questData != null && questData.questType == QuestType.Daily)
                _saveData.dailyLastClaimTime[questID] = DateTime.UtcNow.Ticks;

            if (questData != null)
                GiveReward(questData.reward);
            SaveData();
            EventCenter.Instance.SetEventTrigger(EventNames.QuestCompleted, questID);
        }

        private void GiveReward(QuestReward reward)
        {
            if (reward == null)
                return;

            if (reward.currency != 0)
                BagSystem.Instance.AddCurrency(reward.currency);

            ItemReward[] items = reward.items ?? Array.Empty<ItemReward>();
            for (int i = 0; i < items.Length; i++)
            {
                ItemReward itemReward = items[i];
                if (itemReward?.item == null || string.IsNullOrWhiteSpace(itemReward.item.assetID))
                    continue;

                BagSystem.Instance.AddItem(itemReward.item.assetID, Mathf.Max(itemReward.count, 1));
            }
        }

        private bool IsDailyAvailable(string questID)
        {
            if (!_saveData.dailyLastClaimTime.TryGetValue(questID, out long ticks))
                return true;

            return DateTime.UtcNow.Date > new DateTime(ticks).Date;
        }
    }
}
