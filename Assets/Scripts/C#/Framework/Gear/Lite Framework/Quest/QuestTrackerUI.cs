using System;
using System.Text;
using TMPro;
using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// HUD 任务追踪条，显示当前追踪任务的阶段目标进度。
    /// </summary>
    public class QuestTrackerUI : MonoBehaviour
    {
        [SerializeField, Tooltip("显示任务标题的文本组件。")]
        private TMP_Text titleText;

        [SerializeField, Tooltip("显示阶段目标进度的文本组件。")]
        private TMP_Text objectiveText;

        private string trackedQuestID;

        private void OnEnable()
        {
            EventCenter.Instance.AddEventListener<string>(EventNames.QuestAccepted, OnQuestAccepted);
            EventCenter.Instance.AddEventListener<string>(EventNames.QuestStageAdvanced, Refresh);
            EventCenter.Instance.AddEventListener<string>(EventNames.QuestCompleted, OnQuestCompleted);
        }

        private void OnDisable()
        {
            EventCenter.Instance.RemoveEventListener<string>(EventNames.QuestAccepted, OnQuestAccepted);
            EventCenter.Instance.RemoveEventListener<string>(EventNames.QuestStageAdvanced, Refresh);
            EventCenter.Instance.RemoveEventListener<string>(EventNames.QuestCompleted, OnQuestCompleted);
        }

        private void OnQuestAccepted(string questID)
        {
            trackedQuestID = questID;
            Refresh(questID);
        }

        private void Refresh(string questID)
        {
            if (questID != trackedQuestID)
                return;

            QuestProgress progress = QuestManager.Instance.GetProgress(questID);
            QuestDataSO data = QuestConditionTracker.Instance.GetQuestData(questID);
            if (progress == null || data == null || data.stages == null || progress.currentStageIndex >= data.stages.Length)
                return;

            QuestStage stage = data.stages[progress.currentStageIndex];
            if (titleText != null)
                titleText.text = data.displayName;
            if (objectiveText != null)
                objectiveText.text = BuildObjectiveText(progress, stage);

            gameObject.SetActive(true);
        }

        private string BuildObjectiveText(QuestProgress progress, QuestStage stage)
        {
            StringBuilder sb = new StringBuilder();
            QuestCondition[] conditions = stage?.conditions ?? Array.Empty<QuestCondition>();

            for (int i = 0; i < conditions.Length; i++)
            {
                QuestCondition condition = conditions[i];
                string key = QuestManager.ConditionKey(progress.questID, progress.currentStageIndex, i);
                progress.conditionProgress.TryGetValue(key, out int currentCount);

                string template = string.IsNullOrWhiteSpace(condition.displayText)
                    ? $"{condition.type} {condition.targetID}: {{0}}/{{1}}"
                    : condition.displayText;

                sb.AppendLine(string.Format(template, currentCount, Mathf.Max(condition.requiredCount, 1)));
            }

            return sb.ToString().TrimEnd();
        }

        private void OnQuestCompleted(string questID)
        {
            if (questID != trackedQuestID)
                return;

            trackedQuestID = null;
            gameObject.SetActive(false);
        }
    }
}
