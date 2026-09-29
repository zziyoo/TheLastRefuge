using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using LastRefuge.Core;
using LastRefuge.Data;
using LastRefuge.Gameplay;
using LastRefuge.Save;

namespace LastRefuge.Tests
{
    /// <summary>
    /// Regression coverage for the time/state fix batch:
    /// one click advances exactly one slot, consumption settles once per day,
    /// building cost is deducted once, save/load round-trips and dead characters
    /// leave their workplace and stop consuming.
    /// </summary>
    public class TimeAndStateRegressionTest
    {
        private GameManager gameManager;

        [SetUp]
        public void Setup()
        {
            // Save tests write to an isolated folder, never to the player's real saves.
            SaveSystem.OverrideSaveDirectory = Path.Combine(Application.persistentDataPath, "TimeAndStateRegressionTest_Saves");
            if (Directory.Exists(SaveSystem.OverrideSaveDirectory))
            {
                Directory.Delete(SaveSystem.OverrideSaveDirectory, true);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (gameManager != null)
            {
                Object.Destroy(gameManager.gameObject);
                gameManager = null;
            }

            if (Directory.Exists(SaveSystem.OverrideSaveDirectory))
            {
                Directory.Delete(SaveSystem.OverrideSaveDirectory, true);
            }
        }

        private GameManager StartNewGame()
        {
            var go = new GameObject("GameManager");
            gameManager = go.AddComponent<GameManager>();
            gameManager.NewGame("regression_seed");
            return gameManager;
        }

        /// <summary>NewGame must leave a playable colony with the expected starting ledger.</summary>
        [Test]
        public void NewGame_HasPlayableInitialState()
        {
            var gm = StartNewGame();

            Assert.AreEqual(30, gm.resourceSystem.GetAmount(ResourceType.Food));
            Assert.AreEqual(30, gm.resourceSystem.GetAmount(ResourceType.Water));
            // 60 wood / 30 stone minus the free shelter_temp (20 wood / 10 stone).
            Assert.AreEqual(40, gm.resourceSystem.GetAmount(ResourceType.Wood));
            Assert.AreEqual(20, gm.resourceSystem.GetAmount(ResourceType.Stone));

            Assert.AreEqual(4, gm.characterSystem.GetAliveCharacters().Length);
            Assert.GreaterOrEqual(gm.buildingSystem.GetAllBuildings().Length, 1);

            Assert.AreEqual(1, gm.GetCurrentDay());
            Assert.AreEqual(TimeSlot.Morning, gm.GetCurrentTimeSlot());
            Assert.AreEqual(GameplayState.Morning, gm.GetCurrentGameplayState());
        }

        /// <summary>One AdvanceTimeSlot call must move exactly one slot, never two.</summary>
        [Test]
        public void AdvanceTimeSlot_AdvancesExactlyOneSlotPerClick()
        {
            var gm = StartNewGame();

            Assert.AreEqual(TimeSlot.Morning, gm.GetCurrentTimeSlot());
            gm.AdvanceTimeSlot();
            Assert.AreEqual(TimeSlot.Planning, gm.GetCurrentTimeSlot());
            Assert.AreEqual(1, gm.GetCurrentDay(), "A single click must never roll the day over.");

            gm.AdvanceTimeSlot();
            Assert.AreEqual(TimeSlot.Action, gm.GetCurrentTimeSlot());
            Assert.AreEqual(1, gm.GetCurrentDay());
        }

        /// <summary>
        /// Four survivors consume exactly 8 food and 8 water per day (2 each), settled
        /// once: after a full day the ledger reads 22/22 and the day has rolled to 2.
        /// </summary>
        [Test]
        public void FullDayConsumption_FourCharactersSettlesExactlyOnce()
        {
            var gm = StartNewGame();

            // Deterministic demand: no trait may multiply consumption.
            foreach (var character in gm.characterSystem.GetAliveCharacters())
            {
                character.traits = new string[0];
                character.hunger = 0f;
            }

            // Morning -> Planning -> Action -> Evening -> Night -> DayEnd -> Morning.
            for (int i = 0; i < 6; i++)
            {
                gm.AdvanceTimeSlot();
            }

            Assert.AreEqual(2, gm.GetCurrentDay());
            Assert.AreEqual(TimeSlot.Morning, gm.GetCurrentTimeSlot());
            Assert.AreEqual(22, gm.resourceSystem.GetAmount(ResourceType.Food), "Food must be consumed exactly once per day (30 - 8).");
            Assert.AreEqual(22, gm.resourceSystem.GetAmount(ResourceType.Water), "Water must be consumed exactly once per day (30 - 8).");
        }

        /// <summary>
        /// Building pays its cost exactly once, and maxCount is enforced even for
        /// direct Build() calls, not only through the UI.
        /// </summary>
        [Test]
        public void Build_DeductsCostOnce_AndEnforcesMaxCount()
        {
            var gm = StartNewGame();

            int woodBefore = gm.resourceSystem.GetAmount(ResourceType.Wood);
            int stoneBefore = gm.resourceSystem.GetAmount(ResourceType.Stone);

            var farm = gm.buildingSystem.Build("farm_basic");
            Assert.IsNotNull(farm, "Farm should be buildable with the starting ledger.");

            Assert.AreEqual(woodBefore - 30, gm.resourceSystem.GetAmount(ResourceType.Wood), "Wood must be deducted exactly once.");
            Assert.AreEqual(stoneBefore - 10, gm.resourceSystem.GetAmount(ResourceType.Stone), "Stone must be deducted exactly once.");

            // shelter_temp maxCount = 3, one exists from NewGame: two more builds, then a wall.
            gm.resourceSystem.Add(ResourceType.Wood, 100, "Test");
            gm.resourceSystem.Add(ResourceType.Stone, 100, "Test");

            Assert.IsNotNull(gm.buildingSystem.Build("shelter_temp"), "Second shelter should be buildable.");
            Assert.IsNotNull(gm.buildingSystem.Build("shelter_temp"), "Third shelter should be buildable.");
            Assert.IsNull(gm.buildingSystem.Build("shelter_temp"), "Fourth shelter must be refused by maxCount.");
            Assert.IsFalse(gm.buildingSystem.CanBuildCount("shelter_temp"));
        }

        /// <summary>Save -> load must restore day, time slot and the consumed ledger.</summary>
        [Test]
        public void SaveLoad_PreservesDayTimeSlotAndResources()
        {
            var gm = StartNewGame();

            foreach (var character in gm.characterSystem.GetAliveCharacters())
            {
                character.traits = new string[0];
            }

            gm.AdvanceTimeSlot();
            gm.AdvanceTimeSlot();
            gm.AdvanceTimeSlot(); // Action settled: food/water are now 22.

            Assert.AreEqual(1, gm.GetCurrentDay());
            Assert.AreEqual(TimeSlot.Evening, gm.GetCurrentTimeSlot());
            int foodBeforeSave = gm.resourceSystem.GetAmount(ResourceType.Food);
            Assert.AreEqual(22, foodBeforeSave);

            Assert.IsTrue(gm.saveSystem.SaveGame(false, "regression_roundtrip"));
            var saves = gm.saveSystem.GetSaveFiles();
            Assert.Greater(saves.Length, 0, "The save file must exist on disk.");
            string fileName = Path.GetFileName(saves.OrderByDescending(File.GetLastWriteTime).First());

            // Wipe the state, then bring the save back.
            gm.NewGame("fresh_run");
            Assert.AreEqual(30, gm.resourceSystem.GetAmount(ResourceType.Food));
            Assert.AreEqual(1, gm.GetCurrentDay());

            Assert.IsTrue(gm.saveSystem.LoadGame(fileName));

            Assert.AreEqual(1, gm.GetCurrentDay());
            Assert.AreEqual(TimeSlot.Evening, gm.GetCurrentTimeSlot());
            Assert.AreEqual(foodBeforeSave, gm.resourceSystem.GetAmount(ResourceType.Food), "Loaded food must match the saved ledger.");
        }

        /// <summary>
        /// A dead character must be detached from the building roster, drop out of the
        /// demand forecast and be skipped by the daily settlement.
        /// </summary>
        [Test]
        public void DeadCharacter_DetachedFromBuilding_AndSkippedByConsumption()
        {
            var gm = StartNewGame();

            foreach (var character in gm.characterSystem.GetAliveCharacters())
            {
                character.traits = new string[0];
                character.hunger = 0f;
            }

            var farm = gm.buildingSystem.Build("farm_basic");
            Assert.IsNotNull(farm);

            var victim = gm.characterSystem.GetAliveCharacters()[0];
            Assert.IsTrue(gm.AssignWork(victim.characterId, WorkType.Farming, farm.buildingId));
            Assert.Contains(victim.characterId, farm.assignedWorkers, "The worker must be on the building roster.");
            Assert.AreEqual(8, gm.characterSystem.GetTotalFoodConsumption());

            gm.characterSystem.KillCharacter(victim.characterId, "TestDeath");

            Assert.IsFalse(victim.alive);
            Assert.IsFalse(farm.assignedWorkers.Contains(victim.characterId), "Death must detach the worker from the building.");
            Assert.AreEqual(6, gm.characterSystem.GetTotalFoodConsumption(), "Demand must drop by the dead character's share.");

            float hungerBefore = victim.hunger;
            gm.characterSystem.ProcessDailyConsumption();
            Assert.AreEqual(hungerBefore, victim.hunger, "The settlement must skip dead characters.");

            var alive = gm.characterSystem.GetAliveCharacters();
            Assert.AreEqual(3, alive.Length);
            Assert.AreEqual(24, gm.resourceSystem.GetAmount(ResourceType.Food), "Only the 3 survivors eat: 30 - 6.");
        }
    }
}
