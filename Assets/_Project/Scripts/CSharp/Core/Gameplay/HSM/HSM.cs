using System;
using System.Collections.Generic;

namespace Core.Gear
{
    /// <summary>
    /// 轻量层次状态机。按类型注册状态，运行时切换。
    /// 当前阶段仅做骨架，后续与 IStateOwner 接口整合。
    /// </summary>
    public class HSM
    {
        // 状态类型到状态实例的注册表。
        private readonly Dictionary<Type, StateBase> _states = new Dictionary<Type, StateBase>();
        // 当前处于激活状态的状态实例。
        private StateBase _current;

        /// <summary>
        /// 获取当前激活的状态；尚未切换过状态时返回 null。
        /// </summary>
        public StateBase Current => _current;
        /// <summary>
        /// 注册一个可切换的状态实例。
        /// </summary>
        /// <param name="state">需要注册的状态；传入 null 时忽略。</param>
        public void AddState(StateBase state)
        {
            if (state == null) return;
            _states[state.GetType()] = state;
        }

        /// <summary>
        /// 切换到指定状态类型。
        /// </summary>
        /// <typeparam name="T">目标状态类型。</typeparam>
        /// <returns>实际完成状态切换时返回 true。</returns>
        public bool SwitchState<T>() where T : StateBase
        {
            if (!_states.TryGetValue(typeof(T), out var next)) return false;
            if (_current == next) return false;
            _current?.OnExit();
            _current = next;
            _current.OnEnter();
            return true;
        }

        /// <summary>
        /// 推进当前状态的一次逐帧更新；没有激活状态时不执行操作。
        /// </summary>
        public void Tick()
        {
            _current?.OnUpdate();
        }
    }
}
