using System;
using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// 对话树数据配置（ScriptableObject），定义对话节点和玩家选项。
    /// </summary>
    [CreateAssetMenu(menuName = "Framework/CoreFramework/Quest/Dialogue Data")]
    public class DialogueDataSO : ScriptableObject
    {
        public string startNodeID;
        public DialogueNode[] nodes = Array.Empty<DialogueNode>();

        public DialogueNode GetNode(string id)
        {
            if (nodes == null) return null;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null && nodes[i].nodeID == id)
                    return nodes[i];
            }

            return null;
        }
    }

    /// <summary>
    /// 对话节点，包含发言者、内容、玩家选项和下一节点 ID。
    /// </summary>
    [Serializable]
    public class DialogueNode
    {
        public string nodeID;
        public string speakerName;

        [TextArea]
        public string content;

        public DialogueChoice[] choices = Array.Empty<DialogueChoice>();
        public string nextNodeID;
    }

    /// <summary>
    /// 对话选项，玩家选择后跳转到对应节点或触发事件。
    /// </summary>
    [Serializable]
    public class DialogueChoice
    {
        public string text;
        public string nextNodeID;
        public string triggerEventName;
    }
}
