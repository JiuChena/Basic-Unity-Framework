using UnityEngine;
using Framework.ExpandComponent.DataProvider;

namespace Framework.ExpandComponent.DataProvider.Example
{
    /// <summary>玩家数据源的 Unity 装配点。</summary>
    public sealed class PlayerDataProvider : DataProviderBase<PlayerBlackboard>
    {
        // 负责采集玩家输入并写入 PlayerBlackboard 的数据源处理器。
        [SerializeField] private PlayerDataSourceHandler _dataSource = new PlayerDataSourceHandler();
        // 是否在编辑器或开发构建中输出最近一次处理完成的玩家输入数据。
        [Tooltip("是否输出最近一次处理完成的移动、冲刺和跳跃输入数据，用于排查数据链路")]
        [SerializeField] private bool _enableDataDebug;
        // 两次玩家输入调试日志之间的最小间隔，避免每帧输出淹没 Console。
        [Tooltip("玩家输入调试日志的最小输出间隔，单位：秒")]
        [SerializeField, Min(0.1f)] private float _dataDebugInterval = 0.25f;
        // 下一次允许输出玩家输入调试日志的非缩放时间。
        private float _nextDataDebugTime;

        /// <summary>获取当前玩家专属的属性黑板。</summary>
        public override PlayerBlackboard Blackboard { get; } = new PlayerBlackboard();

        /// <summary>获取负责写入玩家黑板的数据源处理器。</summary>
        protected override DataSourceHandler<PlayerBlackboard> DataSource => _dataSource;

        /// <summary>
        /// 每帧更新玩家输入并写入自己的 PlayerBlackboard。
        /// </summary>
        private void Update()
        {
            Tick();
        }

        /// <summary>
        /// 按需输出已经写入 PlayerBlackboard 的最新玩家输入，帮助定位输入采集是否正常。
        /// </summary>
        /// <param name="blackboard">本次 Tick 已完成更新的玩家黑板。</param>
        protected override void DebugData(PlayerBlackboard blackboard)
        {
            if (!_enableDataDebug || Time.unscaledTime < _nextDataDebugTime) return;

            _nextDataDebugTime = Time.unscaledTime + _dataDebugInterval;
            Debug.Log(
                $"[PlayerDataProvider] Move={blackboard.Move.Value}, "
                + $"Sprint={blackboard.Sprint.Value}, JumpHeld={blackboard.Jump.IsHeld}",
                this);
        }
    }
}
