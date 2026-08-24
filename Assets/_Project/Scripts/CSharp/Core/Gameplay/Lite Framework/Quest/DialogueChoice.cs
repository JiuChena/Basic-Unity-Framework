using System;
using UnityEngine;

namespace Core.Gear
{
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
