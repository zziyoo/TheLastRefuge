using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LastRefuge.Core;
using LastRefuge.Data;
using LastRefuge.Systems;
using LastRefuge.Save;
using LastRefuge.Gameplay;

namespace LastRefuge.Tests
{
    public class CoreSystemsTest
    {
        private GameState gameState;
        private TimeSystem timeSystem;
        private RandomSystem randomSystem;
        private ResourceSystem resourceSystem;
        private CharacterSystem characterSystem;
        private BuildingSystem buildingSystem;
        private EffectResolver effectResolver;
        private SaveSystem saveSystem;
        private GameManager gameManager;
        
        [SetUp]
        public void Setup()
        {
            gameState = new GameState();
            
            timeSystem = new TimeSystem();
            randomSystem = new RandomSystem();
            resourceSystem = new ResourceSystem();
            characterSystem = new CharacterSystem();
            buildingSystem = new BuildingSystem();
            effectResolver = new EffectResolver();
            saveSystem = new SaveSystem();
            
            timeSystem.Initialize(gameState, randomSystem);
            resourceSystem.Initialize(gameState);
            characterSystem.Initialize(gameState, resourceSystem, randomSystem, buildingSystem);
            buildingSystem.Initialize(gameState, resourceSystem, characterSystem);
            effectResolver.Initialize(gameState, resourceSystem, characterSystem, buildingSystem);
            saveSystem.Initialize(gameState, "0.1.0");
            
            randomSystem.Initialize("test_seed_12345");
        }
        
        [Test]
        public void GameState_Initialization_Works()
        {
            Assert.IsNotNull(gameState);
            Assert.AreEqual(1, gameState.currentDay);
            Assert.AreEqual(TimeSlot.Morning, gameState.currentTimeSlot);
            Assert.AreEqual(GameplayState.MainMenu, gameState.gameplayState);
        }
        
        [Test]
        public void RandomSystem_SameSeed_ProducesSameResults()
        {
            randomSystem.Initialize("test_seed");
            int a1 = randomSystem.NextInt(100);
            float b1 = randomSystem.NextFloat();
            
            randomSystem.Initialize("test_seed");
            int a2 = randomSystem.NextInt(100);
            float b2 = randomSystem.NextFloat();
            
            Assert.AreEqual(a1, a2);
            Assert.AreEqual(b1, b2);
        }
        
        [Test]
        public void ResourceSystem_AddAndRemove_Works()
        {
            // Add resource
            bool added = resourceSystem.Add(ResourceType.Food, 10, "Test");
            Assert.IsTrue(added);
            Assert.AreEqual(10, resourceSystem.GetAmount(ResourceType.Food));
            
            // Remove resource
            bool removed = resourceSystem.Remove(ResourceType.Food, 5, "Test");
            Assert.IsTrue(removed);
            Assert.AreEqual(5, resourceSystem.GetAmount(ResourceType.Food));
            
            // Cannot remove more than available
            bool failed = resourceSystem.Remove(ResourceType.Food, 10, "Test");
            Assert.IsFalse(failed);
            Assert.AreEqual(5, resourceSystem.GetAmount(ResourceType.Food));
        }
        
        [Test]
        public void ResourceSystem_CannotGoNegative()
        {
            resourceSystem.Add(ResourceType.Food, 5, "Test");
            bool result = resourceSystem.Remove(ResourceType.Food, 10, "Test");
            Assert.IsFalse(result);
            Assert.AreEqual(5, resourceSystem.GetAmount(ResourceType.Food));
        }
        
