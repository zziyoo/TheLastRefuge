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
using LastRefuge.Save;

namespace LastRefuge.Tests
{
    /// <summary>
    /// Loads the real Main scene (GameBootstrap + GameLauncher + GameManager) inside play
    /// mode and walks through the acceptance flow: new game -> build farm -> assign worker
    /// -> advance a day -> night recovery -> DayEnd autosave -> continue -> save/load.
    /// Every test uses an isolated save folder so real player saves are never touched.
    /// The UIManager is reached through reflection because the test assembly does not
    /// reference the UI assembly; it also recreates the runtime canvas (UISetup runs once
    /// per play mode entry, not per scene load).
    /// </summary>
    public class GameplayPlayModeTest
    {
        private const string SceneName = "Main";
        
        private Type uiManagerType;
        private object uiManager;
        private GameManager gameManager;
        private string testSaveDirectory;
        
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // GameLauncher/GameManager autosave to a dedicated test folder, never the
            // player's real saves.
            testSaveDirectory = Path.Combine(Application.persistentDataPath, "PlayModeTest_Saves");
            if (Directory.Exists(testSaveDirectory))
            {
                Directory.Delete(testSaveDirectory, true);
            }
            SaveSystem.OverrideSaveDirectory = testSaveDirectory;
            
            // Drop any manager left over from a previous test so this test starts clean.
            if (GameManager.Instance != null)
            {
                UnityEngine.Object.Destroy(GameManager.Instance.gameObject);
            }
            yield return null;
            
            // Load the real game scene. GameBootstrap + GameManager + GameLauncher are
            // part of it, so the manager is created exactly like a real launch.
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null;
            
            if (GameManager.Instance == null)
            {
                var bootstrapGO = new GameObject("TestGameBootstrap");
                bootstrapGO.AddComponent<GameBootstrap>();
            }
            yield return null;
            yield return null;
            
            EnsureUIExists();
            yield return null;
            yield return null;
            
            uiManagerType = Type.GetType("LastRefuge.UI.UIManager, LastRefuge.UI");
            Assert.IsNotNull(uiManagerType, "UIManager type must be available at runtime");
            uiManager = uiManagerType.GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(uiManager, "UIManager.Instance must be alive after recreating the UI");
            
            gameManager = GameManager.Instance;
            Assert.IsNotNull(gameManager, "GameManager must exist after loading Main");
            
