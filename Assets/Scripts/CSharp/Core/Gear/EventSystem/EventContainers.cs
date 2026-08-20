using UnityEngine.Events;

namespace Core.Gear
{
    internal class EventsContainer : IEventsContainer
    {
        public UnityAction eventsContainer;
        public EventsContainer(UnityAction action) { eventsContainer += action; }
    }

    internal class EventsContainer<T> : IEventsContainer
    {
        public UnityAction<T> eventsContainer;
        public EventsContainer(UnityAction<T> action) { eventsContainer += action; }
    }

    internal class EventsContainer<T, K> : IEventsContainer
    {
        public UnityAction<T, K> eventsContainer;
        public EventsContainer(UnityAction<T, K> action) { eventsContainer += action; }
    }
}