        [Test]
        public void ResourceSystem_CanAfford_Works()
        {
            resourceSystem.Add(ResourceType.Food, 10, "Test");
            resourceSystem.Add(ResourceType.Wood, 20, "Test");
            
            Assert.IsTrue(resourceSystem.CanAfford(ResourceType.Food, 5));
            Assert.IsFalse(resourceSystem.CanAfford(ResourceType.Food, 15));
            
            var costs = new Dictionary<ResourceType, int>
            {
                { ResourceType.Food, 5 },
                { ResourceType.Wood, 10 }
            };
            Assert.IsTrue(resourceSystem.CanAfford(costs));
            
            costs[ResourceType.Food] = 15;
            Assert.IsFalse(resourceSystem.CanAfford(costs));
        }
        
        [Test]
        public void TimeSystem_AdvancesCorrectly()
        {
            timeSystem.SetTime(1, TimeSlot.Morning);
            
            Assert.AreEqual(TimeSlot.Morning, timeSystem.CurrentTimeSlot);
            
            timeSystem.AdvanceTimeSlot();
            Assert.AreEqual(TimeSlot.Planning, timeSystem.CurrentTimeSlot);
            
            timeSystem.AdvanceTimeSlot();
            Assert.AreEqual(TimeSlot.Action, timeSystem.CurrentTimeSlot);
            
            timeSystem.AdvanceTimeSlot();
            Assert.AreEqual(TimeSlot.Evening, timeSystem.CurrentTimeSlot);
            
            timeSystem.AdvanceTimeSlot();
            Assert.AreEqual(TimeSlot.Night, timeSystem.CurrentTimeSlot);
            
            timeSystem.AdvanceTimeSlot();
            Assert.AreEqual(TimeSlot.DayEnd, timeSystem.CurrentTimeSlot);
            
            timeSystem.AdvanceTimeSlot();
            Assert.AreEqual(TimeSlot.Morning, timeSystem.CurrentTimeSlot);
            Assert.AreEqual(2, timeSystem.CurrentDay);
        }
        
        [Test]
        public void CharacterSystem_GeneratesInitialCharacters()
        {
            characterSystem.GenerateInitialCharacters(4);
            
            var characters = characterSystem.GetAllCharacters();
            Assert.AreEqual(4, characters.Length);
            
            foreach (var c in characters)
            {
                Assert.IsTrue(c.alive);
                Assert.AreEqual(100, c.health);
                Assert.IsNotNull(c.stats);
                Assert.IsNotNull(c.traits);
                Assert.AreEqual(WorkType.Idle, c.currentWork);
            }
        }
        
        [Test]
        public void CharacterSystem_AssignWork_Works()
        {
            characterSystem.GenerateInitialCharacters(1);
            var character = characterSystem.GetAllCharacters()[0];
            
            bool assigned = characterSystem.AssignWork(character.characterId, WorkType.Farming);
            Assert.IsTrue(assigned);
            Assert.AreEqual(WorkType.Farming, character.currentWork);
        }
        
        [Test]
        public void BuildingSystem_CanBuild_And_PaysCost()
        {
            resourceSystem.Add(ResourceType.Wood, 50, "Test");
            resourceSystem.Add(ResourceType.Stone, 30, "Test");
            
            bool canBuild = buildingSystem.CanBuild("shelter_temp");
            Assert.IsTrue(canBuild);
            
            var building = buildingSystem.Build("shelter_temp");
            Assert.IsNotNull(building);
            Assert.AreEqual("shelter_temp", building.definitionId);
            Assert.AreEqual(1, building.level);
            Assert.IsTrue(building.enabled);
            
            Assert.AreEqual(30, resourceSystem.GetAmount(ResourceType.Wood)); // 50 - 20
            Assert.AreEqual(20, resourceSystem.GetAmount(ResourceType.Stone)); // 30 - 10
        }
        
        [Test]
        public void BuildingSystem_CannotBuild_WhenResourcesInsufficient()
        {
            resourceSystem.Add(ResourceType.Wood, 10, "Test");
            resourceSystem.Add(ResourceType.Stone, 5, "Test");
            
            bool canBuild = buildingSystem.CanBuild("shelter_temp");
            Assert.IsFalse(canBuild);
            
            var building = buildingSystem.Build("shelter_temp");
            Assert.IsNull(building);
        }
        
