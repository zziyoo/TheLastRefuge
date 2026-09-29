using System;
using System.Collections.Generic;
using LastRefuge.Data;

namespace LastRefuge.Core
{
    public static class EventBus
    {
        private static Dictionary<Type, List<Delegate>> subscribers = new Dictionary<Type, List<Delegate>>();
        
        public static void Subscribe<T>(Action<T> handler)
        {
            Type type = typeof(T);
            if (!subscribers.ContainsKey(type))
            {
                subscribers[type] = new List<Delegate>();
            }
            subscribers[type].Add(handler);
        }
        
        public static void Unsubscribe<T>(Action<T> handler)
        {
            Type type = typeof(T);
            if (subscribers.ContainsKey(type))
            {
                subscribers[type].Remove(handler);
            }
        }
        
        public static void Publish<T>(T eventData)
        {
            Type type = typeof(T);
            if (subscribers.ContainsKey(type))
            {
                var handlers = subscribers[type];
                for (int i = handlers.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        ((Action<T>)handlers[i])?.Invoke(eventData);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"EventBus error publishing {type.Name}: {e}");
                    }
                }
            }
        }
        
        public static void Clear()
        {
            subscribers.Clear();
        }
    }

    // Core Events
    [System.Serializable]
    public struct ResourceChangedEvent
    {
        public ResourceType resourceType;
        public int oldAmount;
        public int newAmount;
        public int delta;
        public string source;
    }

    [System.Serializable]
    public struct CharacterChangedEvent
    {
        public string characterId;
        public string changeType;
        public string detail;
    }

    [System.Serializable]
    public struct BuildingChangedEvent
    {
        public string buildingId;
        public string changeType;
        public string detail;
    }

    [System.Serializable]
    public struct TimeChangedEvent
    {
        public int day;
        public TimeSlot oldTimeSlot;
        public TimeSlot newTimeSlot;
    }

    [System.Serializable]
    public struct DayChangedEvent
    {
        public int oldDay;
        public int newDay;
    }

    [System.Serializable]
    public struct GameStateChangedEvent
    {
        public GameplayState oldState;
        public GameplayState newState;
    }

    [System.Serializable]
    public struct GameSavedEvent
    {
        public bool isAutoSave;
        public string savePath;
    }

    [System.Serializable]
    public struct GameLoadedEvent
    {
        public string savePath;
    }

    [System.Serializable]
    public struct EventStartedEvent
    {
        public string eventId;
    }

    [System.Serializable]
    public struct EventFinishedEvent
    {
        public string eventId;
        public string choiceId;
    }

    [System.Serializable]
    public struct ExplorationStartedEvent
    {
        public string locationId;
        public int teamSize;
    }

    [System.Serializable]
    public struct ExplorationFinishedEvent
    {
        public string locationId;
        public int teamSize;
        public int survivors;
        public int casualties;
        public string resourceSummary;
    }

    [System.Serializable]
    public struct CharacterDiedEvent
    {
        public string characterId;
        public string cause;
    }

    [System.Serializable]
    public struct BuildingConstructedEvent
    {
        public string buildingId;
        public string definitionId;
    }
}