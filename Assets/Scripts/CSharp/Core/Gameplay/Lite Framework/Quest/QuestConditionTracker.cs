using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 任务条件追踪器，订阅游戏事件（击杀/收集/到达/交互）自动推进所有进行中任务的条件进度。
    /// </summary>
    public class QuestConditionTracker
    {
        private static readonly QuestConditionTracker _instance = new QuestConditionTracker();
        public static QuestConditionTracker Instance => _instance;

        private readonly Dictionary<string, QuestDataSO> questDataCache = new Dictionary<string, QuestDataSO>();
        private bool isTracking;

        /// <summary>
        /// 注册一个任务配置供条件追踪使用。
        /// </summary>
        public void RegisterQuestData(QuestDataSO data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.questID))
                return;

            questDataCache[data.questID] = data;
        }

        /// <summary>
        /// 获取已注册的任务配置。
        /// </summary>
        public QuestDataSO GetQuestData(string questID)
        {
            questDataCache.TryGetValue(questID, out QuestDataSO data);
            return data;
        }

        /// <summary>
        /// 开始监听游戏事件。重复调用安全。
        /// </summary>
        public void StartTracking()
        {
            if (isTracking)
                return;

            isTracking = true;
            EventCenter.Instance.Register<GameObject>(EventNames.UnitDeath, OnUnitDeath);
            EventCenter.Instance.Register(EventNames.BagUpdated, OnBagUpdated);
            EventCenter.Instance.Register<string>(EventNames.AreaEntered, OnAreaEntered);
            EventCenter.Instance.Register<string>(EventNames.InteractionPerformed, OnInteractionPerformed);
        }

        private void OnUnitDeath(GameObject unit)
        {
            if (unit == null)
                return;

            IQuestTargetProvider provider = unit.GetComponent<IQuestTargetProvider>();
            string assetID = provider != null ? provider.QuestTargetID : null;
            if (string.IsNullOrWhiteSpace(assetID))
                assetID = unit.name;

            UpdateConditions(QuestConditionType.Kill, assetID, 1);
        }

        private void OnBagUpdated()
        {
            List<QuestProgress> activeProgresses = QuestManager.Instance.GetActiveProgressesSnapshot();
            for (int i = 0; i < activeProgresses.Count; i++)
            {
                QuestProgress progress = activeProgresses[i];
                if (progress == null || !questDataCache.TryGetValue(progress.questID, out QuestDataSO questData))
                    continue;
                if (progress.currentStageIndex < 0 || progress.currentStageIndex >= questData.stages.Length)
                    continue;

                QuestStage stage = questData.stages[progress.currentStageIndex];
                QuestCondition[] conditions = stage?.conditions ?? Array.Empty<QuestCondition>();

                for (int j = 0; j < conditions.Length; j++)
                {
                    QuestCondition condition = conditions[j];
                    if (condition == null || condition.type != QuestConditionType.Collect)
                        continue;

                    int bagCount = BagSystem.Instance.GetItemCount(condition.targetID);
                    string key = QuestManager.ConditionKey(progress.questID, progress.currentStageIndex, j);
                    progress.conditionProgress[key] = Mathf.Min(bagCount, Mathf.Max(condition.requiredCount, 1));
                }

                QuestManager.Instance.CheckStageCompletion(progress.questID, questData);
            }
        }

        private void OnAreaEntered(string areaID)
        {
            UpdateConditions(QuestConditionType.Reach, areaID, 1);
        }

        private void OnInteractionPerformed(string interactionID)
        {
            UpdateConditions(QuestConditionType.Interact, interactionID, 1);
        }

        private void UpdateConditions(QuestConditionType type, string targetID, int addCount)
        {
            if (string.IsNullOrWhiteSpace(targetID))
                return;

            List<QuestProgress> activeProgresses = QuestManager.Instance.GetActiveProgressesSnapshot();
            for (int i = 0; i < activeProgresses.Count; i++)
            {
                QuestProgress progress = activeProgresses[i];
                if (progress == null || !questDataCache.TryGetValue(progress.questID, out QuestDataSO questData))
                    continue;
                if (progress.currentStageIndex < 0 || progress.currentStageIndex >= questData.stages.Length)
                    continue;

                QuestStage stage = questData.stages[progress.currentStageIndex];
                QuestCondition[] conditions = stage?.conditions ?? Array.Empty<QuestCondition>();

                for (int j = 0; j < conditions.Length; j++)
                {
                    QuestCondition condition = conditions[j];
                    if (condition == null || condition.type != type || condition.targetID != targetID)
                        continue;

                    string key = QuestManager.ConditionKey(progress.questID, progress.currentStageIndex, j);
                    progress.conditionProgress.TryGetValue(key, out int currentCount);
                    progress.conditionProgress[key] = Mathf.Min(currentCount + addCount, Mathf.Max(condition.requiredCount, 1));
                }

                QuestManager.Instance.CheckStageCompletion(progress.questID, questData);
            }
        }
    }
}
