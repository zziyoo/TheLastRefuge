using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LastRefuge.Data;
using LastRefuge.Core;

namespace LastRefuge.Systems
{
    public class CharacterSystem : ICharacterSystem
    {
        private GameState gameState;
        private IResourceSystem resourceSystem;
        private IRandomSystem randomSystem;
        private IBuildingSystem buildingSystem;
        
        public event Action<string> OnCharacterChanged;
        public event Action<string> OnCharacterDied;
        public event Action<string, WorkType> OnWorkAssigned;
        
        public void Initialize(GameState state, IResourceSystem resSys, IRandomSystem randSys, IBuildingSystem buildSys)
        {
            gameState = state;
            resourceSystem = resSys;
            randomSystem = randSys;
            buildingSystem = buildSys;
        }
        
        public CharacterState[] GetAllCharacters()
        {
            return gameState.characters ?? new CharacterState[0];
        }
        
        public CharacterState[] GetAliveCharacters()
        {
            if (gameState.characters == null) return new CharacterState[0];
            return gameState.characters.Where(c => c.alive).ToArray();
        }
        
        public CharacterState GetCharacter(string id)
        {
            if (gameState.characters == null) return null;
            return gameState.characters.FirstOrDefault(c => c.characterId == id);
        }
        
        public CharacterState CreateCharacter(string id, string name, Profession profession, CharacterStats baseStats = null)
        {
            var character = new CharacterState
            {
                characterId = id,
                name = name,
                profession = profession,
                stats = baseStats ?? GenerateRandomStats(profession),
                needs = new CharacterNeeds
                {
                    food = 100f,
                    sleep = 100f,
                    safety = 100f,
                    entertainment = 100f,
                    social = 100f
                },
                health = 100,
                maxHealth = 100,
                hunger = 0f,
                stress = 0f,
                fatigue = 0f,
                traits = GenerateRandomTraits(),
                equipment = new string[0],
                currentWork = WorkType.Idle,
                alive = true
            };
            
            var list = new List<CharacterState>(gameState.characters ?? new CharacterState[0]);
            list.Add(character);
            gameState.characters = list.ToArray();
            
            OnCharacterChanged?.Invoke(id);
            return character;
        }
        
        private CharacterStats GenerateRandomStats(Profession profession)
        {
            var stats = new CharacterStats();
            
            // Base stats
            stats.agriculture = randomSystem.NextInt(1, 4);
            stats.gathering = randomSystem.NextInt(1, 4);
            stats.engineering = randomSystem.NextInt(1, 4);
            stats.research = randomSystem.NextInt(1, 4);
            stats.medical = randomSystem.NextInt(1, 4);
            stats.exploration = randomSystem.NextInt(1, 4);
            stats.combat = randomSystem.NextInt(1, 4);
            
            // Profession bonuses
            switch (profession)
            {
                case Profession.Farmer:
                    stats.agriculture += randomSystem.NextInt(2, 4);
                    break;
                case Profession.Miner:
                    stats.gathering += randomSystem.NextInt(2, 4);
                    break;
                case Profession.Engineer:
                    stats.engineering += randomSystem.NextInt(2, 4);
                    break;
                case Profession.Researcher:
                    stats.research += randomSystem.NextInt(2, 4);
                    break;
                case Profession.Doctor:
                    stats.medical += randomSystem.NextInt(2, 4);
                    break;
                case Profession.Hunter:
                    stats.exploration += randomSystem.NextInt(2, 4);
                    stats.combat += randomSystem.NextInt(1, 3);
                    break;
                case Profession.Soldier:
                    stats.combat += randomSystem.NextInt(2, 4);
                    break;
                case Profession.Merchant:
                    stats.exploration += randomSystem.NextInt(1, 3);
                    break;
            }
            
            return stats;
        }
        
        private string[] GenerateRandomTraits()
        {
            var allTraits = new[]
            {
                "diligent", "frugal", "optimistic", "pessimistic", "cowardly",
                "gluttonous", "resilient", "lazy", "engineering_genius", "night_owl",
                "adventurer", "lone_wolf", "leader", "irritable", "lucky"
            };
            
            int traitCount = randomSystem.NextInt(1, 4);
            var selected = new HashSet<string>();
            
            while (selected.Count < traitCount && selected.Count < allTraits.Length)
            {
                selected.Add(randomSystem.NextElement(allTraits));
            }
            
            return selected.ToArray();
        }
        
