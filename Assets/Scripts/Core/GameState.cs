using System;
using System.Collections.Generic;
using UnityEngine;
using LastRefuge.Data;

namespace LastRefuge.Core
{
    [System.Serializable]
    public class ResourceState
    {
        public ResourceType type;
        public int currentAmount;
        public int capacity;
        
        public ResourceState() { }
        
        public ResourceState(ResourceType type, int amount, int capacity)
        {
            this.type = type;
            this.currentAmount = amount;
            this.capacity = capacity;
        }
    }

    [System.Serializable]
    public class CharacterState
    {
        public string characterId;
        public string name;
        public Profession profession;
        
        public CharacterStats stats;
        public CharacterNeeds needs;
        
        public int health = 100;
        public int maxHealth = 100;
        public float hunger = 0f;
        public float stress = 0f;
        public float fatigue = 0f;
        
        public string[] traits;
        public string[] equipment;
        
        public WorkType currentWork = WorkType.Idle;
        public string assignedBuildingId;
        public string locationId;
        
        public bool alive = true;
        public int dayOfDeath = -1;
        public string deathCause;
        
        public Dictionary<string, int> relationships = new Dictionary<string, int>();
    }

    [System.Serializable]
    public class BuildingState
    {
        public string buildingId;
        public string definitionId;
        public int level = 1;
        public int durability = 100;
        public bool enabled = true;
        public string[] assignedWorkers;
        public int currentProduction = 0;
        public int dayBuilt;
    }

    [System.Serializable]
    public class RelationshipState
    {
        public string characterA;
        public string characterB;
        public int value = 0;
    }

    [System.Serializable]
    public class GameFlags : ISerializationCallbackReceiver
    {
        public Dictionary<string, bool> boolFlags = new Dictionary<string, bool>();
        public Dictionary<string, int> intFlags = new Dictionary<string, int>();
        public Dictionary<string, float> floatFlags = new Dictionary<string, float>();
        public Dictionary<string, string> stringFlags = new Dictionary<string, string>();

        public enum FlagKind
        {
            Bool,
            Int,
            Float,
            String
        }

        [System.Serializable]
        public class FlagEntry
        {
            public FlagKind kind;
            public string key;
            public bool boolValue;
            public int intValue;
            public float floatValue;
            public string stringValue;
        }

        [SerializeField]
        private FlagEntry[] serializedFlags = new FlagEntry[0];

        public void OnBeforeSerialize()
        {
            var entries = new List<FlagEntry>();

            if (boolFlags != null)
            {
                foreach (var kvp in boolFlags)
                {
                    entries.Add(new FlagEntry { kind = FlagKind.Bool, key = kvp.Key, boolValue = kvp.Value });
                }
            }
            if (intFlags != null)
            {
                foreach (var kvp in intFlags)
                {
                    entries.Add(new FlagEntry { kind = FlagKind.Int, key = kvp.Key, intValue = kvp.Value });
                }
            }
            if (floatFlags != null)
            {
                foreach (var kvp in floatFlags)
                {
                    entries.Add(new FlagEntry { kind = FlagKind.Float, key = kvp.Key, floatValue = kvp.Value });
                }
            }
            if (stringFlags != null)
            {
                foreach (var kvp in stringFlags)
                {
                    entries.Add(new FlagEntry { kind = FlagKind.String, key = kvp.Key, stringValue = kvp.Value });
                }
            }

            serializedFlags = entries.ToArray();
        }

        public void OnAfterDeserialize()
        {
            if (serializedFlags == null) return;

            boolFlags = new Dictionary<string, bool>();
            intFlags = new Dictionary<string, int>();
            floatFlags = new Dictionary<string, float>();
            stringFlags = new Dictionary<string, string>();

            foreach (var entry in serializedFlags)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key)) continue;

                switch (entry.kind)
                {
                    case FlagKind.Bool:
                        boolFlags[entry.key] = entry.boolValue;
                        break;
                    case FlagKind.Int:
                        intFlags[entry.key] = entry.intValue;
                        break;
                    case FlagKind.Float:
                        floatFlags[entry.key] = entry.floatValue;
                        break;
                    case FlagKind.String:
                        stringFlags[entry.key] = entry.stringValue;
                        break;
                }
            }
        }
    }

    [System.Serializable]
    public class EventHistoryEntry
    {
        public string eventId;
        public int day;
        public TimeSlot timeSlot;
        public string choiceId;
        public string[] effects;
    }

    [System.Serializable]
    public class GameState
    {
        public string gameSeed;
        public int currentDay = 1;
        public TimeSlot currentTimeSlot = TimeSlot.Morning;
        public GameplayState gameplayState = GameplayState.MainMenu;
        
        public ResourceState[] resources;
        public CharacterState[] characters;
        public BuildingState[] buildings;
        public RelationshipState[] relationships;
        
        public GameFlags gameFlags = new GameFlags();
        public EventHistoryEntry[] eventHistory;
        
        public int saveVersion = 1;
        public int contentVersion = 1;
        public string saveTimestamp;
        
        public WorldState worldState;
    }

    [System.Serializable]
    public class WorldState
    {
        public string currentWeather;
        public int weatherDuration;
        public string activeDisaster;
        public int disasterDuration;
        public string[] discoveredLocations;
        public Dictionary<string, int> factionRelations = new Dictionary<string, int>();
    }

    [System.Serializable]
    public class SaveData
    {
        public int saveVersion = 1;
        public int contentVersion = 1;
        public string gameVersion = "0.1.0";
        public string gameSeed;
        public GameState gameState;
        public string saveTimestamp;
        public bool isAutoSave;
    }
}