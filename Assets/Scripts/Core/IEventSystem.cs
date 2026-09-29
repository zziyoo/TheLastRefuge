using LastRefuge.Data;

namespace LastRefuge.Core
{
    public interface IEventSystem
    {
        void Initialize(GameState state, ITimeSystem timeSystem, IRandomSystem randomSystem,
                        IContentDatabase contentDatabase, EffectResolver effectResolver,
                        IResourceSystem resourceSystem, ICharacterSystem characterSystem);

        EventDefinition PendingEvent { get; }

        bool TryRollDailyEvent();
        bool TryStartEvent(string eventId);
        bool TryStartLocationEvent(string locationId);
        void ScheduleNow(string eventId);
        bool ResolveChoice(string optionId);
        bool IsOptionAvailable(string optionId);
        bool CanResolveChoice(string optionId, out string reason);
        bool EvaluateConditions(EventCondition[] conditions);
        void ClearPending();
    }
}
