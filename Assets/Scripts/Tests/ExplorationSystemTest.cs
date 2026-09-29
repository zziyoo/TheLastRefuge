using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using LastRefuge.Core;
using LastRefuge.Data;
using LastRefuge.Gameplay;
using LastRefuge.Save;
using LastRefuge.Systems;
using EventType = LastRefuge.Core.EventType;

namespace LastRefuge.Tests
{
    public class ExplorationSystemTest
    {
        private GameManager gameManager;
        private GameState state;
        private RandomSystem randomSystem;
        private TimeSystem timeSystem;
        private ResourceSystem resourceSystem;
        private CharacterSystem characterSystem;
        private BuildingSystem buildingSystem;
        private EffectResolver effectResolver;
        private ContentDatabase content;
        private EventSystem eventSystem;
        private ExplorationSystem explorationSystem;
        private int advanceCount;
        private List<ExplorationFinishedEvent> finishedExplorations;

        [SetUp]
        public void Setup()
        {
            EventBus.Clear();
            finishedExplorations = new List<ExplorationFinishedEvent>();
            EventBus.Subscribe<ExplorationFinishedEvent>(e => finishedExplorations.Add(e));

            state = new GameState
            {
                gameSeed = "expl_test_seed",
                currentDay = 1,
                currentTimeSlot = TimeSlot.Action,
                gameplayState = GameplayState.Action,
                resources = new[]
                {
                    new ResourceState(ResourceType.Food, 50, 200),
                    new ResourceState(ResourceType.Water, 50, 200),
                    new ResourceState(ResourceType.Wood, 0, 200)
                },
                characters = new CharacterState[0],
                buildings = new BuildingState[0],
                relationships = new RelationshipState[0],
                gameFlags = new GameFlags(),
                eventHistory = new EventHistoryEntry[0],
                worldState = new WorldState { discoveredLocations = new string[0] }
            };

            randomSystem = new RandomSystem();
            randomSystem.Initialize("expl_test_seed");
            timeSystem = new TimeSystem();
            timeSystem.Initialize(state, randomSystem);
            timeSystem.SetTime(1, TimeSlot.Action);
            resourceSystem = new ResourceSystem();
            resourceSystem.Initialize(state);
            characterSystem = new CharacterSystem();
            buildingSystem = new BuildingSystem();
            characterSystem.Initialize(state, resourceSystem, randomSystem, buildingSystem);
            buildingSystem.Initialize(state, resourceSystem, characterSystem);
            effectResolver = new EffectResolver();
            effectResolver.Initialize(state, resourceSystem, characterSystem, buildingSystem);
            content = new ContentDatabase();
            eventSystem = new EventSystem();
            eventSystem.Initialize(state, timeSystem, randomSystem, content, effectResolver,
                                   resourceSystem, characterSystem);
            effectResolver.SetEventStarter(eventSystem.ScheduleNow);
            explorationSystem = new ExplorationSystem();
            explorationSystem.Initialize(state, randomSystem, content, eventSystem,
                                         resourceSystem, characterSystem);
            advanceCount = 0;
            explorationSystem.SetTimeAdvance(() =>
            {
                advanceCount++;
                timeSystem.AdvanceTimeSlot();
            });

            state.gameplayState = GameplayState.Action;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();

            if (gameManager != null)
            {
                Object.DestroyImmediate(gameManager.gameObject);
                gameManager = null;
            }

            if (!string.IsNullOrEmpty(SaveSystem.OverrideSaveDirectory) &&
                Directory.Exists(SaveSystem.OverrideSaveDirectory))
            {
                Directory.Delete(SaveSystem.OverrideSaveDirectory, true);
            }
            SaveSystem.OverrideSaveDirectory = null;
        }

        private static LocationDefinition MakeLocation(string id, int distance, int danger,
                                                       int unlockDay = 1,
                                                       ResourceYield[] yields = null,
                                                       string[] eventPool = null)
        {
            return new LocationDefinition
            {
                id = id,
                name = id,
                tier = 1,
                unlockDay = unlockDay,
                distance = distance,
                danger = danger,
                baseYield = yields ?? new[] { new ResourceYield { resource = ResourceType.Wood, min = 10, max = 10 } },
                eventPool = eventPool ?? new string[0],
                prerequisites = new string[0],
                tags = new string[0]
            };
        }