            yield return null;
        }
        
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return null;
        }
        
        /// <summary>
        /// UISetup.SetupUI runs once per play-mode entry (RuntimeInitializeOnLoadMethod),
        /// and loading Main destroyed that UI again. Recreate it exactly as UISetup does.
        /// GameLauncher retries for a few frames, so it picks up the recreated UIManager.
        /// </summary>
        private void EnsureUIExists()
        {
            var uiType = Type.GetType("LastRefuge.UI.UIManager, LastRefuge.UI");
            var existing = uiType?.GetProperty("Instance")?.GetValue(null);
            bool alive = existing != null && (UnityEngine.Object)existing != null;
            if (alive && GameObject.Find("MainCanvas") != null) return;
            
            var uiSetupType = Type.GetType("LastRefuge.UI.UISetup, LastRefuge.UI");
            if (uiSetupType == null) return;
            var setupMethod = uiSetupType.GetMethod("SetupUI", BindingFlags.NonPublic | BindingFlags.Static);
            setupMethod?.Invoke(null, null);
        }
        
        private void InvokeUI(string methodName)
        {
            uiManagerType.GetMethod(methodName).Invoke(uiManager, null);
        }
        
        private void Click(int times)
        {
            for (int i = 0; i < times; i++)
            {
                // These tests walk the day loop; keep the daily event roll from
                // interrupting them with real content events.
                gameManager.gameState.gameFlags.intFlags["event_roll_day"] = gameManager.GetCurrentDay();
                InvokeUI("OnNextTimeSlotClicked");
            }
        }
        
        private void InvokeQuit()
        {
            var method = typeof(GameManager).GetMethod("OnApplicationQuit", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(gameManager, null);
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
        
        [UnityTest]
        public IEnumerator AcceptanceFlow_RunsInsidePlayMode()
        {
            // --- New Game through the real button handler ---
            InvokeUI("OnNewGameClicked");
            yield return null;
            
            var characters = gameManager.characterSystem.GetAliveCharacters();
            Assert.AreEqual(4, characters.Length, "A new game starts with 4 characters");
            Assert.IsTrue(gameManager.hasActiveGame, "A started game must be an active run");
            
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
            
            Click(3);
            yield return null;
            
            Assert.AreEqual(TimeSlot.Evening, gameManager.GetCurrentTimeSlot(), "The Action slot must have been processed");
            int foodAfterAction = gameManager.resourceSystem.GetAmount(ResourceType.Food);
            Assert.Greater(foodAfterAction, foodBefore,
                $"diag foodBefore={foodBefore} state={gameManager.gameState.gameplayState} " +
                $"slot={gameManager.GetCurrentTimeSlot()} pending={gameManager.GetPendingEvent()?.id} " +
                $"assigned={farmer.currentWork}/{farmer.assignedBuildingId}");
            Assert.Less(gameManager.resourceSystem.GetAmount(ResourceType.Water), waterBefore, "Water must be spent on upkeep and drinking");
            
            // --- Evening -> Night -> DayEnd ---
            farmer.fatigue = 40f;
            Click(2);
            yield return null;
            
            Assert.AreEqual(TimeSlot.DayEnd, gameManager.GetCurrentTimeSlot());
            Assert.Less(farmer.fatigue, 40f, "Night must recover fatigue");
            Assert.AreEqual(1, gameManager.GetCurrentDay(), "DayEnd must still be day 1, not yet rolled over");
            
            // --- DayEnd -> Day 2 Morning: the autosave is written at the rolled-over
            // Morning, never at an un-settled DayEnd.
            Click(1);
            yield return null;
            
            Assert.AreEqual(2, gameManager.GetCurrentDay(), "The day must advance");
            Assert.AreEqual(TimeSlot.Morning, gameManager.GetCurrentTimeSlot());
            
            var autosaveInfo = gameManager.saveSystem.GetSaveInfo("autosave.json");
            Assert.IsNotNull(autosaveInfo, "The DayEnd rollover must write an autosave");
            Assert.AreEqual(2, autosaveInfo.day, "The autosave must be Day 2");
            Assert.AreEqual(TimeSlot.Morning, autosaveInfo.timeSlot, "The autosave must never hold an un-settled DayEnd");
            
            // --- Continue: reload the autosave and keep playing ---
            gameManager.LoadGame("autosave.json");
            yield return null;
            
            Assert.AreEqual(2, gameManager.GetCurrentDay(), "Day survives an autosave reload");
            Assert.AreEqual(TimeSlot.Morning, gameManager.GetCurrentTimeSlot());
            Assert.AreEqual(WorkType.Farming, gameManager.characterSystem.GetCharacter(farmer.characterId).currentWork);
            Assert.AreEqual(farm.buildingId, gameManager.characterSystem.GetCharacter(farmer.characterId).assignedBuildingId);
            
            var reloadedFarm = gameManager.buildingSystem.GetBuilding(farm.buildingId);
            Assert.IsNotNull(reloadedFarm);
            Assert.AreEqual(1, reloadedFarm.assignedWorkers.Length, "The farm keeps its worker across a reload");
            
            // The reloaded morning is resumable and produces again on the next Action.
            int foodAtContinue = gameManager.resourceSystem.GetAmount(ResourceType.Food);
            Click(3);
            yield return null;
            Assert.AreEqual(TimeSlot.Evening, gameManager.GetCurrentTimeSlot());
            Assert.Greater(gameManager.resourceSystem.GetAmount(ResourceType.Food), foodAtContinue,
                "A continued game must keep producing on the next Action");
            
            // --- Save and reload (manual) ---
            string fileName = SaveAndReload();
            Assert.IsNotNull(fileName, "The manual save must be written");
            yield return null;
            
            Assert.AreEqual(WorkType.Farming, gameManager.characterSystem.GetCharacter(farmer.characterId).currentWork);
            
            // --- Upgrading must not inflate storage ---
            gameManager.resourceSystem.Add(ResourceType.Wood, 200, "Test");
            gameManager.resourceSystem.Add(ResourceType.Stone, 200, "Test");
            int capacityBefore = gameManager.resourceSystem.GetCapacity(ResourceType.Food);
            reloadedFarm = gameManager.buildingSystem.GetBuilding(reloadedFarm.buildingId);
            Assert.IsNotNull(reloadedFarm, "The farm must exist after the manual reload");
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
        
        [UnityTest]
        public IEnumerator DayEndQuit_AutosavesAtMorningAndContinues()
        {
            InvokeUI("OnNewGameClicked");
            yield return null;
            
            // Walk to DayEnd without settling the day.
            Click(5);
            yield return null;
            Assert.AreEqual(TimeSlot.DayEnd, gameManager.GetCurrentTimeSlot());
            Assert.AreEqual(1, gameManager.GetCurrentDay());
            
            string autosaveBefore = Path.Combine(testSaveDirectory, "autosave.json");
            Assert.IsFalse(File.Exists(autosaveBefore), "No autosave may exist before the day is settled");
            
            // Player exits while the app is parked on an un-settled DayEnd.
            InvokeQuit();
            yield return null;
            
            var info = gameManager.saveSystem.GetSaveInfo("autosave.json");
            Assert.IsNotNull(info, "Quitting with an active game must save");
            Assert.AreEqual(2, info.day, "The quit autosave must hold the settled Day 2");
            Assert.AreEqual(TimeSlot.Morning, info.timeSlot, "The quit autosave must never hold an un-settled DayEnd");
            Assert.AreEqual(TimeSlot.Morning, gameManager.GetCurrentTimeSlot(), "Quitting rolls the live state over to Morning");
            
            gameManager.LoadGame("autosave.json");
            yield return null;
            Assert.AreEqual(2, gameManager.GetCurrentDay());
            Assert.AreEqual(TimeSlot.Morning, gameManager.GetCurrentTimeSlot());
            
            // Day 2 must be playable after a DayEnd quit + continue.
            Click(1);
            yield return null;
            Assert.AreEqual(TimeSlot.Planning, gameManager.GetCurrentTimeSlot(), "Day 2 must be playable after a DayEnd quit + continue");
        }
        
        [UnityTest]
        public IEnumerator QuitWithoutActiveRun_DoesNotCreateAnAutosave()
        {
            string autosavePath = Path.Combine(testSaveDirectory, "autosave.json");
            Assert.IsFalse(File.Exists(autosavePath), "The fresh save area must start empty");
            Assert.IsFalse(gameManager.hasActiveGame, "A freshly booted manager has no active run");
            
            InvokeQuit();
            yield return null;
            
            Assert.IsFalse(File.Exists(autosavePath), "Quitting from the main menu must not manufacture an autosave");
            Assert.AreEqual(GameplayState.MainMenu, gameManager.GetCurrentGameplayState());
        }
        
        [UnityTest]
        public IEnumerator QuitWithoutActiveRun_DoesNotOverwriteExistingAutosave()
        {
            // Seed a legitimate autosave with a real run.
            InvokeUI("OnNewGameClicked");
            yield return null;
            Click(1);
            yield return null;
            InvokeQuit();
            yield return null;
            
            string autosavePath = Path.Combine(testSaveDirectory, "autosave.json");
            Assert.IsTrue(File.Exists(autosavePath), "A run quit must write the autosave");
            string contentBefore = File.ReadAllText(autosavePath);
            
            // Simulate a fresh app launch: destroy the run (no active game) and boot a
            // brand new manager while the save file still exists on disk.
            if (gameManager != null)
            {
                UnityEngine.Object.Destroy(gameManager.gameObject);
                gameManager = null;
            }
            yield return null;
            
            if (GameManager.Instance == null)
            {
                var bootstrapGO = new GameObject("TestGameBootstrapAgain");
                bootstrapGO.AddComponent<GameBootstrap>();
            }
            yield return null;
            
            gameManager = GameManager.Instance;
            Assert.IsNotNull(gameManager);
            Assert.IsFalse(gameManager.hasActiveGame, "The booted manager must have no active run");
            
            InvokeQuit();
            yield return null;
            
            Assert.IsTrue(File.Exists(autosavePath));
            Assert.AreEqual(contentBefore, File.ReadAllText(autosavePath),
                "A main-menu quit must not touch the existing autosave");
        }
    }
}