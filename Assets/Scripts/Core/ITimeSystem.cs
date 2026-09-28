using LastRefuge.Data;

namespace LastRefuge.Core
{
    public interface ITimeSystem
    {
        void Initialize(GameState state, IRandomSystem random);
        void SetTime(int day, TimeSlot timeSlot);
        void AdvanceTimeSlot();
        int CurrentDay { get; }
        TimeSlot CurrentTimeSlot { get; }
        void SetGameplayState(GameplayState newState);
        GameplayState GetGameplayState();
        string GetTimeDisplay();
        
        event System.Action<TimeSlot> OnTimeSlotChanged;
        event System.Action<int> OnDayChanged;
        event System.Action OnDayEnd;
    }
}