        private CharacterState AddSurvivor(string id, int health = 100)
        {
            var character = characterSystem.CreateCharacter(id, id, Profession.Hunter);
            character.health = health;
            character.maxHealth = 100;
            return character;
        }

        private GameManager StartNewGame()
        {
            var go = new GameObject("GameManager_ExplorationTest");
            gameManager = go.AddComponent<GameManager>();
            gameManager.NewGame("expl_gm_seed");
            return gameManager;
        }

        private static void PrepareSaveDirectory(string name)
        {
            SaveSystem.OverrideSaveDirectory = Path.Combine(Application.persistentDataPath, name);
            if (Directory.Exists(SaveSystem.OverrideSaveDirectory))
            {
                Directory.Delete(SaveSystem.OverrideSaveDirectory, true);
            }
        }

        private static void AddLocationTo(GameManager gm, LocationDefinition location)
        {
            gm.contentDatabase.AddLocation(location);
        }

        [Test]
        public void Explore_RejectsWrongSlotAndPendingEvent_ZeroStateChange()
        {
            content.AddLocation(MakeLocation("location_forest", 2, 1));
            var survivor = AddSurvivor("char_a");
            int foodBefore = resourceSystem.GetAmount(ResourceType.Food);

            timeSystem.SetTime(1, TimeSlot.Evening);
            state.gameplayState = GameplayState.Evening;
            Assert.IsFalse(TryExplore("location_forest", survivor.characterId, out var reason));
            Assert.IsNotEmpty(reason);

            state.gameplayState = GameplayState.Event;
            Assert.IsFalse(TryExplore("location_forest", survivor.characterId, out reason));
            Assert.IsNotEmpty(reason);

            Assert.AreEqual(foodBefore, resourceSystem.GetAmount(ResourceType.Food));
            Assert.IsNull(survivor.locationId);
            Assert.AreEqual(0, advanceCount);
            Assert.AreEqual(0, finishedExplorations.Count);
        }

        private bool TryExplore(string locationId, string memberId, out string reason)
        {
            return explorationSystem.ExploreLocation(locationId, new[] { memberId }, out reason);
        }

        [Test]
        public void Explore_RejectsEmptyAndOversizedTeam()
        {
            content.AddLocation(MakeLocation("location_forest", 2, 1));
            var a = AddSurvivor("char_a");
            AddSurvivor("char_b");
            AddSurvivor("char_c");
            AddSurvivor("char_d");
            var e = AddSurvivor("char_e");

            Assert.IsFalse(explorationSystem.ExploreLocation("location_forest", null, out var reason));
            Assert.IsNotEmpty(reason);

            Assert.IsFalse(explorationSystem.ExploreLocation("location_forest", new string[0], out reason));
            Assert.IsNotEmpty(reason);

            Assert.IsFalse(explorationSystem.ExploreLocation("location_forest",
                new[] { a.characterId, "char_b", "char_c", "char_d", e.characterId }, out reason));
            Assert.IsNotEmpty(reason);

            Assert.IsNull(a.locationId);
            Assert.AreEqual(0, finishedExplorations.Count);
        }

        [Test]
        public void Explore_RejectsBeforeUnlockDay()
        {
            content.AddLocation(MakeLocation("location_mine", 4, 2, unlockDay: 6));
            var survivor = AddSurvivor("char_a");

            Assert.IsFalse(TryExplore("location_mine", survivor.characterId, out var reason));
            StringAssert.Contains("day 6", reason);
            Assert.IsNull(survivor.locationId);
        }

        [Test]
        public void Explore_RejectsWhenFoodShort_ZeroStateChange()
        {
            content.AddLocation(MakeLocation("location_mine", 4, 2));
            var survivor = AddSurvivor("char_a");
            resourceSystem.Remove(ResourceType.Food, 49, "Test");

            int foodBefore = resourceSystem.GetAmount(ResourceType.Food);
            Assert.IsFalse(TryExplore("location_mine", survivor.characterId, out var reason));

            StringAssert.Contains("food", reason);
            Assert.AreEqual(foodBefore, resourceSystem.GetAmount(ResourceType.Food));
            Assert.IsNull(survivor.locationId);
        }

