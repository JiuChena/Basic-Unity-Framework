using UnityEngine.Events;

namespace Core.Gear
{
    internal class EventsContainer<T, K> : IEventsContainer
    {
        public UnityAction<T, K> eventsContainer;
        public EventsContainer(UnityAction<T, K> action) { eventsContainer += action; }
    }
}
