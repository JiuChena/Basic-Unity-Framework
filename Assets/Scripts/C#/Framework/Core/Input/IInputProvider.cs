namespace CoreFramework
{
    /// <summary>
    /// 输入提供者接口。所有输入源（键盘、手柄、AI、录像回放）实现此接口，
    /// 每帧将数据写入 Blackboard。上层控制器不关心具体来源。
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>
        /// 每帧由上层控制器调用，将当帧输入数据写入黑板。
        /// </summary>
        /// <param name="board">共享数据黑板</param>
        void Tick(Blackboard board);
    }
}
