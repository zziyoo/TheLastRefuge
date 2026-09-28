using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LastRefuge.Data;
using LastRefuge.Core;

namespace LastRefuge.Systems
{
    public class BuildingSystem : IBuildingSystem
    {
        // Storage granted per building level, shared by build/upgrade/load so capacity
        // is always derived, never accumulated.
        private const int StorageCapacityPerLevel = 100;

        private GameState gameState;
        private IResourceSystem resourceSystem;
        private ICharacterSystem characterSystem;
        private Dictionary<string, BuildingDefinition> buildingDefinitions = new Dictionary<string, BuildingDefinition>();
        
        public event Action<string> OnBuildingChanged;
        public event Action<string> OnBuildingConstructed;
        public event Action<string> OnBuildingDestroyed;
        
        public void Initialize(GameState state, IResourceSystem resSys, ICharacterSystem charSys)
        {
            gameState = state;
            resourceSystem = resSys;
            characterSystem = charSys;
            
            LoadBuildingDefinitions();
            RecalculateAllBuildingEffects();
        }
        
        private void LoadBuildingDefinitions()
        {
            CreateDefaultDefinitions();
        }
        
        private void CreateDefaultDefinitions()
        {
            buildingDefinitions["shelter_temp"] = new BuildingDefinition
            {
                id = "shelter_temp",
                name = "临时住所",
                category = BuildingCategory.Housing,
                maxLevel = 1,
                constructionCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Wood, amount = 20 },
                    new ResourceCost { type = ResourceType.Stone, amount = 10 }
                },
                upkeepCost = new ResourceCost[0],
                housingCapacity = 4,
                workerSlots = 0,
                maxCount = 3,
                tags = new[] { "housing", "starter" },
                description = "简易的临时住所，可容纳4人"
            };
            
