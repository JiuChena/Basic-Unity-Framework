using Framework.ExpandComponent.DataProvider;

namespace Framework.ExpandComponent.DataProvider.Example
{
    /// <summary>
    /// 玩家角色的完整数据面板。继承 CharacterBlackboard 的全部移动属性，
    /// 并追加交互和滚轮两个玩家专用输入。
    /// </summary>
    public sealed class PlayerBlackboard : CharacterBlackboard
    {
        /// <summary>交互按钮（调查、对话、拾取）。</summary>
        public InteractAttribute Interact { get; }

        /// <summary>滚轮增量（多消费者独立消费）。</summary>
        public ScrollAttribute Scroll { get; }

        /// <summary>
        /// 构造时自动注册玩家特有属性。
        /// </summary>
        public PlayerBlackboard()
        {
            Interact = Register(new InteractAttribute());
            Scroll = Register(new ScrollAttribute());
        }
    }
}
