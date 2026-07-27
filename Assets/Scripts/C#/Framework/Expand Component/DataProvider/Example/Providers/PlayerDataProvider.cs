using UnityEngine;
using Framework.ExpandComponent.DataProvider;

namespace Framework.ExpandComponent.DataProvider.Example
{
    /// <summary>玩家数据源的 Unity 装配点。</summary>
    public sealed class PlayerDataProvider : DataProviderBase<PlayerBlackboard>
    {
        [SerializeField] private PlayerDataSourceHandler _dataSource = new PlayerDataSourceHandler();

        public override PlayerBlackboard Blackboard { get; } = new PlayerBlackboard();

        protected override DataSourceHandler<PlayerBlackboard> DataSource => _dataSource;

        private void Update()
        {
            Tick();
        }
    }
}