        public void GenerateInitialCharacters(int count = 4)
        {
            var professions = new[]
            {
                Profession.Farmer,
                Profession.Miner,
                Profession.Engineer,
                Profession.Researcher,
                Profession.Doctor,
                Profession.Hunter
            };
            
            var names = new[] { "王", "李", "张", "刘", "陈", "杨", "赵", "黄", "周", "吴" };
            var usedNames = new HashSet<string>();
            
            for (int i = 0; i < count; i++)
            {
                string name;
                do
                {
                    name = names[randomSystem.NextInt(names.Length)] + (i + 1);
                } while (usedNames.Contains(name));
                usedNames.Add(name);
                
                Profession prof = professions[randomSystem.NextInt(professions.Length)];
                CreateCharacter($"char_{i}", name, prof);
            }
        }
        
        public bool AssignWork(string characterId, WorkType workType, string buildingId = null)
        {
            var character = GetCharacter(characterId);
            if (character == null || !character.alive) return false;

            string oldBuildingId = character.assignedBuildingId;

            // Idle never binds a character to a building.
            string targetBuildingId = workType == WorkType.Idle ? null : buildingId;

            // No building requested for a work type: look for a workplace automatically.
            if (string.IsNullOrEmpty(targetBuildingId) && workType != WorkType.Idle)
            {
                targetBuildingId = FindSuitableBuilding(characterId, workType);
                
                // A workplace is mandatory: without one the assignment is refused and
                // the character's current work/building assignment stays untouched.
                if (string.IsNullOrEmpty(targetBuildingId)) return false;
            }

            // --- Validation phase: no state may be mutated before every check passed ---
            BuildingState targetBuilding = null;
            BuildingDefinition targetDef = null;
            if (!string.IsNullOrEmpty(targetBuildingId))
            {
                targetBuilding = buildingSystem.GetBuilding(targetBuildingId);
                if (targetBuilding == null) return false;

                targetDef = buildingSystem.GetBuildingDefinition(targetBuilding.definitionId);
                if (targetDef == null) return false;

                // The building must accept this work type.
                if (!buildingSystem.WorkTypeSupportedBy(targetBuildingId, workType)) return false;

                // A character already counted in this building must not block itself.
                int occupiedSlots = 0;
                if (targetBuilding.assignedWorkers != null)
                {
                    foreach (var workerId in targetBuilding.assignedWorkers)
                    {
                        if (workerId != characterId) occupiedSlots++;
                    }
                }

                if (occupiedSlots >= targetDef.workerSlots) return false;
            }

            bool alreadyInTargetBuilding = targetBuilding != null
                && targetBuilding.assignedWorkers != null
                && Array.IndexOf(targetBuilding.assignedWorkers, characterId) >= 0;

            // --- Commit phase: only reached when the assignment cannot fail ---
            if (oldBuildingId != targetBuildingId && !string.IsNullOrEmpty(oldBuildingId))
            {
                buildingSystem.RemoveWorker(oldBuildingId, characterId);
            }

            if (!string.IsNullOrEmpty(targetBuildingId) && !alreadyInTargetBuilding)
            {
                buildingSystem.AssignWorker(targetBuildingId, characterId);
            }

            character.currentWork = workType;
            character.assignedBuildingId = string.IsNullOrEmpty(targetBuildingId) ? null : targetBuildingId;

            OnWorkAssigned?.Invoke(characterId, workType);
            OnCharacterChanged?.Invoke(characterId);

            EventBus.Publish(new CharacterChangedEvent
            {
                characterId = characterId,
                changeType = "WorkAssigned",
                detail = $"{workType.GetDisplayName()}" + (string.IsNullOrEmpty(targetBuildingId) ? "" : $" @ {targetBuildingId}")
            });

            return true;
        }
        
