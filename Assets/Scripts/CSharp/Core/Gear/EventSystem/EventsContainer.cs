using UnityEngine.Events;

namespace Core.Gear
{
    internal class EventsContainer : IEventsContainer
    {
        public UnityAction eventsContainer;
        public EventsContainer(UnityAction action) { eventsContainer += action; }
    }
}
