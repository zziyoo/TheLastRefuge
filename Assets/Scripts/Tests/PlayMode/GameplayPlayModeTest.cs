using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LastRefuge.Core;
using LastRefuge.Data;
using LastRefuge.Gameplay;

namespace LastRefuge.Tests
{
    /// <summary>
    /// Runs the real scene stack (GameBootstrap + UISetup + UIManager + GameManager) inside
    /// Unity play mode and walks through the acceptance flow:
    /// new game -> build farm -> assign worker -> advance a day -> night recovery -> save/load.
    /// The UIManager is reached through reflection, the same way GameLauncher does it, because
    /// the test assembly does not reference the UI assembly.
    /// </summary>
    public class GameplayPlayModeTest
    {
        private Type uiManagerType;
        private object uiManager;
        private GameManager gameManager;
        
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // UISetup runs through RuntimeInitializeOnLoadMethod, so the UI already exists.
            yield return null;
            yield return null;
            
            // GameBootstrap is the component Main.unity uses to create the GameManager.
            if (GameManager.Instance == null)
            {
                var bootstrapGo = new GameObject("GameBootstrapForTest");
                bootstrapGo.AddComponent<GameBootstrap>();
            }
            yield return null;
            
            uiManagerType = Type.GetType("LastRefuge.UI.UIManager, LastRefuge.UI");
            Assert.IsNotNull(uiManagerType, "UIManager type must be available at runtime");
            
            uiManager = uiManagerType.GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(uiManager, "UIManager.Instance must be created by UISetup");
            
            gameManager = GameManager.Instance;
            Assert.IsNotNull(gameManager, "GameManager must exist in play mode");
        }
        