        [Test]
        public void Explore_DeductsRationsYieldsDiscoveryAndClearsTeam()
        {
            content.AddLocation(MakeLocation("location_forest", 2, 0));
            var survivor = AddSurvivor("char_a");
            int foodBefore = resourceSystem.GetAmount(ResourceType.Food);

            Assert.IsTrue(TryExplore("location_forest", survivor.characterId, out var reason), reason);

            Assert.AreEqual(foodBefore - 1, resourceSystem.GetAmount(ResourceType.Food));
            Assert.AreEqual(10, resourceSystem.GetAmount(ResourceType.Wood));
            CollectionAssert.Contains(state.worldState.discoveredLocations, "location_forest");
            Assert.IsNull(survivor.locationId);
            Assert.AreEqual(2, advanceCount);
            Assert.AreEqual(1, finishedExplorations.Count);
            Assert.AreEqual("location_forest", finishedExplorations[0].locationId);
            Assert.AreEqual(1, finishedExplorations[0].survivors);
            Assert.AreEqual(0, finishedExplorations[0].casualties);
        }

        [Test]
        public void Explore_TimeCost_AdvancesExactSlotsThroughGameManager()
        {
            PrepareSaveDirectory("ExplorationSystemTest_TimeCost");
            var gm = StartNewGame();
            AddLocationTo(gm, MakeLocation("location_forest", 2, 0));

            gm.AdvanceTimeSlot();
            gm.AdvanceTimeSlot();
            Assert.AreEqual(TimeSlot.Action, gm.GetCurrentTimeSlot());

            var survivor = gm.characterSystem.GetAliveCharacters()[0];
            Assert.IsTrue(gm.ExploreLocation("location_forest", new[] { survivor.characterId }, out var reason),
                reason);

            Assert.AreEqual(TimeSlot.Night, gm.GetCurrentTimeSlot());
            Assert.AreEqual(GameplayState.Night, gm.gameState.gameplayState);
        }

        [Test]
        public void Explore_LocationEventModal_ResolveThenFinish()
        {
            content.AddLocation(MakeLocation("location_forest", 2, 0,
                eventPool: new[] { "event_forest_bonus" }));
            content.AddEvent(new EventDefinition
            {
                id = "event_forest_bonus",
                name = "bonus",
                type = EventType.Exploration,
                phase = TimeSlot.Action,
                weight = 10f,
                options = new[]
                {
                    new EventOption
                    {
                        id = "grab",
                        text = "x",
                        effects = new[]
                        {
                            new Effect { type = EffectType.AddResource, resourceType = ResourceType.Water, intValue = 7 }
                        }
                    }
                }
            });
            var survivor = AddSurvivor("char_a");
            int foodBefore = resourceSystem.GetAmount(ResourceType.Food);
            int waterBefore = resourceSystem.GetAmount(ResourceType.Water);

            Assert.IsTrue(TryExplore("location_forest", survivor.characterId, out var reason), reason);

            Assert.IsNotNull(eventSystem.PendingEvent);
            Assert.AreEqual(GameplayState.Event, state.gameplayState);
            Assert.AreEqual("location_forest", survivor.locationId);
            Assert.AreEqual(foodBefore - 1, resourceSystem.GetAmount(ResourceType.Food));
            Assert.AreEqual(0, resourceSystem.GetAmount(ResourceType.Wood));
            Assert.AreEqual(0, advanceCount);

            Assert.IsTrue(eventSystem.ResolveChoice("grab"));

            Assert.AreEqual(waterBefore + 7, resourceSystem.GetAmount(ResourceType.Water));
            Assert.AreEqual(10, resourceSystem.GetAmount(ResourceType.Wood));
            Assert.IsNull(survivor.locationId);
            Assert.AreEqual(2, advanceCount);
            Assert.AreEqual(1, finishedExplorations.Count);
            CollectionAssert.Contains(state.worldState.discoveredLocations, "location_forest");
        }

