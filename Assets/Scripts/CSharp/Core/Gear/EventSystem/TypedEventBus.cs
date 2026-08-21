using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 轻量级强类型事件总线。
    /// 仅用于跨系统通知，不承担查询、命令返回值和强顺序主链路。
    /// </summary>
    public static class TypedEventBus
    {
        private static readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>(16);

        public static void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null)
                return;

            Type eventType = typeof(TEvent);
            if (handlers.TryGetValue(eventType, out Delegate existing))
                handlers[eventType] = Delegate.Combine(existing, handler);
            else
                handlers.Add(eventType, handler);
        }

        public static void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null)
                return;

            Type eventType = typeof(TEvent);
            if (!handlers.TryGetValue(eventType, out Delegate existing))
                return;

            Delegate updated = Delegate.Remove(existing, handler);
            if (updated == null)
                handlers.Remove(eventType);
            else
                handlers[eventType] = updated;
        }

        public static void Publish<TEvent>(TEvent eventData) where TEvent : struct
        {
            if (!handlers.TryGetValue(typeof(TEvent), out Delegate existing))
                return;

            if (existing is Action<TEvent> callback)
                callback.Invoke(eventData);
        }

        public static void Clear()
        {
            handlers.Clear();
        }
    }
}
