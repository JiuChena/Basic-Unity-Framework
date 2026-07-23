using UnityEngine;
using UnityEngine.Events;

namespace CoreFramework
{
    /// <summary>
    /// 交互选项的显示信息，由 InteractionReceiver 用于渲染选项 UI。
    /// </summary>
    public class OptionInfo
    {
        /// <summary>选项图标。</summary>
        public Sprite sprite;

        /// <summary>选项文本。</summary>
        public string text;

        /// <summary>选项混合色。</summary>
        public Color color = Color.white;

        /// <summary>选中后执行的回调。</summary>
        public UnityAction action;
    }
}
