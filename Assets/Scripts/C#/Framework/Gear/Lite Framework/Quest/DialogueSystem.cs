using System;

namespace Framework.Core
{
    /// <summary>
    /// 对话系统，管理对话树状态和节点跳转。
    /// </summary>
    public class DialogueSystem
    {
        private static readonly DialogueSystem _instance = new DialogueSystem();
        public static DialogueSystem Instance => _instance;

        private Action onDialogueEndOnce;
        private DialogueDataSO currentDialogue;
        private DialogueNode currentNode;

        public event Action<DialogueNode> OnNodeChanged;
        public event Action OnDialogueEnd;

        public DialogueNode CurrentNode => currentNode;

        /// <summary>
        /// 开始一段对话。
        /// </summary>
        /// <param name="data">对话数据</param>
        /// <param name="onEnded">对话结束后的回调</param>
        public void StartDialogue(DialogueDataSO data, Action onEnded = null)
        {
            if (data == null)
                return;

            currentDialogue = data;
            onDialogueEndOnce = onEnded;
            EventCenter.Instance.SetEventTrigger(EventNames.DialogueStarted);
            GoToNode(data.startNodeID);
        }

        /// <summary>
        /// 继续对话（无选项时玩家按继续）。
        /// </summary>
        public void Continue()
        {
            if (currentNode == null)
                return;

            DialogueChoice[] choices = currentNode.choices ?? Array.Empty<DialogueChoice>();
            if (choices.Length > 0)
                return;
            if (string.IsNullOrWhiteSpace(currentNode.nextNodeID))
            {
                EndDialogue();
                return;
            }

            GoToNode(currentNode.nextNodeID);
        }

        /// <summary>
        /// 玩家选择对话选项。
        /// </summary>
        public void SelectChoice(int index)
        {
            if (currentNode == null)
                return;

            DialogueChoice[] choices = currentNode.choices ?? Array.Empty<DialogueChoice>();
            if (index < 0 || index >= choices.Length)
                return;

            DialogueChoice choice = choices[index];
            if (!string.IsNullOrWhiteSpace(choice.triggerEventName))
                EventCenter.Instance.SetEventTrigger(choice.triggerEventName);
            if (string.IsNullOrWhiteSpace(choice.nextNodeID))
            {
                EndDialogue();
                return;
            }

            GoToNode(choice.nextNodeID);
        }

        private void GoToNode(string nodeID)
        {
            if (currentDialogue == null)
                return;

            currentNode = currentDialogue.GetNode(nodeID);
            if (currentNode == null)
            {
                EndDialogue();
                return;
            }

            OnNodeChanged?.Invoke(currentNode);
        }

        private void EndDialogue()
        {
            currentDialogue = null;
            currentNode = null;
            Action callback = onDialogueEndOnce;
            onDialogueEndOnce = null;
            EventCenter.Instance.SetEventTrigger(EventNames.DialogueEnded);
            callback?.Invoke();
            OnDialogueEnd?.Invoke();
        }
    }
}