        [UnityTest]
        public IEnumerator AcceptanceFlow_RunsInsidePlayMode()
        {
            // --- New Game through the real button handler ---
            InvokeUI("OnNewGameClicked");
            yield return null;
            
            var characters = gameManager.characterSystem.GetAliveCharacters();
            Assert.AreEqual(4, characters.Length, "A new game starts with 4 characters");
            
            // Deterministic farmer so the numbers below are stable.
            var farmer = characters[0];
            farmer.traits = new string[0];
            farmer.stats = new CharacterStats();
            farmer.stats.SetStat(StatType.Agriculture, 5);
            farmer.health = farmer.maxHealth;
            farmer.hunger = 0f;
            farmer.stress = 0f;
            farmer.fatigue = 0f;
            
            // --- Build a farm ---
            Assert.IsTrue(gameManager.BuildBuilding("farm_basic"), "A farm must be affordable after New Game");
            var farm = gameManager.buildingSystem.GetBuildingsByDefinition("farm_basic").FirstOrDefault();
            Assert.IsNotNull(farm);
            Assert.AreEqual(1, farm.level);
            
            // --- Assign the farmer through the real entry point ---
            gameManager.AssignWork(farmer.characterId, WorkType.Farming, farm.buildingId);
            yield return null;
            
            Assert.AreEqual(WorkType.Farming, farmer.currentWork);
            Assert.AreEqual(farm.buildingId, farmer.assignedBuildingId);
            Assert.AreEqual(1, farm.assignedWorkers.Length, "The farm must show its worker");
            Assert.AreEqual(farmer.characterId, farm.assignedWorkers[0]);
            
            // --- Morning -> Planning -> Action: production and consumption ---
            int foodBefore = gameManager.resourceSystem.GetAmount(ResourceType.Food);
            int waterBefore = gameManager.resourceSystem.GetAmount(ResourceType.Water);
            
            InvokeUI("OnNextTimeSlotClicked");
            InvokeUI("OnNextTimeSlotClicked");
            InvokeUI("OnNextTimeSlotClicked");
            yield return null;
            
            Assert.AreEqual(TimeSlot.Evening, gameManager.GetCurrentTimeSlot(), "The Action slot must have been processed");
            int foodAfterAction = gameManager.resourceSystem.GetAmount(ResourceType.Food);
            Assert.Greater(foodAfterAction, foodBefore, "The farm must feed the colony");
            Assert.Less(gameManager.resourceSystem.GetAmount(ResourceType.Water), waterBefore, "Water must be spent on upkeep and drinking");
            
            // --- Evening -> Night: recovery ---
            farmer.fatigue = 40f;
            InvokeUI("OnNextTimeSlotClicked");
            InvokeUI("OnNextTimeSlotClicked");
            yield return null;
            
            Assert.AreEqual(TimeSlot.DayEnd, gameManager.GetCurrentTimeSlot());
            Assert.Less(farmer.fatigue, 40f, "Night must recover fatigue");
            
            // --- DayEnd: autosave and roll over into day 2 ---
            InvokeUI("OnNextTimeSlotClicked");
            yield return null;
            
            Assert.AreEqual(2, gameManager.GetCurrentDay(), "The day must advance");
            Assert.AreEqual(TimeSlot.Morning, gameManager.GetCurrentTimeSlot());
            
            // --- Save and reload ---
            string fileName = SaveAndReload();
            Assert.IsNotNull(fileName, "The manual save must be written");
            
            yield return null;
            
            Assert.AreEqual(2, gameManager.GetCurrentDay(), "Day survives a reload");
            Assert.AreEqual(WorkType.Farming, gameManager.characterSystem.GetCharacter(farmer.characterId).currentWork);
            Assert.AreEqual(farm.buildingId, gameManager.characterSystem.GetCharacter(farmer.characterId).assignedBuildingId);
            
            var reloadedFarm = gameManager.buildingSystem.GetBuilding(farm.buildingId);
            Assert.IsNotNull(reloadedFarm);
            Assert.AreEqual(1, reloadedFarm.assignedWorkers.Length, "The farm keeps its worker across a reload");
            
            // --- Upgrading must not inflate storage ---
            gameManager.resourceSystem.Add(ResourceType.Wood, 200, "Test");
            gameManager.resourceSystem.Add(ResourceType.Stone, 200, "Test");
            int capacityBefore = gameManager.resourceSystem.GetCapacity(ResourceType.Food);
            Assert.IsTrue(gameManager.UpgradeBuilding(reloadedFarm.buildingId), "The farm must be upgradeable");
            Assert.AreEqual(2, reloadedFarm.level);
            Assert.AreEqual(capacityBefore, gameManager.resourceSystem.GetCapacity(ResourceType.Food), "A farm grants no storage, so capacity must not change");
            
            // --- A building without maintenance must stop working ---
            gameManager.resourceSystem.Add(ResourceType.Metal, 200, "Test");
            gameManager.resourceSystem.Add(ResourceType.Parts, 200, "Test");
            gameManager.resourceSystem.Add(ResourceType.Fuel, 200, "Test");
            var generator = gameManager.buildingSystem.Build("generator");
            Assert.IsNotNull(generator);
            
            int powerBefore = gameManager.resourceSystem.GetAmount(ResourceType.Power);
            gameManager.buildingSystem.ProcessBuildingProduction();
            Assert.Greater(gameManager.resourceSystem.GetAmount(ResourceType.Power), powerBefore, "A fueled generator produces power");
            
            gameManager.resourceSystem.Remove(ResourceType.Fuel, gameManager.resourceSystem.GetAmount(ResourceType.Fuel), "Test");
            Assert.IsFalse(gameManager.buildingSystem.CanOperate(generator), "Without fuel the generator cannot operate");
            
            powerBefore = gameManager.resourceSystem.GetAmount(ResourceType.Power);
            gameManager.buildingSystem.ProcessBuildingProduction();
            Assert.AreEqual(powerBefore, gameManager.resourceSystem.GetAmount(ResourceType.Power), "An unmaintained building must not produce");
            
            yield return null;
        }
        
        private void InvokeUI(string methodName)
        {
            uiManagerType.GetMethod(methodName).Invoke(uiManager, null);
        }
        
        private string SaveAndReload()
        {
            var before = gameManager.saveSystem.GetSaveFiles();
            gameManager.SaveGame(false);
            var after = gameManager.saveSystem.GetSaveFiles();
            
            string newFile = after.Except(before).FirstOrDefault();
            if (newFile == null) return null;
            
            string fileName = Path.GetFileName(newFile);
            gameManager.LoadGame(fileName);
            return fileName;
        }
    }
}
