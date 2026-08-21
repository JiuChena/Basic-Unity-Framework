using UnityEngine.Events;

namespace Core.Gear
{
    internal class EventsContainer<T> : IEventsContainer
    {
        public UnityAction<T> eventsContainer;
        public EventsContainer(UnityAction<T> action) { eventsContainer += action; }
    }
}