        [Test]
        public void EffectResolver_AddResource_Works()
        {
            var effect = Effect.AddResource(ResourceType.Food, 25, "TestEffect");
            bool result = effectResolver.Resolve(effect);
            
            Assert.IsTrue(result);
            Assert.AreEqual(25, resourceSystem.GetAmount(ResourceType.Food));
        }
        
        [Test]
        public void EffectResolver_RemoveResource_Works()
        {
            resourceSystem.Add(ResourceType.Food, 30, "Setup");
            
            var effect = Effect.RemoveResource(ResourceType.Food, 10, "TestEffect");
            bool result = effectResolver.Resolve(effect);
            
            Assert.IsTrue(result);
            Assert.AreEqual(20, resourceSystem.GetAmount(ResourceType.Food));
        }
        
        [Test]
        public void EffectResolver_DamageCharacter_Works()
        {
            characterSystem.GenerateInitialCharacters(1);
            var character = characterSystem.GetAllCharacters()[0];
            int initialHealth = character.health;
            
            var effect = Effect.DamageCharacter(character.characterId, 30);
            bool result = effectResolver.Resolve(effect);
            
            Assert.IsTrue(result);
            Assert.AreEqual(initialHealth - 30, character.health);
        }
        
        [Test]
        public void EffectResolver_KillCharacter_Works()
        {
            characterSystem.GenerateInitialCharacters(1);
            var character = characterSystem.GetAllCharacters()[0];
            
            var effect = Effect.DamageCharacter(character.characterId, 200);
            effectResolver.Resolve(effect);
            
            Assert.IsFalse(character.alive);
            Assert.AreEqual(1, character.dayOfDeath);
        }
        
        [Test]
        public void SaveSystem_SaveAndLoad_PreservesState()
        {
            // Setup initial state
            randomSystem.Initialize("save_test_seed");
            gameState.gameSeed = "save_test_seed"; // Ensure seed is set for save
            resourceSystem.Add(ResourceType.Food, 100, "Test");
            resourceSystem.Add(ResourceType.Water, 50, "Test");
            characterSystem.GenerateInitialCharacters(3);
            buildingSystem.Build("shelter_temp");
            
            timeSystem.SetTime(5, TimeSlot.Evening);
            timeSystem.SetGameplayState(GameplayState.Evening);
            
            string seed = gameState.gameSeed;
            int day = gameState.currentDay;
            TimeSlot slot = gameState.currentTimeSlot;
            int food = resourceSystem.GetAmount(ResourceType.Food);
            int water = resourceSystem.GetAmount(ResourceType.Water);
            int charCount = characterSystem.GetAliveCharacters().Length;
            int buildingCount = buildingSystem.GetAllBuildings().Length;
            
            // Save
            saveSystem.SaveGame(false, "test_save.json");
            
            // Create fresh systems
            var newGameState = new GameState();
            var newResourceSystem = new ResourceSystem();
            var newCharacterSystem = new CharacterSystem();
            var newBuildingSystem = new BuildingSystem();
            var newSaveSystem = new SaveSystem();
            var newRandomSystem = new RandomSystem();
            var newTimeSystem = new TimeSystem();
            var newEffectResolver = new EffectResolver();
            
            // Load
            newSaveSystem.Initialize(newGameState, "0.1.0");
            bool loaded = newSaveSystem.LoadGame("test_save.json");
            Assert.IsTrue(loaded);
            
            // Initialize RandomSystem first (needs seed from loaded state)
            string loadedSeed = newGameState.gameSeed ?? "fallback_seed";
            newRandomSystem.Initialize(loadedSeed);
            
            // Re-initialize all systems with loaded state
            newTimeSystem.Initialize(newGameState, newRandomSystem);
            newResourceSystem.Initialize(newGameState);
            newCharacterSystem.Initialize(newGameState, newResourceSystem, newRandomSystem, newBuildingSystem);
            newBuildingSystem.Initialize(newGameState, newResourceSystem, newCharacterSystem);
            newEffectResolver.Initialize(newGameState, newResourceSystem, newCharacterSystem, newBuildingSystem);
            
            // Verify
            Assert.AreEqual(seed, newGameState.gameSeed);
            Assert.AreEqual(day, newGameState.currentDay);
            Assert.AreEqual(slot, newGameState.currentTimeSlot);
            Assert.AreEqual(food, newResourceSystem.GetAmount(ResourceType.Food));
            Assert.AreEqual(water, newResourceSystem.GetAmount(ResourceType.Water));
            Assert.AreEqual(charCount, newCharacterSystem.GetAliveCharacters().Length);
            Assert.AreEqual(buildingCount, newBuildingSystem.GetAllBuildings().Length);
        }
        