        private string FindSuitableBuilding(string characterId, WorkType workType)
        {
            if (gameState.buildings == null) return null;
            
            var buildings = buildingSystem.GetAllBuildings();
            foreach (var building in buildings)
            {
                if (!building.enabled) continue;
                
                // Only buildings that accept this work type can ever be picked.
                if (!buildingSystem.WorkTypeSupportedBy(building.buildingId, workType)) continue;
                
                var def = buildingSystem.GetBuildingDefinition(building.definitionId);
                if (def == null) continue;
                
                // Check if building matches work type
                bool matches = false;
                if (def.tags != null)
                {
                    switch (workType)
                    {
                        case WorkType.Farming:
                            matches = Array.Exists(def.tags, t => t == "farming" || t == "food");
                            break;
                        case WorkType.Gathering:
                            matches = Array.Exists(def.tags, t => t == "gathering" || t == "water" || t == "industry");
                            break;
                        case WorkType.Engineering:
                            matches = Array.Exists(def.tags, t => t == "crafting" || t == "smelting" || t == "industry" || t == "power" || t == "utility");
                            break;
                        case WorkType.Researching:
                            matches = Array.Exists(def.tags, t => t == "research" || t == "technology");
                            break;
                        case WorkType.Medical:
                            matches = Array.Exists(def.tags, t => t == "medical" || t == "healing");
                            break;
                        case WorkType.Exploring:
                            matches = Array.Exists(def.tags, t => t == "exploration" || t == "scouting");
                            break;
                        case WorkType.Combat:
                            matches = Array.Exists(def.tags, t => t == "defense" || t == "combat");
                            break;
                        case WorkType.Construction:
                            matches = Array.Exists(def.tags, t => t == "construction" || t == "industry");
                            break;
                        case WorkType.Maintenance:
                            matches = Array.Exists(def.tags, t => t == "maintenance" || t == "utility");
                            break;
                    }
                }
                
                if (matches)
                {
                    int occupiedSlots = 0;
                    if (building.assignedWorkers != null)
                    {
                        foreach (var workerId in building.assignedWorkers)
                        {
                            if (workerId != characterId) occupiedSlots++;
                        }
                    }

                    if (occupiedSlots < def.workerSlots)
                    {
                        return building.buildingId;
                    }
                }
            }
            
            return null;
        }
        
        public float GetCharacterFoodConsumption(CharacterState character)
        {
            if (character == null || !character.alive) return 0f;

            float amount = 2f;
            if (HasTrait(character, "gluttonous")) amount *= 1.5f;
            if (HasTrait(character, "frugal")) amount *= 0.8f;
            return amount;
        }

        public float GetCharacterWaterConsumption(CharacterState character)
        {
            if (character == null || !character.alive) return 0f;

            float amount = 2f;
            if (HasTrait(character, "gluttonous")) amount *= 1.2f;
            if (HasTrait(character, "frugal")) amount *= 0.9f;
            return amount;
        }

        public int GetTotalFoodConsumption()
        {
            if (gameState.characters == null) return 0;

            float total = 0f;
            foreach (var character in gameState.characters)
            {
                total += GetCharacterFoodConsumption(character);
            }

            return Mathf.RoundToInt(total);
        }

        public int GetTotalWaterConsumption()
        {
            if (gameState.characters == null) return 0;

            float total = 0f;
            foreach (var character in gameState.characters)
            {
                total += GetCharacterWaterConsumption(character);
            }

            return Mathf.RoundToInt(total);
        }

        public void ProcessDailyConsumption()
        {
            if (gameState.characters == null) return;

            var aliveCharacters = gameState.characters.Where(c => c.alive).ToArray();
            if (aliveCharacters.Length == 0) return;

            // Demand and consumption share the same helpers used by the UI forecast.
            int totalFoodDemand = GetTotalFoodConsumption();
            int totalWaterDemand = GetTotalWaterConsumption();

            int currentFood = resourceSystem.GetAmount(ResourceType.Food);
            int currentWater = resourceSystem.GetAmount(ResourceType.Water);

            // Only what is actually in storage is spent.
            int actualFoodConsume = Mathf.Min(currentFood, totalFoodDemand);
            int actualWaterConsume = Mathf.Min(currentWater, totalWaterDemand);

            if (actualFoodConsume > 0)
            {
                resourceSystem.Remove(ResourceType.Food, actualFoodConsume, "DailyConsumption");
            }

            if (actualWaterConsume > 0)
            {
                resourceSystem.Remove(ResourceType.Water, actualWaterConsume, "DailyConsumption");
            }

            float foodSupplyRatio = totalFoodDemand > 0 ? (float)actualFoodConsume / totalFoodDemand : 1f;
            float waterSupplyRatio = totalWaterDemand > 0 ? (float)actualWaterConsume / totalWaterDemand : 1f;

            float foodShortfall = 1f - foodSupplyRatio;
            float waterShortfall = 1f - waterSupplyRatio;

            foreach (var character in aliveCharacters)
            {
                float foodConsumption = GetCharacterFoodConsumption(character);
                float waterConsumption = GetCharacterWaterConsumption(character);

                // Full supply slowly satisfies hunger; every missing unit feeds it instead.
                if (foodShortfall > 0f)
                {
                    character.hunger += foodConsumption * 2f * foodShortfall;
                }
                else
                {
                    character.hunger = Math.Max(0f, character.hunger - 5f);
                }

                // Work costs energy during the day.
                if (character.currentWork != WorkType.Idle)
                {
                    float fatigueGain = 10f;
                    if (HasTrait(character, "diligent")) fatigueGain *= 0.8f;
                    if (HasTrait(character, "lazy")) fatigueGain *= 1.3f;
                    character.fatigue += fatigueGain;
                }

                // Thirst and sustained hunger wear the character down.
                if (foodShortfall > 0f || waterShortfall > 0f || character.hunger > 50f)
                {
                    float stressGain = waterShortfall * 20f;
                    if (character.hunger > 50f) stressGain += (character.hunger - 50f) * 0.2f;

                    if (HasTrait(character, "resilient")) stressGain *= 0.7f;
                    if (HasTrait(character, "pessimistic")) stressGain *= 1.3f;
                    character.stress += stressGain;
                }

                character.hunger = Math.Clamp(character.hunger, 0, 100);
                character.stress = Math.Clamp(character.stress, 0, 100);
                character.fatigue = Math.Clamp(character.fatigue, 0, 100);

                OnCharacterChanged?.Invoke(character.characterId);
            }
        }
        
