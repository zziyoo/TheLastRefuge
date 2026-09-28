using System;
using System.Collections.Generic;
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
    public class GameFlags
    {
        public Dictionary<string, bool> boolFlags = new Dictionary<string, bool>();
        public Dictionary<string, int> intFlags = new Dictionary<string, int>();
        public Dictionary<string, float> floatFlags = new Dictionary<string, float>();
        public Dictionary<string, string> stringFlags = new Dictionary<string, string>();
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