        [Test]
        public void CharacterSystem_WorkEfficiency_CalculatesCorrectly()
        {
            characterSystem.GenerateInitialCharacters(1);
            var character = characterSystem.GetAllCharacters()[0];
            
            character.currentWork = WorkType.Farming;
            character.health = 100;
            character.hunger = 0;
            character.stress = 0;
            character.fatigue = 0;
            
            float efficiency = characterSystem.GetWorkEfficiency(character);
            Assert.Greater(efficiency, 0f);
            Assert.LessOrEqual(efficiency, 3f); // Max with bonuses
        }
        
        [Test]
        public void BuildingSystem_Production_GeneratesResources()
        {
            resourceSystem.Add(ResourceType.Wood, 50, "Test");
            resourceSystem.Add(ResourceType.Stone, 30, "Test");
            resourceSystem.Add(ResourceType.Water, 50, "Test");
            
            var building = buildingSystem.Build("farm_basic");
            Assert.IsNotNull(building);
            
            // Assign a worker
            characterSystem.GenerateInitialCharacters(1);
            var character = characterSystem.GetAllCharacters()[0];
            character.currentWork = WorkType.Farming;
            character.stats.agriculture = 5;
            
            buildingSystem.AssignWorker(building.buildingId, character.characterId);
            
            // Process production
            buildingSystem.ProcessBuildingProduction();
            
            // Should have produced some food
            int food = resourceSystem.GetAmount(ResourceType.Food);
            Assert.Greater(food, 0);
        }
        
        [Test]
        public void TimeSlot_Next_ReturnsCorrectSequence()
        {
            Assert.AreEqual(TimeSlot.Planning, TimeSlot.Morning.Next());
            Assert.AreEqual(TimeSlot.Action, TimeSlot.Planning.Next());
            Assert.AreEqual(TimeSlot.Evening, TimeSlot.Action.Next());
            Assert.AreEqual(TimeSlot.Night, TimeSlot.Evening.Next());
            Assert.AreEqual(TimeSlot.DayEnd, TimeSlot.Night.Next());
            Assert.AreEqual(TimeSlot.Morning, TimeSlot.DayEnd.Next());
        }
        
        [Test]
        public void WorkType_GetPrimaryStat_ReturnsCorrectStat()
        {
            Assert.AreEqual(StatType.Agriculture, WorkType.Farming.GetPrimaryStat());
            Assert.AreEqual(StatType.Gathering, WorkType.Gathering.GetPrimaryStat());
            Assert.AreEqual(StatType.Engineering, WorkType.Engineering.GetPrimaryStat());
            Assert.AreEqual(StatType.Research, WorkType.Researching.GetPrimaryStat());
            Assert.AreEqual(StatType.Medical, WorkType.Medical.GetPrimaryStat());
            Assert.AreEqual(StatType.Exploration, WorkType.Exploring.GetPrimaryStat());
            Assert.AreEqual(StatType.Combat, WorkType.Combat.GetPrimaryStat());
        }
    }
}