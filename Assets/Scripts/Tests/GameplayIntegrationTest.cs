using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using LastRefuge.Core;
using LastRefuge.Data;
using LastRefuge.Systems;
using LastRefuge.Save;

namespace LastRefuge.Tests
{
    /// <summary>
    /// Cross system coverage for the Phase 2 core loop:
    /// character -> building -> production -> consumption -> time -> save/load.
    /// </summary>
    public class GameplayIntegrationTest
    {
        private GameState gameState;
        private TimeSystem timeSystem;
        private RandomSystem randomSystem;
        private ResourceSystem resourceSystem;
        private CharacterSystem characterSystem;
        private BuildingSystem buildingSystem;
        private SaveSystem saveSystem;
        
        [SetUp]
        public void Setup()
        {
            gameState = new GameState();
            timeSystem = new TimeSystem();
            randomSystem = new RandomSystem();
            resourceSystem = new ResourceSystem();
            characterSystem = new CharacterSystem();
            buildingSystem = new BuildingSystem();
            saveSystem = new SaveSystem();
            
            timeSystem.Initialize(gameState, randomSystem);
            resourceSystem.Initialize(gameState, buildingSystem, characterSystem);
            characterSystem.Initialize(gameState, resourceSystem, randomSystem, buildingSystem);
            buildingSystem.Initialize(gameState, resourceSystem, characterSystem);
            saveSystem.Initialize(gameState, "0.1.0");
            
            randomSystem.Initialize("integration_seed");
        }
        
        private CharacterState CreateWorker(string id, WorkType work, StatType stat, int statValue)
        {
            var character = characterSystem.CreateCharacter(id, id, Profession.Farmer);
            character.traits = new string[0];
            character.health = character.maxHealth;
            character.hunger = 0f;
            character.stress = 0f;
            character.fatigue = 0f;
            character.currentWork = work;
            character.stats = new CharacterStats();
            character.stats.SetStat(stat, statValue);
            return character;
        }
        
        /// <summary>
        /// Reproduces the GameManager day order: Action (production + consumption),
        /// Evening, Night (recovery), DayEnd.
        /// </summary>
        private void ProcessTimeSlot(TimeSlot slot)
        {
            ProcessSlot(timeSystem, buildingSystem, characterSystem, slot);
        }
        
        private static void ProcessSlot(TimeSystem time, BuildingSystem buildings, CharacterSystem characters, TimeSlot slot)
        {
            switch (slot)
            {
                case TimeSlot.Action:
                    buildings.ProcessBuildingProduction();
                    characters.ProcessDailyConsumption();
                    break;
                case TimeSlot.Night:
                    characters.ProcessNightRecovery();
                    break;
            }
            
            time.AdvanceTimeSlot();
        }
        
        [Test]
        public void FullDayFlow_BuildAssignProduceConsumeRecoverAndAdvance()
        {
            characterSystem.GenerateInitialCharacters(4);
            Assert.AreEqual(4, characterSystem.GetAliveCharacters().Length);
            
            resourceSystem.Add(ResourceType.Wood, 100, "Setup");
            resourceSystem.Add(ResourceType.Stone, 100, "Setup");
            resourceSystem.Add(ResourceType.Food, 30, "Setup");
            resourceSystem.Add(ResourceType.Water, 30, "Setup");
            
            var farm = buildingSystem.Build("farm_basic");
            Assert.IsNotNull(farm, "Farm should be buildable with enough resources");
            
            // Deterministic worker: agriculture 5 -> efficiency 1.0, no traits.
            var characters = characterSystem.GetAliveCharacters();
            foreach (var character in characters)
            {
                character.traits = new string[0];
                character.hunger = 0f;
                character.stress = 0f;
                character.fatigue = 0f;
                character.health = character.maxHealth;
                character.stats = new CharacterStats();
            }
            characters[0].stats.SetStat(StatType.Agriculture, 5);
            
            bool assigned = characterSystem.AssignWork(characters[0].characterId, WorkType.Farming, farm.buildingId);
            Assert.IsTrue(assigned);
            Assert.AreEqual(farm.buildingId, characters[0].assignedBuildingId);
            Assert.AreEqual(1, farm.assignedWorkers.Length, "Farm must list the assigned worker");
            Assert.AreEqual(characters[0].characterId, farm.assignedWorkers[0]);
            
            // Action: 10 food produced, 8 food eaten by 4 characters.
            int foodBefore = resourceSystem.GetAmount(ResourceType.Food);
            int waterBefore = resourceSystem.GetAmount(ResourceType.Water);
            timeSystem.SetTime(1, TimeSlot.Action);
            ProcessTimeSlot(TimeSlot.Action);
            
            Assert.AreEqual(foodBefore + 2, resourceSystem.GetAmount(ResourceType.Food), "Food must grow by the farm output net of consumption");
            Assert.AreEqual(waterBefore - 5 - 8, resourceSystem.GetAmount(ResourceType.Water), "Farm upkeep and daily drinking must be deducted");
            Assert.AreEqual(TimeSlot.Evening, timeSystem.CurrentTimeSlot);
            
            // Night: a working character is tired, the night recovers it.
            ProcessTimeSlot(TimeSlot.Evening);
            Assert.AreEqual(TimeSlot.Night, timeSystem.CurrentTimeSlot);
            
            characters[0].fatigue = 40f;
            float fatigueBeforeNight = characters[0].fatigue;
            ProcessTimeSlot(TimeSlot.Night);
            Assert.Less(characters[0].fatigue, fatigueBeforeNight, "Night must recover fatigue");
            Assert.AreEqual(TimeSlot.DayEnd, timeSystem.CurrentTimeSlot);
            
            ProcessTimeSlot(TimeSlot.DayEnd);
            Assert.AreEqual(TimeSlot.Morning, timeSystem.CurrentTimeSlot);
            Assert.AreEqual(2, gameState.currentDay, "DayEnd must roll over into the next day");
        }
        
