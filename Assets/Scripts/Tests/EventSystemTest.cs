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
    public class EventSystemTest
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
        private List<EventStartedEvent> startedEvents;
        private List<EventFinishedEvent> finishedEvents;

        [SetUp]
        public void Setup()
        {
            EventBus.Clear();
            startedEvents = new List<EventStartedEvent>();
            finishedEvents = new List<EventFinishedEvent>();
            EventBus.Subscribe<EventStartedEvent>(e => startedEvents.Add(e));
            EventBus.Subscribe<EventFinishedEvent>(e => finishedEvents.Add(e));

            state = CreateState("event_test_seed");
            randomSystem = new RandomSystem();
            randomSystem.Initialize("event_test_seed");
            timeSystem = new TimeSystem();
            timeSystem.Initialize(state, randomSystem);
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

        private static GameState CreateState(string seed)
        {
            return new GameState
            {
                gameSeed = seed,
                currentDay = 1,
                currentTimeSlot = TimeSlot.Evening,
                gameplayState = GameplayState.Evening,
                resources = new[]
                {
                    new ResourceState(ResourceType.Food, 50, 200),
                    new ResourceState(ResourceType.Water, 50, 200),
                    new ResourceState(ResourceType.Wood, 0, 200),
                    new ResourceState(ResourceType.Stone, 0, 200),
                    new ResourceState(ResourceType.Iron, 0, 200)
                },
                characters = new CharacterState[0],
                buildings = new BuildingState[0],
                relationships = new RelationshipState[0],
                gameFlags = new GameFlags(),
                eventHistory = new EventHistoryEntry[0],
                worldState = new WorldState { discoveredLocations = new string[0] }
            };
        }

        private EventDefinition MakeEvent(string id, float weight = 10f, int cooldownDays = 0,
                                          EventCondition[] conditions = null,
                                          EventOption[] options = null,
                                          FollowUpEvent[] followUps = null,
                                          EventType type = EventType.Normal,
                                          int minDay = 0, int maxDay = 0)
        {
            var definition = new EventDefinition
            {
                id = id,
                name = id,
                type = type,
                phase = TimeSlot.Evening,
                weight = weight,
                cooldownDays = cooldownDays,
                minDay = minDay,
                maxDay = maxDay,
                conditions = conditions,
                options = options ?? new[]
                {
                    new EventOption { id = "opt", text = "ok", conditions = null, effects = new Effect[0] }
                },
                followUpEvents = followUps
            };
            content.AddEvent(definition);
            return definition;
        }

        private GameManager StartNewGame()
        {
            var go = new GameObject("GameManager_EventSystemTest");
            gameManager = go.AddComponent<GameManager>();
            gameManager.NewGame("event_gm_seed");
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

        private static void AddEventTo(GameManager gm, string id, float weight = 10f, int cooldownDays = 0)
        {
            gm.contentDatabase.AddEvent(new EventDefinition
            {
                id = id,
                name = id,
                type = EventType.Normal,
                phase = TimeSlot.Evening,
                weight = weight,
                cooldownDays = cooldownDays,
                options = new[] { new EventOption { id = "opt", text = "ok", effects = new Effect[0] } }
            });
        }

        [Test]
        public void DailyRoll_TriggersEvent_EntersEventState()
        {
            MakeEvent("event_roll_basic");

            bool started = eventSystem.TryRollDailyEvent();

            Assert.IsTrue(started);
            Assert.AreEqual(GameplayState.Event, state.gameplayState);
            Assert.IsNotNull(eventSystem.PendingEvent);
            Assert.AreEqual("event_roll_basic", eventSystem.PendingEvent.id);
            Assert.AreEqual(1, startedEvents.Count);
            Assert.AreEqual("event_roll_basic", startedEvents[0].eventId);
        }

        [Test]
        public void DailyRoll_OnlyOncePerDay()
        {
            Assert.IsFalse(eventSystem.TryRollDailyEvent());

            MakeEvent("event_roll_basic");

            Assert.IsFalse(eventSystem.TryRollDailyEvent());
            Assert.IsNull(eventSystem.PendingEvent);

            state.currentDay = 2;
            Assert.IsTrue(eventSystem.TryRollDailyEvent());
        }

        [Test]
        public void DailyRoll_SkippedWhileEventStateActive()
        {
            MakeEvent("event_roll_basic");
            state.gameplayState = GameplayState.Event;

            Assert.IsFalse(eventSystem.TryRollDailyEvent());
            Assert.IsNull(eventSystem.PendingEvent);
            Assert.IsFalse(state.gameFlags.intFlags.ContainsKey("event_roll_day"));
        }

        [Test]
        public void DailyRoll_ConditionNotMet_DoesNotTrigger()
        {
            MakeEvent("event_rich_only", conditions: new[]
            {
                new EventCondition
                {
                    type = ConditionType.ResourceAtLeast,
                    resource = ResourceType.Food,
                    amount = 999
                }
            });

            Assert.IsFalse(eventSystem.TryRollDailyEvent());
            Assert.IsNull(eventSystem.PendingEvent);
        }

        [Test]
        public void DailyRoll_CooldownBlocksUntilExpired()
        {
            MakeEvent("event_cooldown", cooldownDays: 3);

            Assert.IsTrue(eventSystem.TryRollDailyEvent());
            Assert.IsTrue(eventSystem.ResolveChoice("opt"));

            state.currentDay = 2;
            Assert.IsFalse(eventSystem.TryRollDailyEvent());

            state.currentDay = 4;
            Assert.IsTrue(eventSystem.TryRollDailyEvent());
        }

        [Test]
        public void ResolveChoice_ConditionNotMet_Rejected()
        {
            MakeEvent("event_cond", options: new[]
            {
                new EventOption
                {
                    id = "rich",
                    text = "x",
                    conditions = new[]
                    {
                        new EventCondition
                        {
                            type = ConditionType.ResourceAtLeast,
                            resource = ResourceType.Food,
                            amount = 999
                        }
                    },
                    effects = new[]
                    {
                        new Effect { type = EffectType.AddResource, resourceType = ResourceType.Wood, intValue = 5 }
                    }
                }
            });

            Assert.IsTrue(eventSystem.TryStartEvent("event_cond"));
            Assert.IsFalse(eventSystem.ResolveChoice("rich"));
            Assert.AreEqual(GameplayState.Event, state.gameplayState);
            Assert.AreEqual(0, state.eventHistory.Length);
            Assert.AreEqual(0, resourceSystem.GetAmount(ResourceType.Wood));
        }

        [Test]
        public void ResolveChoice_AppliesEffectsHistoryAndRestoresState()
        {
            MakeEvent("event_resolve", options: new[]
            {
                new EventOption
                {
                    id = "take",
                    text = "x",
                    effects = new[]
                    {
                        new Effect { type = EffectType.AddResource, resourceType = ResourceType.Wood, intValue = 5 }
                    }
                }
            });

            Assert.IsTrue(eventSystem.TryStartEvent("event_resolve"));
            Assert.AreEqual(GameplayState.Event, state.gameplayState);

            Assert.IsTrue(eventSystem.ResolveChoice("take"));

            Assert.AreEqual(5, resourceSystem.GetAmount(ResourceType.Wood));
            Assert.AreEqual(1, state.eventHistory.Length);
            Assert.AreEqual("event_resolve", state.eventHistory[0].eventId);
            Assert.AreEqual("take", state.eventHistory[0].choiceId);
            Assert.AreEqual(GameplayState.Evening, state.gameplayState);
            Assert.IsNull(eventSystem.PendingEvent);
            Assert.AreEqual(1, finishedEvents.Count);
            Assert.AreEqual("take", finishedEvents[0].choiceId);
        }

        [Test]
        public void ResolveChoice_RandomTargetPlaceholder_DamagesAnAliveCharacter()
        {
            var survivor = characterSystem.CreateCharacter("c1", "c1", Profession.Hunter);
            survivor.health = 100;
            survivor.maxHealth = 100;

            MakeEvent("event_random", options: new[]
            {
                new EventOption
                {
                    id = "hit",
                    text = "x",
                    effects = new[]
                    {
                        new Effect { type = EffectType.DamageCharacter, targetId = "@random", intValue = 12 }
                    }
                }
            });

            Assert.IsTrue(eventSystem.TryStartEvent("event_random"));
            Assert.IsTrue(eventSystem.ResolveChoice("hit"));

            Assert.AreEqual(88, survivor.health);
            Assert.AreEqual(1, state.eventHistory.Length);
            Assert.AreEqual("hit", state.eventHistory[0].choiceId);
        }

        [Test]
        public void ResolveChoice_BuildingPlaceholder_DamagesMatchingBuilding()
        {
            var generator = new BuildingState
            {
                buildingId = "generator_1_0",
                definitionId = "generator",
                durability = 100
            };
            state.buildings = new[] { generator };

            MakeEvent("event_bld", options: new[]
            {
                new EventOption
                {
                    id = "ignore",
                    text = "x",
                    effects = new[]
                    {
                        new Effect { type = EffectType.DamageBuilding, targetId = "@building:generator", intValue = 20 }
                    }
                }
            });

            Assert.IsTrue(eventSystem.TryStartEvent("event_bld"));
            Assert.IsTrue(eventSystem.ResolveChoice("ignore"));

            Assert.AreEqual(80, generator.durability);
        }

        [Test]
        public void FollowUp_ScheduledToday_ChainsImmediatelyAfterResolve()
        {
            MakeEvent("event_chain_a",
                followUps: new[] { new FollowUpEvent { id = "event_chain_b", delayDays = 0 } });
            MakeEvent("event_chain_b", weight: 5f);

            Assert.IsTrue(eventSystem.TryStartEvent("event_chain_a"));
            Assert.IsTrue(eventSystem.ResolveChoice("opt"));

            Assert.IsNotNull(eventSystem.PendingEvent);
            Assert.AreEqual("event_chain_b", eventSystem.PendingEvent.id);
            Assert.AreEqual(GameplayState.Event, state.gameplayState);
            Assert.AreEqual(2, startedEvents.Count);
        }

        [Test]
        public void FollowUp_DueTomorrow_TriggersBeforeWeightRoll_EvenWithZeroWeight()
        {
            MakeEvent("event_due_a",
                followUps: new[] { new FollowUpEvent { id = "event_due_b", delayDays = 1 } });
            MakeEvent("event_due_b", weight: 0f);

            Assert.IsTrue(eventSystem.TryStartEvent("event_due_a"));
            Assert.IsTrue(eventSystem.ResolveChoice("opt"));
            Assert.IsNull(eventSystem.PendingEvent);

            state.currentDay = 2;
            Assert.IsTrue(eventSystem.TryRollDailyEvent());
            Assert.AreEqual("event_due_b", eventSystem.PendingEvent.id);
        }

        [Test]
        public void StartEventEffect_ChainsToNextEventSameInteraction()
        {
            MakeEvent("event_eff_a", options: new[]
            {
                new EventOption
                {
                    id = "go",
                    text = "x",
                    effects = new[]
                    {
                        new Effect { type = EffectType.StartEvent, targetId = "event_eff_b" }
                    }
                }
            });
            MakeEvent("event_eff_b");

            Assert.IsTrue(eventSystem.TryStartEvent("event_eff_a"));
            Assert.IsTrue(eventSystem.ResolveChoice("go"));

            Assert.IsNotNull(eventSystem.PendingEvent);
            Assert.AreEqual("event_eff_b", eventSystem.PendingEvent.id);
        }

        [Test]
        public void UnlockLocationEffect_WritesDiscoveredLocationsOnce()
        {
            var effect = new Effect { type = EffectType.UnlockLocation, targetId = "location_forest" };

            Assert.IsTrue(effectResolver.Resolve(effect));
            Assert.IsTrue(effectResolver.Resolve(effect));

            Assert.AreEqual(1, state.worldState.discoveredLocations.Length);
            Assert.AreEqual("location_forest", state.worldState.discoveredLocations[0]);
        }

        [Test]
        public void EventState_BlocksAdvanceAndCommands_ZeroStateChange()
        {
            var gm = StartNewGame();
            AddEventTo(gm, "event_gate");

            for (int i = 0; i < 3; i++) gm.AdvanceTimeSlot();
            Assert.IsNotNull(gm.GetPendingEvent());

            TimeSlot slotBefore = gm.GetCurrentTimeSlot();
            int dayBefore = gm.GetCurrentDay();

            gm.AdvanceTimeSlot();

            Assert.AreEqual(slotBefore, gm.GetCurrentTimeSlot());
            Assert.AreEqual(dayBefore, gm.GetCurrentDay());
            Assert.IsFalse(gm.BuildBuilding("farm"));

            var survivor = gm.characterSystem.GetAliveCharacters()[0];
            Assert.IsFalse(gm.AssignWork(survivor.characterId, WorkType.Farming));
        }

        [Test]
        public void Cooldown_SurvivesSaveLoad()
        {
            PrepareSaveDirectory("EventSystemTest_CooldownSaves");
            var gm = StartNewGame();
            AddEventTo(gm, "event_cd_persist", cooldownDays: 5);
            gm.eventSystem.ScheduleNow("event_cd_persist");

            for (int i = 0; i < 3; i++) gm.AdvanceTimeSlot();
            Assert.IsNotNull(gm.GetPendingEvent());
            Assert.IsTrue(gm.ResolveEventChoice("opt"),
                $"diag pending={gm.GetPendingEvent()?.id} slot={gm.gameState.currentTimeSlot}");

            gm.SaveGame(false, "event_cd_save.json");
            Object.DestroyImmediate(gm.gameObject);
            gameManager = null;

            var reloaded = StartNewGame();
            reloaded.LoadGame("event_cd_save.json");

            int cooldownUntil;
            Assert.IsTrue(reloaded.gameState.gameFlags.intFlags
                .TryGetValue("event_cd_event_cd_persist", out cooldownUntil));
            Assert.AreEqual(reloaded.GetCurrentDay() + 5, cooldownUntil);

            for (int i = 0; i < 6; i++) reloaded.AdvanceTimeSlot();

            var pending = reloaded.GetPendingEvent();
            Assert.IsTrue(pending == null || pending.id != "event_cd_persist",
                $"diag pending={pending?.id}");
        }

        [Test]
        public void EventStateSave_ReloadReturnsValidSlotState()
        {
            PrepareSaveDirectory("EventSystemTest_EventNorm");
            var gm = StartNewGame();
            AddEventTo(gm, "event_norm");

            for (int i = 0; i < 3; i++) gm.AdvanceTimeSlot();
            Assert.AreEqual(GameplayState.Event, gm.gameState.gameplayState);

            gm.SaveGame(false, "event_norm_save.json");
            Object.DestroyImmediate(gm.gameObject);
            gameManager = null;

            var reloaded = StartNewGame();
            reloaded.LoadGame("event_norm_save.json");

            Assert.AreEqual(GameplayState.Evening, reloaded.gameState.gameplayState);
            Assert.IsNull(reloaded.GetPendingEvent());
            Assert.AreEqual(TimeSlot.Evening, reloaded.GetCurrentTimeSlot());

            reloaded.AdvanceTimeSlot();
            Assert.AreEqual(TimeSlot.Night, reloaded.GetCurrentTimeSlot());
        }

        [Test]
        public void WeightedRoll_SameSeedSamePool_SameSelection()
        {
            string first = RollWithFreshRig();
            string second = RollWithFreshRig();

            Assert.IsNotNull(first);
            Assert.AreEqual(first, second);
        }

        private static string RollWithFreshRig()
        {
            var rigState = CreateState("det_seed");
            var rigRandom = new RandomSystem();
            rigRandom.Initialize("det_seed");
            var rigTime = new TimeSystem();
            rigTime.Initialize(rigState, rigRandom);
            var rigResources = new ResourceSystem();
            rigResources.Initialize(rigState);
            var rigCharacters = new CharacterSystem();
            var rigBuildings = new BuildingSystem();
            rigCharacters.Initialize(rigState, rigResources, rigRandom, rigBuildings);
            rigBuildings.Initialize(rigState, rigResources, rigCharacters);
            var rigEffects = new EffectResolver();
            rigEffects.Initialize(rigState, rigResources, rigCharacters, rigBuildings);
            var rigContent = new ContentDatabase();

            string[] ids = { "event_det_a", "event_det_b", "event_det_c" };
            float[] weights = { 1f, 5f, 10f };
            for (int i = 0; i < ids.Length; i++)
            {
                rigContent.AddEvent(new EventDefinition
                {
                    id = ids[i],
                    name = ids[i],
                    type = EventType.Normal,
                    phase = TimeSlot.Evening,
                    weight = weights[i],
                    options = new[] { new EventOption { id = "opt", text = "ok", effects = new Effect[0] } }
                });
            }

            var rigEvents = new EventSystem();
            rigEvents.Initialize(rigState, rigTime, rigRandom, rigContent, rigEffects,
                                 rigResources, rigCharacters);

            return rigEvents.TryRollDailyEvent() && rigEvents.PendingEvent != null
                ? rigEvents.PendingEvent.id
                : null;
        }
    }
}
