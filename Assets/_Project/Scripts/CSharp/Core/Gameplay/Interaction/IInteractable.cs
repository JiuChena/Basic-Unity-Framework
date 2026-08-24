using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 可交互对象接口。挂载到 NPC、道具、门等物体上，由 InteractionEmitter 在玩家进入范围时注册到 InteractionReceiver。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>交互唯一标识，用于任务系统的 InteractionPerformed 事件匹配。</summary>
        string InteractionID { get; }

        /// <summary>交互提示文本，如 "F - 打开宝箱"。</summary>
        string PromptText { get; }

        /// <summary>交互选项图标，null 则不显示图标。</summary>
        Sprite Icon { get; }

        /// <summary>选项混合色，用于 UI 背景或高亮。</summary>
        Color BlendColor { get; }

        /// <summary>优先级，多个交互重叠时数值大的排在前面。</summary>
        int Priority { get; }

        /// <summary>交互者是否可以与当前对象交互（如面向检测、状态检测）。</summary>
        bool CanInteract(GameObject interactor);

        /// <summary>执行交互逻辑。</summary>
        void OnInteract(GameObject interactor);

        /// <summary>玩家进入交互范围时回调。</summary>
        void OnEnterRange(GameObject interactor);

        /// <summary>玩家离开交互范围时回调。</summary>
        void OnExitRange(GameObject interactor);
    }
}