        [Test]
        public void FoodShortage_DrainsStockAndCausesHunger()
        {
            var character = CreateWorker("hungry", WorkType.Idle, StatType.Agriculture, 1);
            resourceSystem.Add(ResourceType.Food, 1, "Setup");
            resourceSystem.Add(ResourceType.Water, 50, "Setup");
            
            Assert.AreEqual(2, characterSystem.GetTotalFoodConsumption(), "A character needs 2 food per day");
            
            float hungerBefore = character.hunger;
            ProcessTimeSlot(TimeSlot.Action);
            
            Assert.AreEqual(0, resourceSystem.GetAmount(ResourceType.Food), "Available food must be consumed even when it cannot cover the demand");
            Assert.Greater(character.hunger, hungerBefore, "Starving characters must become hungry");
        }
        
        [Test]
        public void FullFoodSupply_KeepsHungerFlat()
        {
            var character = CreateWorker("fed", WorkType.Idle, StatType.Agriculture, 1);
            resourceSystem.Add(ResourceType.Food, 50, "Setup");
            resourceSystem.Add(ResourceType.Water, 50, "Setup");
            
            float hungerBefore = character.hunger;
            ProcessTimeSlot(TimeSlot.Action);
            
            Assert.AreEqual(48, resourceSystem.GetAmount(ResourceType.Food));
            Assert.AreEqual(hungerBefore, character.hunger, 0.001f, "A fed colony must not gain hunger");
        }
        
        [Test]
        public void BuildingWithoutUpkeep_StopsProducing()
        {
            resourceSystem.Add(ResourceType.Metal, 100, "Setup");
            resourceSystem.Add(ResourceType.Parts, 100, "Setup");
            resourceSystem.Add(ResourceType.Fuel, 100, "Setup");
            
            var generator = buildingSystem.Build("generator");
            Assert.IsNotNull(generator);
            
            int powerBefore = resourceSystem.GetAmount(ResourceType.Power);
            ProcessTimeSlot(TimeSlot.Action);
            Assert.AreEqual(powerBefore + 50, resourceSystem.GetAmount(ResourceType.Power), "A fueled generator produces power");
            Assert.AreEqual(100 - 20 - 15, resourceSystem.GetAmount(ResourceType.Fuel), "20 fuel for construction plus 15 upkeep are paid exactly once");
            
            // Out of fuel: the generator must not produce and must not consume.
            resourceSystem.Remove(ResourceType.Fuel, resourceSystem.GetAmount(ResourceType.Fuel), "Setup");
            Assert.AreEqual(0, resourceSystem.GetAmount(ResourceType.Fuel));
            Assert.AreEqual(15, buildingSystem.CalculateOperationCost(generator)[ResourceType.Fuel], "Fuel requirement is reported");
            Assert.IsFalse(buildingSystem.CanOperate(generator), "A generator without fuel cannot operate");
            
            powerBefore = resourceSystem.GetAmount(ResourceType.Power);
            ProcessTimeSlot(TimeSlot.Action);
            
            Assert.AreEqual(powerBefore, resourceSystem.GetAmount(ResourceType.Power), "A generator without fuel must not add power");
            Assert.AreEqual(0, generator.currentProduction);
        }
        
