using System.Collections.Generic;
using MessagePack;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 区域触发器，玩家进入时广播 AreaEntered 事件（用于任务"到达某地"类条件）。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AreaTrigger : MonoBehaviour
    {
        [SerializeField] private string areaID;
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private LayerMask playerLayer = ~0;

        private bool triggered;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && triggered) return;
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
            triggered = true;
            EventCenter.Instance.SetEventTrigger(EventNames.AreaEntered, areaID);
        }
    }
}
