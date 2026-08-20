using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 由数据黑板实现的通用移动输入读取契约。
    /// 具体移动策略按需读取该契约，不依赖任何具体 Provider、Blackboard 或 Attribute 类型。
    /// </summary>
    public interface IUnitMovementInput
    {
        /// <summary>获取已转换到世界空间的当前平面移动方向。</summary>
        Vector3 WorldMoveDirection { get; }

        /// <summary>获取当前移动速度倍率。</summary>
        float SpeedScale { get; }

        /// <summary>获取当前跳跃输入是否仍处于按住状态。</summary>
        bool IsJumpHeld { get; }

        /// <summary>
        /// 初始化一个独立消费者的跳跃按下事件游标。
        /// </summary>
        /// <param name="pressedVersion">需要初始化的消费者游标。</param>
        void InitializeJumpPressedCursor(ref uint pressedVersion);

        /// <summary>
        /// 消费从指定游标之后产生的跳跃按下事件。
        /// </summary>
        /// <param name="pressedVersion">消费者上次读取到的事件版本。</param>
        /// <param name="pressed">是否存在未消费的跳跃按下事件。</param>
        /// <returns>是否成功读取跳跃输入。</returns>
        bool ConsumeJumpPressed(ref uint pressedVersion, out bool pressed);
    }
}