        [Test]
        public void Explore_FatalDamage_KillsTeamMember()
        {
            content.AddLocation(MakeLocation("location_swamp", 2, 10));
            var doomed = AddSurvivor("char_doomed", health: 1);
            var sturdy = AddSurvivor("char_sturdy", health: 100);
            int foodBefore = resourceSystem.GetAmount(ResourceType.Food);
            int consumptionBefore = characterSystem.GetTotalFoodConsumption();

            Assert.IsTrue(explorationSystem.ExploreLocation("location_swamp",
                new[] { doomed.characterId, sturdy.characterId }, out var reason), reason);

            Assert.IsFalse(doomed.alive);
            Assert.IsTrue(sturdy.alive);
            Assert.IsNull(doomed.locationId);
            Assert.IsNull(sturdy.locationId);
            Assert.AreEqual(1, finishedExplorations[0].casualties);
            Assert.AreEqual(1, finishedExplorations[0].survivors);
            Assert.AreEqual(foodBefore - 2, resourceSystem.GetAmount(ResourceType.Food));
            Assert.Less(characterSystem.GetTotalFoodConsumption(), consumptionBefore);
        }

        [Test]
        public void Explore_Discovery_SurvivesSaveLoad()
        {
            PrepareSaveDirectory("ExplorationSystemTest_Discovery");
            var gm = StartNewGame();
            AddLocationTo(gm, MakeLocation("location_forest", 2, 0));

            gm.AdvanceTimeSlot();
            gm.AdvanceTimeSlot();

            var survivor = gm.characterSystem.GetAliveCharacters()[0];
            Assert.IsTrue(gm.ExploreLocation("location_forest", new[] { survivor.characterId }, out var reason),
                reason);
            CollectionAssert.Contains(gm.gameState.worldState.discoveredLocations, "location_forest");

            gm.SaveGame(false, "expl_discovery_save.json");
            Object.DestroyImmediate(gm.gameObject);
            gameManager = null;

            var reloaded = StartNewGame();
            reloaded.LoadGame("expl_discovery_save.json");

            CollectionAssert.Contains(reloaded.gameState.worldState.discoveredLocations, "location_forest");
        }

        [Test]
        public void Explore_DailyEventInterruptsAdvance_ResumesAfterResolve()
        {
            PrepareSaveDirectory("ExplorationSystemTest_Resume");
            var gm = StartNewGame();
            AddLocationTo(gm, MakeLocation("location_forest", 2, 0));
            gm.contentDatabase.AddEvent(new EventDefinition
            {
                id = "event_daily_blocker",
                name = "blocker",
                type = EventType.Normal,
                phase = TimeSlot.Evening,
                weight = 10f,
                options = new[] { new EventOption { id = "opt", text = "ok", effects = new Effect[0] } }
            });

            gm.AdvanceTimeSlot();
            gm.AdvanceTimeSlot();
            Assert.AreEqual(TimeSlot.Action, gm.GetCurrentTimeSlot());

            var survivor = gm.characterSystem.GetAliveCharacters()[0];
            Assert.IsTrue(gm.ExploreLocation("location_forest", new[] { survivor.characterId }, out var reason),
                reason);

            Assert.AreEqual(TimeSlot.Evening, gm.GetCurrentTimeSlot());
            Assert.AreEqual(GameplayState.Event, gm.gameState.gameplayState);
            Assert.IsNotNull(gm.GetPendingEvent());
            Assert.AreEqual("event_daily_blocker", gm.GetPendingEvent().id);

            Assert.IsTrue(gm.ResolveEventChoice("opt"));

            Assert.AreEqual(TimeSlot.Night, gm.GetCurrentTimeSlot());
            Assert.AreEqual(GameplayState.Night, gm.gameState.gameplayState);
            Assert.IsNull(gm.GetPendingEvent());
            CollectionAssert.Contains(gm.gameState.worldState.discoveredLocations, "location_forest");
            Assert.IsNull(survivor.locationId);
        }
    }
}
