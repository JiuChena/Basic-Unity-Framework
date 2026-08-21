using System;
using UnityEngine;

namespace Core.Gear
{
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
}