        [Test]
        public void PowerIsNotChargedTwice()
        {
            resourceSystem.Add(ResourceType.Wood, 100, "Setup");
            resourceSystem.Add(ResourceType.Stone, 100, "Setup");
            resourceSystem.Add(ResourceType.Metal, 100, "Setup");
            resourceSystem.Add(ResourceType.Parts, 100, "Setup");
            resourceSystem.Add(ResourceType.Power, 100, "Setup");
            
            var purifier = buildingSystem.Build("water_purifier");
            Assert.IsNotNull(purifier);
            
            var cost = buildingSystem.CalculateOperationCost(purifier);
            Assert.AreEqual(5, cost[ResourceType.Power], "Power must be charged once, not upkeep plus powerConsumption");
            
            int powerBefore = resourceSystem.GetAmount(ResourceType.Power);
            ProcessTimeSlot(TimeSlot.Action);
            Assert.AreEqual(powerBefore - 5, resourceSystem.GetAmount(ResourceType.Power));
        }
        
        [Test]
        public void UpgradeWarehouse_RecalculatesCapacityWithoutDoubleCount()
        {
            resourceSystem.Add(ResourceType.Wood, 200, "Setup");
            resourceSystem.Add(ResourceType.Stone, 200, "Setup");
            
            int baseCapacity = resourceSystem.GetBaseCapacity(ResourceType.Food);
            
            var warehouse = buildingSystem.Build("warehouse");
            Assert.IsNotNull(warehouse);
            Assert.AreEqual(baseCapacity + 100, resourceSystem.GetCapacity(ResourceType.Food), "Level 1 warehouse adds 100 storage");
            
            Assert.IsTrue(buildingSystem.UpgradeBuilding(warehouse.buildingId));
            Assert.AreEqual(2, warehouse.level);
            Assert.AreEqual(baseCapacity + 200, resourceSystem.GetCapacity(ResourceType.Food), "Level 2 warehouse total is 200, not 100+200");
            
            Assert.IsTrue(buildingSystem.UpgradeBuilding(warehouse.buildingId));
            Assert.AreEqual(3, warehouse.level);
            Assert.AreEqual(baseCapacity + 300, resourceSystem.GetCapacity(ResourceType.Food), "Level 3 warehouse total is 300");
            
            // Recalculating must be idempotent, not cumulative.
            buildingSystem.RecalculateAllBuildingEffects();
            Assert.AreEqual(baseCapacity + 300, resourceSystem.GetCapacity(ResourceType.Food));
        }
        
        [Test]
        public void BuildCountLimit_IsEnforcedByTheSystem()
        {
            resourceSystem.Add(ResourceType.Wood, 500, "Setup");
            resourceSystem.Add(ResourceType.Stone, 500, "Setup");
            resourceSystem.Add(ResourceType.Metal, 500, "Setup");
            resourceSystem.Add(ResourceType.Medicine, 500, "Setup");
            
            Assert.IsNotNull(buildingSystem.Build("medical_bay"));
            Assert.AreEqual(1, buildingSystem.GetBuildingsByDefinition("medical_bay").Length);
            
            Assert.IsFalse(buildingSystem.CanBuild("medical_bay"), "Count limit must block further building");
            Assert.IsNull(buildingSystem.Build("medical_bay"), "Build must fail even when called directly");
            Assert.AreEqual(1, buildingSystem.GetBuildingsByDefinition("medical_bay").Length);
        }
        
        [Test]
        public void AssignWork_IsAtomicWhenTargetIsFull()
        {
            resourceSystem.Add(ResourceType.Wood, 200, "Setup");
            resourceSystem.Add(ResourceType.Stone, 200, "Setup");
            
            var farm = buildingSystem.Build("farm_basic");
            var workers = new List<CharacterState>();
            for (int i = 0; i < 3; i++)
            {
                workers.Add(CreateWorker("worker_" + i, WorkType.Idle, StatType.Agriculture, 5));
            }
            
            Assert.IsTrue(characterSystem.AssignWork(workers[0].characterId, WorkType.Farming, farm.buildingId));
            Assert.IsTrue(characterSystem.AssignWork(workers[1].characterId, WorkType.Farming, farm.buildingId));
            Assert.AreEqual(2, farm.assignedWorkers.Length, "Farm has only 2 slots");
            
            // Third worker is refused and must keep his previous state.
            Assert.IsFalse(characterSystem.AssignWork(workers[2].characterId, WorkType.Farming, farm.buildingId));
            Assert.AreEqual(WorkType.Idle, workers[2].currentWork);
            Assert.IsNull(workers[2].assignedBuildingId);
            Assert.AreEqual(2, farm.assignedWorkers.Length);
            
            // Switching a worker out of the farm keeps the farm valid.
            Assert.IsTrue(characterSystem.AssignWork(workers[0].characterId, WorkType.Idle));
            Assert.AreEqual(1, farm.assignedWorkers.Length);
            Assert.IsNull(workers[0].assignedBuildingId);
        }
        
