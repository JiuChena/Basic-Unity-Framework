namespace Framework.ExpandComponent.DataProvider
{
    /// <summary>
    /// 为实体采集或计算运行时数据，并写入其属性黑板。
    /// 每个需要数据驱动的实体挂载一个实现此接口的 MonoBehaviour。
    /// </summary>
    public interface IDataProvider
    {
        /// <summary>
        /// 当前实体持有的属性黑板。
        /// </summary>
        Blackboard Blackboard { get; }

        /// <summary>
        /// 每帧采集数据源，处理并写入 Blackboard 中的属性。
        /// </summary>
        void Tick();
    }
}
