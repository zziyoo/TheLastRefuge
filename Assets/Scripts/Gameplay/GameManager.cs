using System;
using UnityEngine;
using LastRefuge.Data;
using LastRefuge.Core;
using LastRefuge.Systems;
using LastRefuge.Save;

namespace LastRefuge.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>
        /// Instance is self-healing: a caller that runs before any Awake (or after a
        /// domain reload) still gets the singleton instead of null.
        /// </summary>
        public static GameManager GetOrCreate()
        {
            if (Instance != null) return Instance;

            Instance = UnityEngine.Object.FindObjectOfType<GameManager>();
            if (Instance != null) return Instance;

            var go = new GameObject("GameManager");
            Instance = go.AddComponent<GameManager>();
            return Instance;
        }

        [Header("Game State")]
        public GameState gameState;
        
        [Header("Systems")]
        public ITimeSystem timeSystem;
        public IRandomSystem randomSystem;
        public IResourceSystem resourceSystem;
        public ICharacterSystem characterSystem;
        public IBuildingSystem buildingSystem;
        public EffectResolver effectResolver;
        public ISaveSystem saveSystem;
        public IEventSystem eventSystem;
        public IContentDatabase contentDatabase;
        
        [Header("Settings")]
        public string gameVersion = "0.1.0";
        public bool autoSaveOnDayEnd = true;
        
        /// <summary>
        /// True while a run is actually active (New Game or successful Load).
        /// Quit/pause autosaves are gated on this so returning to the main menu or
        /// quitting before starting a run can never manufacture an invalid autosave.
        /// </summary>
        [HideInInspector]
        public bool hasActiveGame = false;
        
        private bool isInitialized = false;
        
        private void Awake()
        {
            // Only one GameManager may live. A second copy (duplicate scene object, or
            // one left over from a previous scene) must never steal the singleton.
            if (Instance != null && Instance != this)
            {
                UnityEngine.Debug.LogWarning($"GameManager: duplicate instance on '{name}' destroyed, keeping the existing one.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureCoreSystems();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (timeSystem != null)
            {
                timeSystem.OnDayEnd -= OnDayEnd;
            }
        }

        /// <summary>
        /// Builds every system exactly once. Safe to call from Awake and from any later
        /// entry point that needs a guaranteed-ready manager.
        /// </summary>
        public void EnsureCoreSystems()
        {
            if (isInitialized) return;
            InitializeCoreSystems();
        }

        private void InitializeCoreSystems()
        {
            gameState = new GameState();
            
            timeSystem = new TimeSystem();
            randomSystem = new RandomSystem();
            resourceSystem = new ResourceSystem();
            characterSystem = new CharacterSystem();
            buildingSystem = new BuildingSystem();
            effectResolver = new EffectResolver();
            saveSystem = new SaveSystem();
            
            contentDatabase = ContentLoader.LoadAll();
            foreach (var diagnostic in contentDatabase.Diagnostics)
            {
                UnityEngine.Debug.LogError($"Content diagnostic: {diagnostic}");
            }
            foreach (var problem in contentDatabase.Validate())
            {
                UnityEngine.Debug.LogError($"Content validation: {problem}");
            }
            
            eventSystem = new EventSystem();
            
            timeSystem.Initialize(gameState, randomSystem);
            resourceSystem.Initialize(gameState, buildingSystem, characterSystem);
            characterSystem.Initialize(gameState, resourceSystem, randomSystem, buildingSystem);
            buildingSystem.Initialize(gameState, resourceSystem, characterSystem);
            effectResolver.Initialize(gameState, resourceSystem, characterSystem, buildingSystem);
            saveSystem.Initialize(gameState, gameVersion);
            eventSystem.Initialize(gameState, timeSystem, randomSystem, contentDatabase, effectResolver,
                                   resourceSystem, characterSystem);
            
            effectResolver.SetEventStarter(eventSystem.ScheduleNow);
            
            timeSystem.OnDayEnd += OnDayEnd;
            
            isInitialized = true;
            
            UnityEngine.Debug.Log("GameManager initialized");
        }
        
        private void Start()
        {
            SetupMobilePortrait();
        }
        
        private void SetupMobilePortrait()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
        
        public void NewGame(string seed = null)
        {
            EnsureCoreSystems();

            saveSystem.CreateNewGame(seed);

            // CreateNewGame wipes the arrays on gameState, but the live amounts live
            // inside the systems. Re-bind every system so all of them point at the fresh
            // state, otherwise one of them keeps serving the previous run's data.
            RebindSystemsToState();

            randomSystem.Initialize(gameState.gameSeed);

            GenerateInitialState();

            // A new game must never be empty: repair anything the generators failed to
            // produce so the UI always has real data to bind.
            ValidateAndRepairInitialState();

            timeSystem.SetTime(1, TimeSlot.Morning);
            timeSystem.SetGameplayState(GameplayState.Morning);

            hasActiveGame = true;

            UnityEngine.Debug.Log($"New game started with seed: {gameState.gameSeed}");
            LogGameStateSnapshot("NewGame");
        }

        /// <summary>
        /// Points every system at the current GameState. Used by NewGame and LoadGame so
        /// the whole UI -> GameManager -> GameState -> Systems chain is always consistent.
        /// </summary>
        private void RebindSystemsToState()
        {
            timeSystem.Initialize(gameState, randomSystem);
            resourceSystem.Initialize(gameState, buildingSystem, characterSystem);
            characterSystem.Initialize(gameState, resourceSystem, randomSystem, buildingSystem);
            buildingSystem.Initialize(gameState, resourceSystem, characterSystem);
            effectResolver.Initialize(gameState, resourceSystem, characterSystem, buildingSystem);
            saveSystem.Initialize(gameState, gameVersion);
            eventSystem.Initialize(gameState, timeSystem, randomSystem, contentDatabase, effectResolver,
                                   resourceSystem, characterSystem);

            timeSystem.OnDayEnd -= OnDayEnd;
            timeSystem.OnDayEnd += OnDayEnd;
        }

        private void GenerateInitialState()
        {
            // Starting stock is sized so the first shelter plus one farm can be built
            // on day one: farm costs 30 Wood / 10 Stone, shelter costs 20 Wood / 10 Stone.
            resourceSystem.Add(ResourceType.Food, 30, "Initial");
            resourceSystem.Add(ResourceType.Water, 30, "Initial");
            resourceSystem.Add(ResourceType.Wood, 60, "Initial");
            resourceSystem.Add(ResourceType.Stone, 30, "Initial");
            resourceSystem.Add(ResourceType.Iron, 10, "Initial");

            characterSystem.GenerateInitialCharacters(4);

            buildingSystem.Build("shelter_temp");

            gameState.gameplayState = GameplayState.Morning;
        }

        /// <summary>
        /// Hard guarantee for a new game: non-zero starting resources, at least one
        /// survivor and at least one shelter core. Anything missing is created here
        /// instead of letting the player land on an empty colony.
        /// </summary>
        private void ValidateAndRepairInitialState()
        {
            if (resourceSystem.GetAmount(ResourceType.Food) <= 0)
            {
                resourceSystem.Add(ResourceType.Food, 30, "InitialRepair");
            }
            if (resourceSystem.GetAmount(ResourceType.Water) <= 0)
            {
                resourceSystem.Add(ResourceType.Water, 30, "InitialRepair");
            }
            if (resourceSystem.GetAmount(ResourceType.Wood) <= 0)
            {
                resourceSystem.Add(ResourceType.Wood, 60, "InitialRepair");
            }

            if (characterSystem.GetAliveCharacters().Length <= 0)
            {
                UnityEngine.Debug.LogWarning("GameManager: no survivors generated, creating the starting character.");
                characterSystem.GenerateInitialCharacters(1);
            }

            if (buildingSystem.GetAllBuildings().Length <= 0)
            {
                UnityEngine.Debug.LogWarning("GameManager: no shelter built, falling back to a free shelter core.");
                buildingSystem.ForceBuildFree("shelter_temp");
            }
        }

        /// <summary>
        /// Requirement: entering a game must print the real state so an empty colony is
        /// immediately visible in the log instead of only in the UI.
        /// </summary>
        public void LogGameStateSnapshot(string context)
        {
            if (gameState == null)
            {
                UnityEngine.Debug.LogError($"[{context}] GameState is null");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{context}] GameState created");
            sb.AppendLine("Resource:");
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                int amount = resourceSystem != null ? resourceSystem.GetAmount(type) : 0;
                sb.AppendLine($"  {type.ToString().ToLowerInvariant()}={amount}");
            }
            sb.AppendLine($"Character count={(characterSystem != null ? characterSystem.GetAliveCharacters().Length : 0)}");
            sb.AppendLine($"Building count={(buildingSystem != null ? buildingSystem.GetAllBuildings().Length : 0)}");
            UnityEngine.Debug.Log(sb.ToString());
        }
        
        public void LoadGame(string fileName)
        {
            EnsureCoreSystems();

            if (saveSystem.LoadGame(fileName))
            {
                randomSystem.Initialize(gameState.gameSeed);

                RebindSystemsToState();

                if (IsEventStateBlocked())
                {
                    eventSystem.ClearPending();
                    UpdateGameplayState();
                }

                hasActiveGame = true;

                LogGameStateSnapshot("LoadGame");
            }
        }
        
        public void AdvanceTimeSlot()
        {
            if (!isInitialized) return;
            if (IsEventStateBlocked()) return;
            
            TimeSlot currentSlot = timeSystem.CurrentTimeSlot;
            GameplayState currentState = timeSystem.GetGameplayState();
            
            ProcessTimeSlot(currentSlot);
            
            timeSystem.AdvanceTimeSlot();
            
            UpdateGameplayState();
            
            if (currentSlot == TimeSlot.Action)
            {
                eventSystem.TryRollDailyEvent();
            }
            
            // The day has been settled (DayEnd) and rolled over to the next Morning:
            // autosave from this stable point so a load never resumes at an un-settled
            // DayEnd. This is the only transition that lands on Morning.
            if (timeSystem.CurrentTimeSlot == TimeSlot.Morning && autoSaveOnDayEnd)
            {
                saveSystem.SaveGame(true);
            }
        }
        
        private bool IsEventStateBlocked()
        {
            return gameState.gameplayState == GameplayState.Event ||
                   gameState.gameplayState == GameplayState.Resolution;
        }
        
        private void ProcessTimeSlot(TimeSlot slot)
        {
            switch (slot)
            {
                case TimeSlot.Morning:
                    ProcessMorning();
                    break;
                case TimeSlot.Planning:
                    break;
                case TimeSlot.Action:
                    ProcessAction();
                    break;
                case TimeSlot.Evening:
                    ProcessEvening();
                    break;
                case TimeSlot.Night:
                    ProcessNight();
                    break;
                case TimeSlot.DayEnd:
                    ProcessDayEnd();
                    break;
            }
        }
        
        private void ProcessMorning()
        {
            UnityEngine.Debug.Log($"=== Day {gameState.currentDay} Morning ===");
            timeSystem.SetGameplayState(GameplayState.Planning);
        }
        
        private void ProcessAction()
        {
            UnityEngine.Debug.Log($"=== Day {gameState.currentDay} Action ===");
            
            // Action: buildings produce (and pay their running cost), the colony then eats
            // and drinks from what the day actually produced and what is in storage.
            buildingSystem.ProcessBuildingProduction();
            characterSystem.ProcessDailyConsumption();
            
            timeSystem.SetGameplayState(GameplayState.Action);
        }
        
        private void ProcessEvening()
        {
            UnityEngine.Debug.Log($"=== Day {gameState.currentDay} Evening ===");
            timeSystem.SetGameplayState(GameplayState.Evening);
        }
        
        private void ProcessNight()
        {
            UnityEngine.Debug.Log($"=== Day {gameState.currentDay} Night ===");
            // Night: characters rest, fatigue and stress recover, starvation takes its toll.
            characterSystem.ProcessNightRecovery();
            timeSystem.SetGameplayState(GameplayState.Night);
        }
        
        private void ProcessDayEnd()
        {
            UnityEngine.Debug.Log($"=== Day {gameState.currentDay} End ===");
            // DayEnd: the colony stats are recomputed and the daily report is written.
            // The autosave happens in AdvanceTimeSlot after the rollover to the next
            // Morning, so the saved state is always a stable, continuable day start.
            buildingSystem.RecalculateAllBuildingEffects();
            
            GenerateDailyReport();
        }
        
        private void OnDayEnd()
        {
            timeSystem.SetGameplayState(GameplayState.Morning);
        }
        
        private void UpdateGameplayState()
        {
            TimeSlot slot = timeSystem.CurrentTimeSlot;
            
            switch (slot)
            {
                case TimeSlot.Morning:
                    timeSystem.SetGameplayState(GameplayState.Morning);
                    break;
                case TimeSlot.Planning:
                    timeSystem.SetGameplayState(GameplayState.Planning);
                    break;
                case TimeSlot.Action:
                    timeSystem.SetGameplayState(GameplayState.Action);
                    break;
                case TimeSlot.Evening:
                    timeSystem.SetGameplayState(GameplayState.Evening);
                    break;
                case TimeSlot.Night:
                    timeSystem.SetGameplayState(GameplayState.Night);
                    break;
                case TimeSlot.DayEnd:
                    timeSystem.SetGameplayState(GameplayState.DayEnd);
                    break;
            }
        }
        
        private void GenerateDailyReport()
        {
            int food = resourceSystem.GetAmount(ResourceType.Food);
            int water = resourceSystem.GetAmount(ResourceType.Water);
            int pop = buildingSystem.GetCurrentPopulation();
            
            UnityEngine.Debug.Log($"Daily Report - Day {gameState.currentDay}: Food={food}, Water={water}, Population={pop}");
        }
        
        public bool AssignWork(string characterId, WorkType workType, string buildingId = null)
        {
            if (IsEventStateBlocked()) return false;
            return characterSystem.AssignWork(characterId, workType, buildingId);
        }
        
        public bool BuildBuilding(string definitionId)
        {
            if (IsEventStateBlocked()) return false;
            return buildingSystem.Build(definitionId) != null;
        }
        
        public bool UpgradeBuilding(string buildingId)
        {
            if (IsEventStateBlocked()) return false;
            return buildingSystem.UpgradeBuilding(buildingId);
        }
        
        public void SaveGame(bool autoSave = false, string customName = null)
        {
            saveSystem.SaveGame(autoSave, customName);
        }
        
        public string GetGameSeed()
        {
            return gameState.gameSeed;
        }
        
        public int GetCurrentDay()
        {
            return gameState.currentDay;
        }
        
        public TimeSlot GetCurrentTimeSlot()
        {
            return gameState.currentTimeSlot;
        }
        
        public GameplayState GetCurrentGameplayState()
        {
            return gameState.gameplayState;
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && hasActiveGame)
            {
                CommitAutosaveBeforeExit();
            }
        }
        
        private void OnApplicationQuit()
        {
            if (!hasActiveGame) return;
            CommitAutosaveBeforeExit();
        }
        
        /// <summary>
        /// Save on exit. At DayEnd the day has not been settled yet, so first roll over
        /// to the next Morning (which settles the day and writes the autosave). Never
        /// persist an un-settled DayEnd snapshot.
        /// </summary>
        private void CommitAutosaveBeforeExit()
        {
            if (IsEventStateBlocked())
            {
                eventSystem.ClearPending();
                UpdateGameplayState();
            }

            if (timeSystem.CurrentTimeSlot == TimeSlot.DayEnd)
            {
                AdvanceTimeSlot();
            }
            else if (autoSaveOnDayEnd)
            {
                saveSystem.SaveGame(true);
            }
        }

        public bool ResolveEventChoice(string optionId)
        {
            return eventSystem.ResolveChoice(optionId);
        }

        public EventDefinition GetPendingEvent()
        {
            return eventSystem.PendingEvent;
        }
    }
}