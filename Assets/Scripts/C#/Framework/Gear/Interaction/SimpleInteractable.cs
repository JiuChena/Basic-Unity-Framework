using UnityEngine;
using UnityEngine.Events;

namespace Framework.Core
{
    /// <summary>
    /// 通用可交互组件，直接在 Inspector 配置交互参数和回调。
    /// 简单交互（开门、拾取、对话入口）只需挂这个组件 + InteractionEmitter 即可。
    /// 复杂交互可手写自己的 MonoBehaviour 实现 IInteractable 接口。
    /// </summary>
    public class SimpleInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("交互唯一标识，用于任务系统匹配")]
        private string interactionID;

        [SerializeField, Tooltip("交互提示文本")]
        private string promptText = "F - 交互";

        [SerializeField, Tooltip("交互图标")]
        private Sprite icon;

        [SerializeField, Tooltip("混合色")]
        private Color blendColor = Color.white;

        [SerializeField, Tooltip("优先级（数值大的排在前面）")]
        private int priority;

        [SerializeField, Tooltip("需要玩家面朝物体才能交互")]
        private bool requireFacing;

        [SerializeField, Tooltip("朝向点积阈值（0=完全不限制，1=完全正对）")]
        private float facingDotThreshold = 0.5f;

        [SerializeField, Tooltip("交互事件")]
        private UnityEvent<GameObject> onInteract;

        [SerializeField, Tooltip("进入范围事件")]
        private UnityEvent<GameObject> onEnterRange;

        [SerializeField, Tooltip("离开范围事件")]
        private UnityEvent<GameObject> onExitRange;

        public string InteractionID => interactionID;
        public string PromptText => promptText;
        public Sprite Icon => icon;
        public Color BlendColor => blendColor;
        public int Priority => priority;

        public bool CanInteract(GameObject interactor)
        {
            if (!requireFacing) return true;
            Vector3 toTarget = (transform.position - interactor.transform.position).normalized;
            return Vector3.Dot(interactor.transform.forward, toTarget) > facingDotThreshold;
        }

        public void OnInteract(GameObject interactor)
        {
            onInteract?.Invoke(interactor);
        }

        public void OnEnterRange(GameObject interactor)
        {
            onEnterRange?.Invoke(interactor);
        }

        public void OnExitRange(GameObject interactor)
        {
            onExitRange?.Invoke(interactor);
        }
    }
}
