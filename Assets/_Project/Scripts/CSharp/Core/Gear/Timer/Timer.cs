using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 定时器事件管理器，基于管理器时间与事件记录时间差来触发回调。
    /// </summary>
    public class Timer : MonoBehaviour
    {
        private const double TimeRebaseThreshold = 1_000_000d;
        private const int RebaseBatchSize = 256;

        private static Timer instance;
        private readonly List<TimerEvent> actions = new List<TimerEvent>();
        private double currentTime;
        private bool isRebasing;
        private Coroutine rebaseCoroutine;

        public static Timer Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject obj = new GameObject();
                    instance = obj.AddComponent<Timer>();
                    obj.name = "TimerEventManager";
                    DontDestroyOnLoad(obj);
                }

                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 添加一个定时事件，默认只触发一次。
        /// </summary>
        /// <param name="interval">触发间隔（秒）</param>
        /// <param name="action">触发时执行的回调</param>
        public void AddTimerEvent(float interval, UnityAction action)
        {
            AddTimerEvent(interval, 1, action);
        }

        /// <summary>
        /// 添加一个定时事件。
        /// </summary>
        /// <param name="interval">触发间隔（秒），必须大于 0</param>
        /// <param name="triggerCount">触发次数。1 代表一次，8 代表八次，-1 代表无限次。</param>
        /// <param name="action">触发时执行的回调</param>
        public void AddTimerEvent(float interval, int triggerCount, UnityAction action)
        {
            if (interval <= 0f)
            {
                Debug.LogError("TimerEvent interval must be greater than 0.");
                return;
            }

            if (triggerCount == 0 || triggerCount < -1)
            {
                Debug.LogError("TimerEvent triggerCount must be -1 or greater than 0.");
                return;
            }

            if (action == null)
            {
                Debug.LogError("TimerEvent action can not be null.");
                return;
            }

            actions.Add(new TimerEvent(interval, triggerCount, action, currentTime));
        }

        private void Update()
        {
            currentTime += Time.deltaTime;

            if (isRebasing)
            {
                return;
            }

            if (currentTime >= TimeRebaseThreshold)
            {
                StartRebase();
                return;
            }

            for (int i = actions.Count - 1; i >= 0; i--)
            {
                TimerEvent timerEvent = actions[i];

                while (currentTime - timerEvent.RecordTime >= timerEvent.Interval)
                {
                    timerEvent.RecordTime += timerEvent.Interval;
                    timerEvent.Action.Invoke();

                    if (timerEvent.TriggerCount > 0)
                    {
                        timerEvent.ExecutedCount++;
                        if (timerEvent.ExecutedCount >= timerEvent.TriggerCount)
                        {
                            actions.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        private void StartRebase()
        {
            if (isRebasing)
            {
                return;
            }

            isRebasing = true;
            double rebaseAmount = currentTime;
            currentTime = 0d;

            if (rebaseCoroutine != null)
            {
                StopCoroutine(rebaseCoroutine);
            }

            rebaseCoroutine = StartCoroutine(RebaseTimeAsync(rebaseAmount, actions.Count));
        }

        private System.Collections.IEnumerator RebaseTimeAsync(double rebaseAmount, int eventCountSnapshot)
        {
            int processedCount = 0;
            int safeCount = Mathf.Min(eventCountSnapshot, actions.Count);

            for (int i = 0; i < safeCount; i++)
            {
                actions[i].RecordTime -= rebaseAmount;
                processedCount++;

                if (processedCount >= RebaseBatchSize)
                {
                    processedCount = 0;
                    yield return null;
                }
            }

            rebaseCoroutine = null;
            isRebasing = false;
        }
    }
}
