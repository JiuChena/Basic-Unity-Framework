using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 进入 Timeline 作者期时传递给轨道编译器的结构上下文。
    /// </summary>
    internal sealed class BehaviorAuthoringContext
    {
        /// <summary>正在准备的 Timeline 资产。</summary>
        public TimelineAsset Timeline { get; }

        /// <summary>
        /// 创建 Timeline 作者期上下文。
        /// </summary>
        /// <param name="timeline">需要补齐轨道的 Timeline 资产。</param>
        public BehaviorAuthoringContext(TimelineAsset timeline)
        {
            Timeline = timeline;
        }
    }
}
