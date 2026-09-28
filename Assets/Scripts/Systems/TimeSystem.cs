using System;
using LastRefuge.Data;
using LastRefuge.Core;

namespace LastRefuge.Systems
{
    public class TimeSystem : ITimeSystem
    {
        private GameState gameState;
        private IRandomSystem randomSystem;
        
        public event Action<TimeSlot> OnTimeSlotChanged;
        public event Action<int> OnDayChanged;
        public event Action OnDayEnd;
        
        public int CurrentDay => gameState.currentDay;
        public TimeSlot CurrentTimeSlot => gameState.currentTimeSlot;
        
        public void Initialize(GameState state, IRandomSystem random)
        {
            gameState = state;
            randomSystem = random;
        }
        
        public void SetTime(int day, TimeSlot timeSlot)
        {
            gameState.currentDay = day;
            gameState.currentTimeSlot = timeSlot;
        }
        
        public void AdvanceTimeSlot()
        {
            TimeSlot oldSlot = gameState.currentTimeSlot;
            TimeSlot newSlot = oldSlot.Next();
            
            gameState.currentTimeSlot = newSlot;
            
            EventBus.Publish(new TimeChangedEvent
            {
                day = gameState.currentDay,
                oldTimeSlot = oldSlot,
                newTimeSlot = newSlot
            });
            
            OnTimeSlotChanged?.Invoke(newSlot);
            
            if (newSlot == TimeSlot.Morning && oldSlot == TimeSlot.DayEnd)
            {
                AdvanceDay();
            }
        }
        
        public void AdvanceDay()
        {
            int oldDay = gameState.currentDay;
            gameState.currentDay++;
            
            EventBus.Publish(new DayChangedEvent
            {
                oldDay = oldDay,
                newDay = gameState.currentDay
            });
            
            OnDayChanged?.Invoke(gameState.currentDay);
            OnDayEnd?.Invoke();
        }
        
        public void SetGameplayState(GameplayState newState)
        {
            GameplayState oldState = gameState.gameplayState;
            gameState.gameplayState = newState;
            
            EventBus.Publish(new GameStateChangedEvent
            {
                oldState = oldState,
                newState = newState
            });
        }
        
        public GameplayState GetGameplayState()
        {
            return gameState.gameplayState;
        }
        
        public string GetTimeDisplay()
        {
            return $"第 {gameState.currentDay} 天 {gameState.currentTimeSlot.GetDisplayName()}";
        }
    }
}