        [Test]
        public void CharacterDeath_ReleasesTheWorkplace()
        {
            resourceSystem.Add(ResourceType.Wood, 200, "Setup");
            resourceSystem.Add(ResourceType.Stone, 200, "Setup");
            
            var farm = buildingSystem.Build("farm_basic");
            var worker = CreateWorker("doomed", WorkType.Farming, StatType.Agriculture, 5);
            Assert.IsTrue(characterSystem.AssignWork(worker.characterId, WorkType.Farming, farm.buildingId));
            Assert.AreEqual(1, farm.assignedWorkers.Length);
            
            characterSystem.KillCharacter(worker.characterId, "Test");
            
            Assert.IsFalse(worker.alive);
            Assert.IsNull(worker.assignedBuildingId);
            Assert.AreEqual(WorkType.Idle, worker.currentWork);
            Assert.AreEqual(0, farm.assignedWorkers.Length, "A dead character must release his workplace");
        }
        
        [Test]
        public void ForecastMatchesTheActualDailySettlement()
        {
            resourceSystem.Add(ResourceType.Wood, 200, "Setup");
            resourceSystem.Add(ResourceType.Stone, 200, "Setup");
            resourceSystem.Add(ResourceType.Food, 60, "Setup");
            resourceSystem.Add(ResourceType.Water, 60, "Setup");
            
            var farm = buildingSystem.Build("farm_basic");
            var idle = CreateWorker("idle_guy", WorkType.Idle, StatType.Agriculture, 1);
            var farmer = CreateWorker("farmer", WorkType.Farming, StatType.Agriculture, 5);
            Assert.IsTrue(characterSystem.AssignWork(farmer.characterId, WorkType.Farming, farm.buildingId));
            
            int predictedFood = resourceSystem.GetDailyProduction(ResourceType.Food);
            int predictedConsumption = resourceSystem.GetDailyConsumption(ResourceType.Food);
            int predictedNet = resourceSystem.GetNetDailyChange(ResourceType.Food);
            
            Assert.AreEqual(10, predictedFood, "Forecast uses the real worker efficiency");
            Assert.AreEqual(4, predictedConsumption, "Forecast counts every living character");
            Assert.AreEqual(predictedFood - predictedConsumption, predictedNet);
            
            int foodBefore = resourceSystem.GetAmount(ResourceType.Food);
            ProcessTimeSlot(TimeSlot.Action);
            int foodAfter = resourceSystem.GetAmount(ResourceType.Food);
            
            Assert.AreEqual(predictedNet, foodAfter - foodBefore, "Settlement must match the forecast exactly");
            Assert.IsNull(idle.assignedBuildingId, "Idle characters stay unassigned");
        }
        
        [Test]
        public void ProductionForecast_IsZeroWithoutWorkers()
        {
            resourceSystem.Add(ResourceType.Wood, 200, "Setup");
            resourceSystem.Add(ResourceType.Stone, 200, "Setup");
            resourceSystem.Add(ResourceType.Water, 200, "Setup");
            
            var farm = buildingSystem.Build("farm_basic");
            Assert.IsNotNull(farm);
            Assert.AreEqual(0, buildingSystem.GetBuildingDailyProduction(farm, ResourceType.Food), "An unmanned farm produces nothing");
            Assert.AreEqual(0, resourceSystem.GetDailyProduction(ResourceType.Food));
        }
        
