using System.Collections.Generic;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 任务系统入口 MonoBehaviour，初始化 QuestConditionTracker 并预加载任务配置。
    /// </summary>
    public class TaskSystem : MonoBehaviour
    {
        private static TaskSystem instance;

        public static TaskSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject(nameof(TaskSystem));
                    instance = go.AddComponent<TaskSystem>();
                }

                return instance;
            }
        }

        [SerializeField, Tooltip("任务系统启动时预注册到 QuestConditionTracker 的任务配置列表。")]
        private List<QuestDataSO> preloadQuestDatas = new List<QuestDataSO>();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            QuestConditionTracker.Instance.StartTracking();

            for (int i = 0; i < preloadQuestDatas.Count; i++)
            {
                if (preloadQuestDatas[i] != null)
                    QuestConditionTracker.Instance.RegisterQuestData(preloadQuestDatas[i]);
            }
        }
    }
}
