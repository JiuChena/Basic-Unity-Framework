namespace Framework.ExpandComponent.DataProvider
{
    /// <summary>
    /// 可由持续状态或瞬时命令驱动的通用按钮属性基类。
    /// InputButton 完全内聚不外露，写入走 SetHeld 或 Trigger，读取走 Consume。
    /// </summary>
    public abstract class ButtonAttribute : BlackboardAttribute
    {
        private readonly InputButton _value = new InputButton();

        /// <summary>当前是否处于按住状态。</summary>
        public bool IsHeld => _value.IsHeld;

        /// <summary>
        /// 根据持续按住状态自动推导按下/抬起边沿。
        /// 普通设备输入使用此方法，AI 或回放的瞬时动作使用 Trigger。
        /// </summary>
        /// <param name="held">当前帧是否按住</param>
        public void SetHeld(bool held) => _value.SetHeld(held);

        /// <summary>
        /// 写入一次不改变持续状态的瞬时动作命令。
        /// 用于 AI、回放脚本等不需要持续状态仅需触发一次的场景。
        /// </summary>
        public void Trigger() => _value.Trigger();

        /// <summary>将按下事件游标对齐到当前版本，后续 ConsumePressed 只会收到未来事件。</summary>
        public void InitializePressedCursor(ref uint consumedVersion) => _value.InitializePressedCursor(ref consumedVersion);

        /// <summary>将抬起事件游标对齐到当前版本，后续 ConsumeReleased 只会收到未来事件。</summary>
        public void InitializeReleasedCursor(ref uint consumedVersion) => _value.InitializeReleasedCursor(ref consumedVersion);

        /// <summary>
        /// 按消费者游标读取一次尚未处理的按下事件。每个消费者独立消费，互不干扰。
        /// 新消费者应先调用 InitializePressedCursor 对齐游标。
        /// </summary>
        /// <param name="consumedVersion">调用方持有的最后已消费版本号</param>
        /// <param name="pressed">未消费的按下事件存在时为 true</param>
        /// <returns>存在未消费的按下事件时返回 true</returns>
        public bool ConsumePressed(ref uint consumedVersion, out bool pressed)
        {
            return _value.ConsumePressed(ref consumedVersion, out pressed);
        }

        /// <summary>
        /// 按消费者游标读取一次尚未处理的抬起事件。每个消费者独立消费，互不干扰。
        /// 新消费者应先调用 InitializeReleasedCursor 对齐游标。
        /// </summary>
        /// <param name="consumedVersion">调用方持有的最后已消费版本号</param>
        /// <param name="released">未消费的抬起事件存在时为 true</param>
        /// <returns>存在未消费的抬起事件时返回 true</returns>
        public bool ConsumeReleased(ref uint consumedVersion, out bool released)
        {
            return _value.ConsumeReleased(ref consumedVersion, out released);
        }
    }
}
