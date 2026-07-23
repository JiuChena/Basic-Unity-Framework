using System;
using System.Collections.Generic;

namespace CoreFramework
{
    /// <summary>
    /// 轻量层次状态机。按类型注册状态，运行时切换。
    /// 当前阶段仅做骨架，后续与 IStateOwner 接口整合。
    /// </summary>
    public class HSM
    {
        private readonly Dictionary<Type, StateBase> _states = new Dictionary<Type, StateBase>();
        private StateBase _current;

        public StateBase Current => _current;
        public Func<InterruptPriority, bool> TransitionGuard { get; set; }

        public void AddState(StateBase state)
        {
            if (state == null) return;
            _states[state.GetType()] = state;
        }

        public bool SwitchState<T>(InterruptPriority priority = InterruptPriority.None, bool bypassGuard = false) where T : StateBase
        {
            if (!_states.TryGetValue(typeof(T), out var next)) return false;
            if (_current == next) return false;
            if (!bypassGuard && _current != null && TransitionGuard != null && !TransitionGuard(priority))
                return false;

            _current?.OnExit();
            _current = next;
            _current.OnEnter();
            return true;
        }

        public void Tick()
        {
            _current?.OnUpdate();
        }
    }
}
