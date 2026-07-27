using Framework.ExpandComponent.DataProvider;

namespace Framework.ExpandComponent.DataProvider.Example
{
    /// <summary>
    /// 交互动作按钮（调查、对话、拾取等）。
    /// </summary>
    public sealed class InteractAttribute : ButtonAttribute { }

    /// <summary>
    /// 滚轮增量。每个消费者独立消费自上次读取后的累计滚动量。
    /// </summary>
    public sealed class ScrollAttribute : IntDeltaAttribute { }
}
