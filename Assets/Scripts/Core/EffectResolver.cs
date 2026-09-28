using System;
using System.Collections.Generic;
using System.Linq;
using LastRefuge.Data;
using LastRefuge.Core;

namespace LastRefuge.Core
{
    public class EffectResolver
    {
        private GameState gameState;
        private IResourceSystem resourceSystem;
        private ICharacterSystem characterSystem;
        private IBuildingSystem buildingSystem;
        
        public void Initialize(GameState state, IResourceSystem resSys, ICharacterSystem charSys, IBuildingSystem buildSys)
        {
            gameState = state;
            resourceSystem = resSys;
            characterSystem = charSys;
            buildingSystem = buildSys;
        }
        
        public bool Resolve(Effect effect)
        {
            try
            {
                return ResolveInternal(effect);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"EffectResolver error: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }
        
        public bool ResolveAll(Effect[] effects)
        {
            bool allSuccess = true;
            foreach (var effect in effects)
            {
                if (!Resolve(effect))
                {
                    allSuccess = false;
                }
            }
            return allSuccess;
        }
        
        private bool ResolveInternal(Effect effect)
        {
            switch (effect.type)
            {
                case EffectType.AddResource:
                    return resourceSystem.Add(effect.resourceType, effect.intValue, effect.targetId);
                
                case EffectType.RemoveResource:
                    return resourceSystem.Remove(effect.resourceType, effect.intValue, effect.targetId);
                
                case EffectType.DamageCharacter:
                    return DamageCharacter(effect.targetId, effect.intValue);
                
                case EffectType.HealCharacter:
                    return HealCharacter(effect.targetId, effect.intValue);
                
                case EffectType.AddStress:
                    return ModifyStress(effect.targetId, effect.floatValue);
                
                case EffectType.RemoveStress:
                    return ModifyStress(effect.targetId, -effect.floatValue);
                
                case EffectType.AddFatigue:
                    return ModifyFatigue(effect.targetId, effect.floatValue);
                
                case EffectType.RemoveFatigue:
                    return ModifyFatigue(effect.targetId, -effect.floatValue);
                
                case EffectType.AddHunger:
                    return ModifyHunger(effect.targetId, effect.floatValue);
                
                case EffectType.RemoveHunger:
                    return ModifyHunger(effect.targetId, -effect.floatValue);
                
                case EffectType.SetHealth:
                    return SetHealth(effect.targetId, effect.intValue);
                
                case EffectType.AddTrait:
                    return AddTrait(effect.targetId, effect.stringValue);
                
                case EffectType.RemoveTrait:
                    return RemoveTrait(effect.targetId, effect.stringValue);
                
                case EffectType.AddRelationship:
                    return ModifyRelationship(effect.targetId, effect.intValue);
                
                case EffectType.RemoveRelationship:
                    return ModifyRelationship(effect.targetId, -effect.intValue);
                
                case EffectType.DamageBuilding:
                    return DamageBuilding(effect.targetId, effect.intValue);
                
                case EffectType.RepairBuilding:
                    return RepairBuilding(effect.targetId, effect.intValue);
                
                case EffectType.SetBuildingEnabled:
                    return SetBuildingEnabled(effect.targetId, effect.intValue == 1);
                
                case EffectType.SetFlag:
                    return SetFlag(effect.targetId, effect.stringValue);
                
                case EffectType.ClearFlag:
                    return ClearFlag(effect.targetId);
                
                case EffectType.KillCharacter:
                    characterSystem.KillCharacter(effect.targetId, effect.stringValue ?? "Effect");
                    return true;
                
                case EffectType.ChangeWork:
                    return characterSystem.AssignWork(effect.targetId, (WorkType)effect.intValue, effect.stringValue);
                
                default:
                    UnityEngine.Debug.LogWarning($"Unknown effect type: {effect.type}");
                    return false;
            }
        }
        
        private bool DamageCharacter(string characterId, int amount)
        {
            var character = characterSystem.GetCharacter(characterId);
            if (character == null || !character.alive) return false;
            
            character.health = Math.Max(0, character.health - amount);
            
            if (character.health <= 0)
            {
                characterSystem.KillCharacter(characterId, "Damage");
            }
            
            return true;
        }
        
        private bool HealCharacter(string characterId, int amount)
        {
            var character = characterSystem.GetCharacter(characterId);
            if (character == null || !character.alive) return false;
            
            character.health = Math.Min(character.maxHealth, character.health + amount);
            return true;
        }
        
        private bool ModifyStress(string characterId, float amount)
        {
            var character = characterSystem.GetCharacter(characterId);
            if (character == null || !character.alive) return false;
            
            character.stress = Math.Clamp(character.stress + amount, 0, 100);
            return true;
        }
        
        private bool ModifyFatigue(string characterId, float amount)
        {
            var character = characterSystem.GetCharacter(characterId);
            if (character == null || !character.alive) return false;
            
            character.fatigue = Math.Clamp(character.fatigue + amount, 0, 100);
            return true;
        }
        
        private bool ModifyHunger(string characterId, float amount)
        {
            var character = characterSystem.GetCharacter(characterId);
            if (character == null || !character.alive) return false;
            
            character.hunger = Math.Clamp(character.hunger + amount, 0, 100);
            return true;
        }
        
        private bool SetHealth(string characterId, int health)
        {
            var character = characterSystem.GetCharacter(characterId);
            if (character == null) return false;
            
            character.health = Math.Clamp(health, 0, character.maxHealth);
            if (character.health <= 0 && character.alive)
            {
                characterSystem.KillCharacter(characterId, "SetHealth");
            }
            return true;
        }
        
        private bool AddTrait(string characterId, string traitId)
        {
            var character = characterSystem.GetCharacter(characterId);
            if (character == null) return false;
            
            var traits = new List<string>(character.traits ?? new string[0]);
            if (!traits.Contains(traitId))
            {
                traits.Add(traitId);
                character.traits = traits.ToArray();
            }
            return true;
        }
        
        private bool RemoveTrait(string characterId, string traitId)
        {
            var character = characterSystem.GetCharacter(characterId);
            if (character == null) return false;
            
            var traits = new List<string>(character.traits ?? new string[0]);
            traits.Remove(traitId);
            character.traits = traits.ToArray();
            return true;
        }
        
        private bool ModifyRelationship(string targetId, int delta)
        {
            var parts = targetId.Split(':');
            if (parts.Length != 2) return false;
            
            var rels = gameState.relationships?.ToList() ?? new List<RelationshipState>();
            var existing = rels.Find(r => 
                (r.characterA == parts[0] && r.characterB == parts[1]) ||
                (r.characterA == parts[1] && r.characterB == parts[0]));
            
            if (existing == null)
            {
                existing = new RelationshipState { characterA = parts[0], characterB = parts[1], value = 0 };
                rels.Add(existing);
            }
            
            existing.value = Math.Clamp(existing.value + delta, -100, 100);
            gameState.relationships = rels.ToArray();
            return true;
        }
        
        private bool DamageBuilding(string buildingId, int amount)
        {
            var building = buildingSystem.GetBuilding(buildingId);
            if (building == null) return false;
            
            building.durability = Math.Max(0, building.durability - amount);
            if (building.durability <= 0)
            {
                building.enabled = false;
            }
            return true;
        }
        
        private bool RepairBuilding(string buildingId, int amount)
        {
            var building = buildingSystem.GetBuilding(buildingId);
            if (building == null) return false;
            
            var def = buildingSystem.GetBuildingDefinition(building.definitionId);
            int maxDurability = 100;
            building.durability = Math.Min(maxDurability, building.durability + amount);
            if (building.durability > 0)
            {
                building.enabled = true;
            }
            return true;
        }
        
        private bool SetBuildingEnabled(string buildingId, bool enabled)
        {
            var building = buildingSystem.GetBuilding(buildingId);
            if (building == null) return false;
            
            building.enabled = enabled;
            return true;
        }
        
        private bool SetFlag(string flagName, string value)
        {
            gameState.gameFlags.stringFlags[flagName] = value;
            
            if (bool.TryParse(value, out bool boolVal))
            {
                gameState.gameFlags.boolFlags[flagName] = boolVal;
            }
            if (int.TryParse(value, out int intVal))
            {
                gameState.gameFlags.intFlags[flagName] = intVal;
            }
            if (float.TryParse(value, out float floatVal))
            {
                gameState.gameFlags.floatFlags[flagName] = floatVal;
            }
            return true;
        }
        
        private bool ClearFlag(string flagName)
        {
            gameState.gameFlags.boolFlags.Remove(flagName);
            gameState.gameFlags.intFlags.Remove(flagName);
            gameState.gameFlags.floatFlags.Remove(flagName);
            gameState.gameFlags.stringFlags.Remove(flagName);
            return true;
        }
    }
}