        [Test]
        public void SaveLoad_PreservesWorkAndState()
        {
            resourceSystem.Add(ResourceType.Wood, 200, "Setup");
            resourceSystem.Add(ResourceType.Stone, 200, "Setup");
            resourceSystem.Add(ResourceType.Food, 40, "Setup");
            resourceSystem.Add(ResourceType.Water, 40, "Setup");
            
            var farm = buildingSystem.Build("farm_basic");
            var worker = CreateWorker("saver", WorkType.Farming, StatType.Agriculture, 5);
            Assert.IsTrue(characterSystem.AssignWork(worker.characterId, WorkType.Farming, farm.buildingId));
            
            timeSystem.SetTime(3, TimeSlot.Night);
            
            string seed = gameState.gameSeed;
            int day = gameState.currentDay;
            TimeSlot slot = gameState.currentTimeSlot;
            int food = resourceSystem.GetAmount(ResourceType.Food);
            int water = resourceSystem.GetAmount(ResourceType.Water);
            int capacity = resourceSystem.GetCapacity(ResourceType.Food);
            WorkType work = worker.currentWork;
            string assigned = worker.assignedBuildingId;
            string[] farmWorkers = farm.assignedWorkers.ToArray();
            
            saveSystem.SaveGame(false, "integration_save.json");
            
            var newGameState = new GameState();
            var newTimeSystem = new TimeSystem();
            var newRandomSystem = new RandomSystem();
            var newResourceSystem = new ResourceSystem();
            var newCharacterSystem = new CharacterSystem();
            var newBuildingSystem = new BuildingSystem();
            var newSaveSystem = new SaveSystem();
            
            newSaveSystem.Initialize(newGameState, "0.1.0");
            Assert.IsTrue(newSaveSystem.LoadGame("integration_save.json"));
            
            newRandomSystem.Initialize(newGameState.gameSeed ?? "fallback");
            newTimeSystem.Initialize(newGameState, newRandomSystem);
            newResourceSystem.Initialize(newGameState, newBuildingSystem, newCharacterSystem);
            newCharacterSystem.Initialize(newGameState, newResourceSystem, newRandomSystem, newBuildingSystem);
            newBuildingSystem.Initialize(newGameState, newResourceSystem, newCharacterSystem);
            
            var loadedWorker = newCharacterSystem.GetCharacter(worker.characterId);
            var loadedFarm = newBuildingSystem.GetBuilding(farm.buildingId);
            
            Assert.IsNotNull(loadedWorker);
            Assert.IsNotNull(loadedFarm);
            Assert.AreEqual(seed, newGameState.gameSeed);
            Assert.AreEqual(day, newGameState.currentDay);
            Assert.AreEqual(slot, newGameState.currentTimeSlot);
            Assert.AreEqual(food, newResourceSystem.GetAmount(ResourceType.Food));
            Assert.AreEqual(water, newResourceSystem.GetAmount(ResourceType.Water));
            Assert.AreEqual(capacity, newResourceSystem.GetCapacity(ResourceType.Food), "Storage capacity survives a reload");
            Assert.AreEqual(work, loadedWorker.currentWork);
            Assert.AreEqual(assigned, loadedWorker.assignedBuildingId);
            CollectionAssert.AreEqual(farmWorkers, loadedFarm.assignedWorkers, "Building worker list must survive a reload");
            
            // The reloaded world must stay playable.
            ProcessSlot(newTimeSystem, newBuildingSystem, newCharacterSystem, TimeSlot.Action);
            Assert.IsTrue(newResourceSystem.GetAmount(ResourceType.Food) > food, "The loaded farm still produces");
        }
        
        [Test]
        public void ReloadedCapacity_DoesNotDoubleWithBuildings()
        {
            resourceSystem.Add(ResourceType.Wood, 500, "Setup");
            resourceSystem.Add(ResourceType.Stone, 500, "Setup");
            
            var warehouse = buildingSystem.Build("warehouse");
            buildingSystem.UpgradeBuilding(warehouse.buildingId);
            
            int capacityBefore = resourceSystem.GetCapacity(ResourceType.Food);
            
            var newGameState = new GameState();
            var newResourceSystem = new ResourceSystem();
            var newCharacterSystem = new CharacterSystem();
            var newBuildingSystem = new BuildingSystem();
            var newSaveSystem = new SaveSystem();
            var newRandomSystem = new RandomSystem();
            
            saveSystem.SaveGame(false, "integration_capacity.json");
            newSaveSystem.Initialize(newGameState, "0.1.0");
            Assert.IsTrue(newSaveSystem.LoadGame("integration_capacity.json"));
            
            newRandomSystem.Initialize(newGameState.gameSeed ?? "fallback");
            newResourceSystem.Initialize(newGameState, newBuildingSystem, newCharacterSystem);
            newCharacterSystem.Initialize(newGameState, newResourceSystem, newRandomSystem, newBuildingSystem);
            newBuildingSystem.Initialize(newGameState, newResourceSystem, newCharacterSystem);
            
            Assert.AreEqual(capacityBefore, newResourceSystem.GetCapacity(ResourceType.Food), "Reloading recalculates the same capacity instead of adding it again");
        }
    }
}
