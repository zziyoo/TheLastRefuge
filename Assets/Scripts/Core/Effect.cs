using System;
using LastRefuge.Data;

namespace LastRefuge.Core
{
    public enum EffectType
    {
        AddResource,
        RemoveResource,
        DamageCharacter,
        HealCharacter,
        AddStress,
        RemoveStress,
        AddFatigue,
        RemoveFatigue,
        AddHunger,
        RemoveHunger,
        SetHealth,
        AddTrait,
        RemoveTrait,
        AddRelationship,
        RemoveRelationship,
        DamageBuilding,
        RepairBuilding,
        SetBuildingEnabled,
        AddModifier,
        RemoveModifier,
        UnlockTechnology,
        UnlockLocation,
        StartEvent,
        SetFlag,
        ClearFlag,
        KillCharacter,
        ChangeWork
    }

    [System.Serializable]
    public class Effect
    {
        public EffectType type;
        public string targetId;
        public ResourceType resourceType;
        public int intValue;
        public float floatValue;
        public string stringValue;
        public string[] stringArrayValue;
        
        public Effect() { }
        
        public Effect(EffectType type, string targetId = null, int intValue = 0, float floatValue = 0f, string stringValue = null)
        {
            this.type = type;
            this.targetId = targetId;
            this.intValue = intValue;
            this.floatValue = floatValue;
            this.stringValue = stringValue;
        }
        
        public static Effect AddResource(ResourceType resource, int amount, string source = "Effect")
        {
            return new Effect(EffectType.AddResource, source, amount);
        }
        
        public static Effect RemoveResource(ResourceType resource, int amount, string source = "Effect")
        {
            return new Effect(EffectType.RemoveResource, source, amount);
        }
        
        public static Effect DamageCharacter(string characterId, int amount)
        {
            return new Effect(EffectType.DamageCharacter, characterId, amount);
        }
        
        public static Effect HealCharacter(string characterId, int amount)
        {
            return new Effect(EffectType.HealCharacter, characterId, amount);
        }
        
        public static Effect AddStress(string characterId, float amount)
        {
            return new Effect(EffectType.AddStress, characterId, 0, amount);
        }
        
        public static Effect RemoveStress(string characterId, float amount)
        {
            return new Effect(EffectType.RemoveStress, characterId, 0, amount);
        }
        
        public static Effect DamageBuilding(string buildingId, int amount)
        {
            return new Effect(EffectType.DamageBuilding, buildingId, amount);
        }
        
        public static Effect RepairBuilding(string buildingId, int amount)
        {
            return new Effect(EffectType.RepairBuilding, buildingId, amount);
        }
        
        public static Effect SetFlag(string flagName, string value = "true")
        {
            return new Effect(EffectType.SetFlag, flagName, 0, 0f, value);
        }
        
        public static Effect StartEvent(string eventId)
        {
            return new Effect(EffectType.StartEvent, eventId);
        }
    }
}