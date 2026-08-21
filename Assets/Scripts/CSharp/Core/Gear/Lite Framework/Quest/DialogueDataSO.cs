using System;
using UnityEngine;

namespace Core.Gear
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
}