        public void ProcessNightRecovery()
        {
            if (gameState.characters == null) return;
            
            foreach (var character in gameState.characters)
            {
                if (!character.alive) continue;
                
                // Fatigue recovery
                character.fatigue = Math.Max(0, character.fatigue - 30f);
                
                // Stress recovery
                character.stress = Math.Max(0, character.stress - 5f);
                
                // Health recovery/damage based on hunger
                if (character.hunger < 20)
                {
                    character.health = Math.Min(character.maxHealth, character.health + 5);
                }
                else if (character.hunger > 80)
                {
                    character.health = Math.Max(0, character.health - 10);
                    if (character.health <= 0)
                    {
                        KillCharacter(character.characterId, "Starvation");
                        continue; // Character is dead, skip further processing
                    }
                }
                
                character.health = Math.Clamp(character.health, 0, character.maxHealth);
                
                OnCharacterChanged?.Invoke(character.characterId);
            }
        }
        
        private bool HasTrait(CharacterState character, string traitId)
        {
            if (character.traits == null) return false;
            return Array.Exists(character.traits, t => t == traitId);
        }
        
        public void KillCharacter(string characterId, string cause)
        {
            var character = GetCharacter(characterId);
            if (character == null || !character.alive) return;

            // 1) read the binding, 2) detach from the building, 3) only then clear the state.
            string oldBuildingId = character.assignedBuildingId;

            if (!string.IsNullOrEmpty(oldBuildingId))
            {
                buildingSystem.RemoveWorker(oldBuildingId, characterId);
            }

            character.assignedBuildingId = null;
            character.currentWork = WorkType.Idle;
            character.alive = false;
            character.dayOfDeath = gameState.currentDay;
            character.deathCause = cause;

            OnCharacterDied?.Invoke(characterId);
            OnCharacterChanged?.Invoke(characterId);

            EventBus.Publish(new CharacterDiedEvent
            {
                characterId = characterId,
                cause = cause
            });
        }
        
        public float GetWorkEfficiency(CharacterState character)
        {
            if (character == null || !character.alive) return 0f;
            
            float efficiency = 1f;
            
            StatType primaryStat = character.currentWork.GetPrimaryStat();
            efficiency *= character.stats.GetWorkEfficiency(primaryStat);
            
            efficiency *= (float)character.health / character.maxHealth;
            
            if (character.hunger > 50) efficiency *= 1f - (character.hunger - 50) * 0.005f;
            if (character.stress > 50) efficiency *= 1f - (character.stress - 50) * 0.005f;
            if (character.fatigue > 50) efficiency *= 1f - (character.fatigue - 50) * 0.005f;
            
            if (HasTrait(character, "diligent")) efficiency *= 1.1f;
            if (HasTrait(character, "lazy")) efficiency *= 0.8f;
            if (HasTrait(character, "engineering_genius") && 
                (character.currentWork == WorkType.Engineering || character.currentWork == WorkType.Construction))
                efficiency *= 1.3f;
            
            return Math.Max(0.1f, efficiency);
        }
    }
}