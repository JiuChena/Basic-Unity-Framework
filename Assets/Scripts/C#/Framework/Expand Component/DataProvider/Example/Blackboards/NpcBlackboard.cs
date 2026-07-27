using Framework.ExpandComponent.DataProvider;

namespace Framework.ExpandComponent.DataProvider.Example
{
    /// <summary>
    /// NPC 的数据面板。继承角色移动属性并追加交互能力，无战斗属性。
    /// </summary>
    public sealed class NpcBlackboard : CharacterBlackboard
    {
        /// <summary>交互按钮（对话、交易等）。</summary>
        public InteractAttribute Interact { get; }

        /// <summary>
        /// 构造时自动注册交互属性。
        /// </summary>
        public NpcBlackboard()
        {
            Interact = Register(new InteractAttribute());
        }
    }
}
