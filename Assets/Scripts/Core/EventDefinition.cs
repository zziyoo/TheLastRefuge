using LastRefuge.Data;

namespace LastRefuge.Core
{
    public enum EventType
    {
        Normal,
        Character,
        Exploration,
        Crisis
    }

    public enum ConditionType
    {
        ResourceAtLeast,
        ResourceBelow,
        FlagIs,
        FlagNot,
        HasTrait,
        HasProfession,
        DayBetween,
        LocationDiscovered,
        BuildingExists
    }

    [System.Serializable]
    public class EventCondition
    {
        public ConditionType type;
        public ResourceType resource;
        public string targetId;
        public int amount;
        public int minDay;
        public int maxDay;
        public string stringValue;
    }

    [System.Serializable]
    public class EventOption
    {
        public string id;
        public string text;
        public EventCondition[] conditions;
        public Effect[] effects;
    }

    [System.Serializable]
    public class FollowUpEvent
    {
        public string id;
        public int delayDays;
    }

    [System.Serializable]
    public class EventDefinition
    {
        public string id;
        public string name;
        public string description;
        public EventType type;
        public TimeSlot phase;
        public float weight;
        public int cooldownDays;
        public int minDay;
        public int maxDay;
        public EventCondition[] conditions;
        public EventOption[] options;
        public FollowUpEvent[] followUpEvents;
        public string[] tags;
    }
}