            buildingDefinitions["farm_basic"] = new BuildingDefinition
            {
                id = "farm_basic",
                name = "农田",
                category = BuildingCategory.Production,
                maxLevel = 3,
                constructionCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Wood, amount = 30 },
                    new ResourceCost { type = ResourceType.Stone, amount = 10 }
                },
                upkeepCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Water, amount = 5 }
                },
                production = new ResourceProduction[]
                {
                    new ResourceProduction { type = ResourceType.Food, baseAmount = 10, primaryStat = StatType.Agriculture }
                },
                workerSlots = 2,
                maxCount = 0,
                tags = new[] { "farming", "food" },
                description = "基础农田，生产食物"
            };
            
            buildingDefinitions["water_purifier"] = new BuildingDefinition
            {
                id = "water_purifier",
                name = "净水器",
                category = BuildingCategory.Production,
                maxLevel = 3,
                constructionCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Wood, amount = 20 },
                    new ResourceCost { type = ResourceType.Stone, amount = 20 },
                    new ResourceCost { type = ResourceType.Metal, amount = 10 }
                },
                upkeepCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Power, amount = 5 }
                },
                production = new ResourceProduction[]
                {
                    new ResourceProduction { type = ResourceType.Water, baseAmount = 15, primaryStat = StatType.Engineering }
                },
                workerSlots = 1,
                maxCount = 0,
                powerConsumption = 5,
                tags = new[] { "water", "utility" },
                description = "净化水源，生产洁净水"
            };
            
            buildingDefinitions["warehouse"] = new BuildingDefinition
            {
                id = "warehouse",
                name = "仓库",
                category = BuildingCategory.Storage,
                maxLevel = 3,
                constructionCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Wood, amount = 40 },
                    new ResourceCost { type = ResourceType.Stone, amount = 20 }
                },
                upkeepCost = new ResourceCost[0],
                storageCapacity = new ResourceType[]
                {
                    ResourceType.Food, ResourceType.Water, ResourceType.Wood,
                    ResourceType.Stone, ResourceType.Iron, ResourceType.Metal,
                    ResourceType.Fuel, ResourceType.Parts, ResourceType.Medicine
                },
                workerSlots = 0,
                maxCount = 2,
                tags = new[] { "storage" },
                description = "增加资源存储容量"
            };
            
            buildingDefinitions["workbench"] = new BuildingDefinition
            {
                id = "workbench",
                name = "工作台",
                category = BuildingCategory.Production,
                maxLevel = 3,
                constructionCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Wood, amount = 30 },
                    new ResourceCost { type = ResourceType.Metal, amount = 20 },
                    new ResourceCost { type = ResourceType.Parts, amount = 10 }
                },
                upkeepCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Power, amount = 10 }
                },
                production = new ResourceProduction[]
                {
                    new ResourceProduction { type = ResourceType.Parts, baseAmount = 5, primaryStat = StatType.Engineering }
                },
                workerSlots = 1,
                maxCount = 0,
                powerConsumption = 10,
                tags = new[] { "crafting", "industry" },
                description = "基础制造设施，生产零件"
            };
            
            buildingDefinitions["furnace"] = new BuildingDefinition
            {
                id = "furnace",
                name = "熔炉",
                category = BuildingCategory.Production,
                maxLevel = 3,
                constructionCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Stone, amount = 50 },
                    new ResourceCost { type = ResourceType.Iron, amount = 30 },
                    new ResourceCost { type = ResourceType.Fuel, amount = 20 }
                },
                upkeepCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Fuel, amount = 10 },
                    new ResourceCost { type = ResourceType.Power, amount = 15 }
                },
                production = new ResourceProduction[]
                {
                    new ResourceProduction { type = ResourceType.Metal, baseAmount = 8, primaryStat = StatType.Engineering }
                },
                workerSlots = 1,
                maxCount = 0,
                powerConsumption = 15,
                tags = new[] { "smelting", "industry" },
                description = "冶炼铁矿为金属"
            };
            
            buildingDefinitions["generator"] = new BuildingDefinition
            {
                id = "generator",
                name = "发电机",
                category = BuildingCategory.Utility,
                maxLevel = 3,
                constructionCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Metal, amount = 30 },
                    new ResourceCost { type = ResourceType.Parts, amount = 20 },
                    new ResourceCost { type = ResourceType.Fuel, amount = 20 }
                },
                upkeepCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Fuel, amount = 15 }
                },
                production = new ResourceProduction[]
                {
                    new ResourceProduction { type = ResourceType.Power, baseAmount = 50, primaryStat = StatType.Engineering }
                },
                workerSlots = 0,
                maxCount = 0,
                powerProduction = 50,
                tags = new[] { "power", "utility" },
                description = "燃油发电机，提供电力"
            };
            
            buildingDefinitions["medical_bay"] = new BuildingDefinition
            {
                id = "medical_bay",
                name = "医疗室",
                category = BuildingCategory.Medical,
                maxLevel = 3,
                constructionCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Wood, amount = 30 },
                    new ResourceCost { type = ResourceType.Metal, amount = 20 },
                    new ResourceCost { type = ResourceType.Medicine, amount = 10 }
                },
                upkeepCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Medicine, amount = 2 },
                    new ResourceCost { type = ResourceType.Power, amount = 10 }
                },
                production = new ResourceProduction[0],
                workerSlots = 1,
                maxCount = 1,
                powerConsumption = 10,
                tags = new[] { "medical", "healing" },
                description = "治疗受伤人员，生产药品"
            };
            
            buildingDefinitions["research_lab"] = new BuildingDefinition
            {
                id = "research_lab",
                name = "研究室",
                category = BuildingCategory.Research,
                maxLevel = 3,
                constructionCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Metal, amount = 40 },
                    new ResourceCost { type = ResourceType.Parts, amount = 20 },
                    new ResourceCost { type = ResourceType.Power, amount = 20 }
                },
                upkeepCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Power, amount = 20 }
                },
                production = new ResourceProduction[0],
                workerSlots = 2,
                maxCount = 2,
                powerConsumption = 20,
                tags = new[] { "research", "technology" },
                description = "进行科技研究"
            };
            
            buildingDefinitions["scout_station"] = new BuildingDefinition
            {
                id = "scout_station",
                name = "侦察站",
                category = BuildingCategory.Special,
                maxLevel = 3,
                constructionCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Wood, amount = 30 },
                    new ResourceCost { type = ResourceType.Metal, amount = 15 },
                    new ResourceCost { type = ResourceType.Parts, amount = 10 }
                },
                upkeepCost = new ResourceCost[]
                {
                    new ResourceCost { type = ResourceType.Food, amount = 5 }
                },
                production = new ResourceProduction[0],
                workerSlots = 1,
                maxCount = 1,
                tags = new[] { "exploration", "scouting" },
                description = "派遣探索队，发现新地点"
            };
        }
        
        public IEnumerable<BuildingDefinition> GetAvailableBuildings()
        {
            return buildingDefinitions.Values.Where(d => 
            {
                if (d.maxCount <= 0) return true; // unlimited
                int currentCount = GetBuildingsByDefinition(d.id).Length;
                return currentCount < d.maxCount;
            });
        }
        
        public bool CanUpgrade(string buildingId)
        {
            var building = GetBuilding(buildingId);
            if (building == null) return false;
            
            var def = GetBuildingDefinition(building.definitionId);
            if (def == null || building.level >= def.maxLevel) return false;
            
            foreach (var cost in def.constructionCost)
            {
                int upgradeCost = cost.amount * building.level / 2;
                if (!resourceSystem.CanAfford(cost.type, upgradeCost)) return false;
            }
            
            return true;
        }
        
        public BuildingDefinition GetBuildingDefinition(string id)
        {
            buildingDefinitions.TryGetValue(id, out var def);
            return def;
        }
        
        public BuildingState[] GetAllBuildings()
        {
            return gameState.buildings ?? new BuildingState[0];
        }
        
        public BuildingState GetBuilding(string buildingId)
        {
            if (gameState.buildings == null) return null;
            return gameState.buildings.FirstOrDefault(b => b.buildingId == buildingId);
        }
        
        public BuildingState[] GetBuildingsByDefinition(string definitionId)
        {
            if (gameState.buildings == null) return new BuildingState[0];
            return gameState.buildings.Where(b => b.definitionId == definitionId).ToArray();
        }
        
        public bool CanBuild(string definitionId)
        {
            var def = GetBuildingDefinition(definitionId);
            if (def == null) return false;
            
            if (!CanBuildCount(definitionId)) return false;
            
            if (def.prerequisites != null)
            {
                foreach (var prereq in def.prerequisites)
                {
                    if (!HasBuilding(prereq)) return false;
                }
            }
            
            foreach (var cost in def.constructionCost)
            {
                if (!resourceSystem.CanAfford(cost.type, cost.amount))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// maxCount is enforced here, so the rule also holds for direct Build() calls
        /// and does not depend on the UI hiding the button.
        /// </summary>
        public bool CanBuildCount(string definitionId)
        {
            var def = GetBuildingDefinition(definitionId);
            if (def == null) return false;
            if (def.maxCount <= 0) return true;
            
            return GetBuildingsByDefinition(definitionId).Length < def.maxCount;
        }
        
        public bool HasBuilding(string definitionId)
        {
            return GetBuildingsByDefinition(definitionId).Length > 0;
        }
        
        public BuildingState Build(string definitionId)
        {
            var def = GetBuildingDefinition(definitionId);
            if (def == null) return null;
            
            // Count limit is re-checked here: Build must never rely on CanBuild alone.
            if (!CanBuildCount(definitionId)) return null;
            
            if (!CanBuild(definitionId)) return null;
            
            foreach (var cost in def.constructionCost)
            {
                resourceSystem.Remove(cost.type, cost.amount, $"Build_{definitionId}");
            }
            
            string buildingId = $"{definitionId}_{gameState.currentDay}_{GetAllBuildings().Length}";
            
            var building = new BuildingState
            {
                buildingId = buildingId,
                definitionId = definitionId,
                level = 1,
                durability = 100,
                enabled = true,
                assignedWorkers = new string[0],
                currentProduction = 0,
                dayBuilt = gameState.currentDay
            };
            
            var list = new List<BuildingState>(gameState.buildings ?? new BuildingState[0]);
            list.Add(building);
            gameState.buildings = list.ToArray();
            
            RecalculateAllBuildingEffects();
            
            OnBuildingConstructed?.Invoke(buildingId);
            OnBuildingChanged?.Invoke(buildingId);
            
            EventBus.Publish(new BuildingConstructedEvent
            {
                buildingId = buildingId,
                definitionId = definitionId
            });
            
            return building;
        }
        
        public bool UpgradeBuilding(string buildingId)
        {
            var building = GetBuilding(buildingId);
            if (building == null) return false;
            
            var def = GetBuildingDefinition(building.definitionId);
            if (def == null || building.level >= def.maxLevel) return false;
            
            foreach (var cost in def.constructionCost)
            {
                int upgradeCost = cost.amount * building.level / 2;
                if (!resourceSystem.CanAfford(cost.type, upgradeCost)) return false;
            }
            
            foreach (var cost in def.constructionCost)
            {
                int upgradeCost = cost.amount * building.level / 2;
                resourceSystem.Remove(cost.type, upgradeCost, $"Upgrade_{buildingId}");
            }
            
            building.level++;
            building.durability = 100;
            
            // Capacity is recomputed from every current building level, never added up.
            RecalculateAllBuildingEffects();
            
            OnBuildingChanged?.Invoke(buildingId);
            return true;
        }
        
        /// <summary>
        /// Recomputes every derived value from the current building levels.
        /// Called after build, upgrade and load so capacities can never accumulate twice.
        /// </summary>
        public void RecalculateAllBuildingEffects()
        {
            var bonus = new Dictionary<ResourceType, int>();
            
            foreach (var building in GetAllBuildings())
            {
                var def = GetBuildingDefinition(building.definitionId);
                if (def == null || def.storageCapacity == null) continue;
                
                foreach (var resType in def.storageCapacity)
                {
                    int contribution = StorageCapacityPerLevel * Math.Max(1, building.level);
                    if (!bonus.TryGetValue(resType, out int current))
                    {
                        current = 0;
                    }
                    bonus[resType] = current + contribution;
                }
            }
            
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                int extra = bonus.TryGetValue(type, out int value) ? value : 0;
                resourceSystem.SetCapacity(type, resourceSystem.GetBaseCapacity(type) + extra);
            }
        }
        
        public bool AssignWorker(string buildingId, string characterId)
        {
            var building = GetBuilding(buildingId);
            if (building == null) return false;
            
            var def = GetBuildingDefinition(building.definitionId);
            if (def == null) return false;
            
            var workers = new List<string>(building.assignedWorkers ?? new string[0]);
            if (workers.Contains(characterId)) return true;
            
            if (workers.Count >= def.workerSlots) return false;
            
            workers.Add(characterId);
            building.assignedWorkers = workers.ToArray();
            
            OnBuildingChanged?.Invoke(buildingId);
            return true;
        }
        
        public bool RemoveWorker(string buildingId, string characterId)
        {
            var building = GetBuilding(buildingId);
            if (building == null) return false;
            
            var workers = new List<string>(building.assignedWorkers ?? new string[0]);
            if (!workers.Contains(characterId)) return true;
            
            workers.Remove(characterId);
            building.assignedWorkers = workers.ToArray();
            
            OnBuildingChanged?.Invoke(buildingId);
            return true;
        }
        
        /// <summary>
        /// The real cost of running the building for one cycle.
        /// upkeepCost covers maintenance materials (Fuel, Parts...), powerConsumption covers
        /// running power. When both list Power the stronger of the two is used so electricity
        /// is never paid twice.
        /// </summary>
        public Dictionary<ResourceType, int> CalculateOperationCost(BuildingState building)
        {
            var cost = new Dictionary<ResourceType, int>();
            if (building == null) return cost;
            
            var def = GetBuildingDefinition(building.definitionId);
            if (def == null) return cost;
            
            if (def.upkeepCost != null)
            {
                foreach (var upkeep in def.upkeepCost)
                {
                    if (cost.TryGetValue(upkeep.type, out int current))
                    {
                        // Power: keep the requirement only once.
                        if (upkeep.type == ResourceType.Power)
                        {
                            cost[upkeep.type] = Math.Max(current, upkeep.amount);
                        }
                        else
                        {
                            cost[upkeep.type] = current + upkeep.amount;
                        }
                    }
                    else
                    {
                        cost[upkeep.type] = upkeep.amount;
                    }
                }
            }
            
            if (def.powerConsumption > 0)
            {
                if (cost.TryGetValue(ResourceType.Power, out int powerCost))
                {
                    cost[ResourceType.Power] = Math.Max(powerCost, def.powerConsumption);
                }
                else
                {
                    cost[ResourceType.Power] = def.powerConsumption;
                }
            }
            
            return cost;
        }
        
        /// <summary>
        /// Step 1 of the production cycle: enabled, durable and able to pay.
        /// </summary>
        public bool CanOperate(BuildingState building)
        {
            if (building == null) return false;
            if (!building.enabled) return false;
            if (building.durability <= 0) return false;
            
            var cost = CalculateOperationCost(building);
            return resourceSystem.CanAfford(cost);
        }
        
        /// <summary>
        /// The single production formula. Used by the daily settlement and by the UI forecast,
        /// so predicted and actual output can never drift apart.
        /// </summary>
        public Dictionary<ResourceType, int> CalculateProduction(BuildingState building)
        {
            var result = new Dictionary<ResourceType, int>();
            if (building == null) return result;
            
            var def = GetBuildingDefinition(building.definitionId);
            if (def == null || def.production == null) return result;
            
            // A building that cannot pay its cost produces nothing this cycle.
            if (!CanOperate(building)) return result;
            
            float workerEfficiency = 0f;
            if (building.assignedWorkers != null)
            {
                foreach (var workerId in building.assignedWorkers)
                {
                    var character = characterSystem.GetCharacter(workerId);
                    if (character != null && character.alive)
                    {
                        workerEfficiency += characterSystem.GetWorkEfficiency(character);
                    }
                }
            }
            
            foreach (var prod in def.production)
            {
                // Power is generated automatically, it does not need staffed workers.
                float amount = prod.type == ResourceType.Power
                    ? prod.baseAmount * prod.efficiencyMultiplier
                    : prod.baseAmount * workerEfficiency * prod.efficiencyMultiplier;
                
                int finalAmount = Mathf.RoundToInt(amount);
                if (finalAmount <= 0) continue;
                
                if (result.TryGetValue(prod.type, out int existing))
                {
                    result[prod.type] = existing + finalAmount;
                }
                else
                {
                    result[prod.type] = finalAmount;
                }
            }
            
            return result;
        }
        
        public int GetBuildingDailyProduction(BuildingState building, ResourceType type)
        {
            var production = CalculateProduction(building);
            return production.TryGetValue(type, out int amount) ? amount : 0;
        }
        
        public void ProcessBuildingProduction()
        {
            if (gameState.buildings == null) return;
            
            foreach (var building in gameState.buildings)
            {
                if (!building.enabled || building.durability <= 0)
                {
                    building.currentProduction = 0;
                    continue;
                }
                
                var def = GetBuildingDefinition(building.definitionId);
                if (def == null) continue;
                
                // CheckOperation -> ConsumeCosts -> Produce. Nothing is produced unless the
                // whole cost was verified and paid first.
                var cost = CalculateOperationCost(building);
                if (!resourceSystem.CanAfford(cost))
                {
                    building.currentProduction = 0;
                    building.durability = Math.Max(0, building.durability - 1);
                    continue;
                }
                
                foreach (var kvp in cost)
                {
                    resourceSystem.Remove(kvp.Key, kvp.Value, $"Operation_{building.buildingId}");
                }
                
                var production = CalculateProduction(building);
                
                int totalProduced = 0;
                foreach (var kvp in production)
                {
                    resourceSystem.Add(kvp.Key, kvp.Value, $"Building_{building.buildingId}");
                    totalProduced += kvp.Value;
                }
                
                building.currentProduction = totalProduced;
                
                building.durability = Math.Max(0, building.durability - 1);
                if (building.durability <= 0)
                {
                    building.enabled = false;
                }
            }
        }
        
        public int GetTotalHousingCapacity()
        {
            int capacity = 0;
            if (gameState.buildings != null)
            {
                foreach (var building in gameState.buildings)
                {
                    if (building.enabled)
                    {
                        var def = GetBuildingDefinition(building.definitionId);
                        if (def != null)
                        {
                            capacity += def.housingCapacity * building.level;
                        }
                    }
                }
            }
            return capacity;
        }
        
        public int GetCurrentPopulation()
        {
            if (gameState.characters == null) return 0;
            return gameState.characters.Count(c => c.alive);
        }
        
        public bool IsOvercrowded()
        {
            return GetCurrentPopulation() > GetTotalHousingCapacity();
        }
    }
}